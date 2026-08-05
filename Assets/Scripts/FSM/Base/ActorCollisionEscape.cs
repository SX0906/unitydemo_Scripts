using System.Collections.Generic;
using UnityEngine;

public static class ActorCollisionEscape
{
    private static readonly LayerMask ActorMask = LayerMask.GetMask("Player", "Enemy");

    private const float IgnoreDuration = 0.2f;
    private const float DefaultEscapeSpeed = 3f;

    private sealed class IgnoreEntry
    {
        public CharacterController Self;
        public Transform SupportRoot;
        public float StartTime;
    }

    private static readonly Dictionary<int, IgnoreEntry> IgnoreEntries = new Dictionary<int, IgnoreEntry>();

    public static bool IsSupportedByActor(CharacterController controller)
    {
        return TryGetSupportCollider(controller, out _);
    }

    public static bool MoveOffActor(CharacterController controller, Vector3 horizontalVelocity, float fallSpeed)
    {
        if (controller == null || !controller.enabled)
            return false;

        if (!TryGetSupportCollider(controller, out Collider support))
            return false;

        Transform supportRoot = support.transform.root;
        int key = GetPairKey(controller, supportRoot);

        if (!IgnoreEntries.TryGetValue(key, out IgnoreEntry entry))
        {
            entry = new IgnoreEntry
            {
                Self = controller,
                SupportRoot = supportRoot,
                StartTime = Time.unscaledTime
            };
            IgnoreColliders(controller, supportRoot, true);
            IgnoreEntries.Add(key, entry);
        }

        Vector3 horizontal = horizontalVelocity;
        horizontal.y = 0f;

        if (horizontal.sqrMagnitude < 0.0001f)
        {
            Vector3 away = controller.transform.position - supportRoot.position;
            away.y = 0f;
            horizontal = away.sqrMagnitude > 0.0001f
                ? away.normalized * DefaultEscapeSpeed
                : Vector3.zero;
        }

        Vector3 motion = horizontal + Vector3.down * Mathf.Max(0f, fallSpeed);
        controller.Move(motion * Time.deltaTime);
        return true;
    }

    public static void Tick(CharacterController controller)
    {
        if (IgnoreEntries.Count == 0 || controller == null)
            return;

        List<int> removeKeys = null;

        foreach (KeyValuePair<int, IgnoreEntry> pair in IgnoreEntries)
        {
            IgnoreEntry entry = pair.Value;
            if (entry.Self != controller)
                continue;

            if (entry.Self == null || entry.SupportRoot == null)
            {
                (removeKeys ??= new List<int>()).Add(pair.Key);
                continue;
            }

            if (Time.unscaledTime - entry.StartTime < IgnoreDuration)
                continue;

            IgnoreColliders(controller, entry.SupportRoot, false);
            (removeKeys ??= new List<int>()).Add(pair.Key);
        }

        if (removeKeys == null)
            return;

        foreach (int key in removeKeys)
            IgnoreEntries.Remove(key);
    }

    private static bool TryGetSupportCollider(CharacterController controller, out Collider support)
    {
        support = null;

        if (controller == null || !controller.enabled)
            return false;

        Vector3 origin = controller.transform.TransformPoint(controller.center);
        float radius = Mathf.Max(0.05f, controller.radius * 0.8f);
        float distance = Mathf.Max(0.2f, controller.height * 0.5f - controller.radius + 0.15f);

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            radius,
            Vector3.down,
            distance,
            ActorMask,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider.transform.root == controller.transform.root)
                continue;

            if (!IsActorCollider(hit.collider))
                continue;

            support = hit.collider;
            return true;
        }

        return false;
    }

    private static bool IsActorCollider(Collider collider)
    {
        return collider.GetComponentInParent<TestFSM>() != null
            || collider.GetComponentInParent<EnemyFSM>() != null
            || collider.GetComponentInParent<ActorBase>() != null;
    }

    private static void IgnoreColliders(CharacterController self, Transform supportRoot, bool ignore)
    {
        if (self == null || supportRoot == null)
            return;

        Collider[] colliders = supportRoot.GetComponentsInChildren<Collider>(true);
        foreach (Collider other in colliders)
        {
            if (other == null || other == self || other.isTrigger)
                continue;

            Physics.IgnoreCollision(self, other, ignore);
        }
    }

    private static int GetPairKey(CharacterController self, Transform supportRoot)
    {
        return (self.GetInstanceID() * 397) ^ supportRoot.GetInstanceID();
    }
}
