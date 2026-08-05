using System.Collections.Generic;
using UnityEngine;

public static class ActorCollisionEscape
{
    private static readonly LayerMask ActorMask = LayerMask.GetMask("Player", "Enemy");
    private const float IgnoreDuration = 0.2f;
    private const float DefaultEscapeSpeed = 5f;
    private const float DefaultFallSpeed = 6f;

    private sealed class IgnoreEntry
    {
        public CharacterController Self;
        public int SavedExcludeLayers;
        public int OtherLayer;
        public float StartTime;
    }

    private static readonly Dictionary<int, IgnoreEntry> Entries = new Dictionary<int, IgnoreEntry>();

    public static bool IsOverlappingActor(CharacterController controller, out int otherLayer)
    {
        otherLayer = -1;
        if (controller == null || !controller.enabled) return false;

        Vector3 footPos = GetFootPosition(controller);
        float checkRadius = Mathf.Max(0.06f, controller.radius * 1.2f);

        Collider[] hits = Physics.OverlapSphere(footPos, checkRadius, ActorMask, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            if (hit.transform.root == controller.transform.root) continue;
            if (!IsActorCollider(hit)) continue;

            otherLayer = hit.gameObject.layer;
            return true;
        }
        return false;
    }

    public static void ResolveOverlap(CharacterController controller, int otherLayer)
    {
        if (controller == null || !controller.enabled || otherLayer < 0) return;

        int key = controller.GetInstanceID();
        if (!Entries.TryGetValue(key, out IgnoreEntry entry))
        {
            entry = new IgnoreEntry
            {
                Self = controller,
                SavedExcludeLayers = controller.excludeLayers,
                OtherLayer = otherLayer,
                StartTime = Time.unscaledTime
            };
            Entries[key] = entry;
        }
        else
        {
            entry.OtherLayer = otherLayer;
            entry.StartTime = Time.unscaledTime;
        }

        controller.excludeLayers = entry.SavedExcludeLayers | (1 << otherLayer);

        Vector3 away = Vector3.zero;
        Vector3 footPos = GetFootPosition(controller);
        float checkRadius = Mathf.Max(0.06f, controller.radius * 1.2f);
        Collider[] hits = Physics.OverlapSphere(footPos, checkRadius, ActorMask, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit == null || hit.transform.root == controller.transform.root) continue;
            if (!IsActorCollider(hit)) continue;
            Vector3 dir = controller.transform.position - hit.transform.root.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) { away = dir.normalized; break; }
        }
        if (away.sqrMagnitude < 0.0001f) away = controller.transform.forward;

        Vector3 motion = away * DefaultEscapeSpeed + (-controller.transform.up) * DefaultFallSpeed;
        controller.Move(motion * Time.deltaTime);
    }

    public static void Tick(CharacterController controller)
    {
        if (Entries.Count == 0 || controller == null) return;

        int key = controller.GetInstanceID();
        if (!Entries.TryGetValue(key, out IgnoreEntry entry)) return;

        if (entry.Self == null)
        {
            Entries.Remove(key);
            return;
        }

        if (Time.unscaledTime - entry.StartTime < IgnoreDuration) return;

        controller.excludeLayers = entry.SavedExcludeLayers;
        Entries.Remove(key);
    }

    private static Vector3 GetFootPosition(CharacterController controller)
    {
        Vector3 center = controller.transform.TransformPoint(controller.center);
        float halfHeight = controller.height * 0.5f;
        float footOffset = halfHeight;
        return center - controller.transform.up * footOffset;
    }

    private static bool IsActorCollider(Collider collider)
    {
        return collider.GetComponentInParent<TestFSM>() != null
            || collider.GetComponentInParent<EnemyFSM>() != null
            || collider.GetComponentInParent<ActorBase>() != null;
    }
}
