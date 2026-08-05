using UnityEngine;
using System.Collections.Generic;

public class WeaponHitDetector_test : MonoBehaviour
{
    private string _currentHitDirTag;
    private readonly HashSet<ICombatTarget> _hitTargets = new();
    private Collider _weaponCollider;
    private ICombatant _owner;
    private HitEffectSpawner _hitEffectSpawner;
    private Vector3 _previousBladeBase;
    private Vector3 _previousBladeTip;
    private bool _hasPreviousBladePose;
    private readonly Collider[] _overlapBuffer = new Collider[32];
    private readonly RaycastHit[] _sweepHitBuffer = new RaycastHit[32];

    public float damage = 10f;
    public float rageGainPerHit = 5f;
    private const float BackHitAngleThreshold = 100f;

    private void Awake()
    {
        _weaponCollider = GetComponent<Collider>();
        if (_weaponCollider != null)
        {
            _weaponCollider.isTrigger = true;
            _weaponCollider.enabled = false;
        }
        _owner = GetComponentInParent<ICombatant>();
        _hitEffectSpawner = GetComponent<HitEffectSpawner>();
    }

    public void OnHitWindowOpen(string dirTag)
    {
        _currentHitDirTag = dirTag;
        _hitTargets.Clear();
        if (_weaponCollider != null)
        {
            _weaponCollider.enabled = true;
            _hasPreviousBladePose = false;
            ScanWeaponOverlap();
        }
    }

    public void OnHitWindowClose()
    {
        _currentHitDirTag = string.Empty;
        _hitTargets.Clear();
        _hasPreviousBladePose = false;
        if (_weaponCollider != null) _weaponCollider.enabled = false;
    }

    private void FixedUpdate()
    {
        if (string.IsNullOrEmpty(_currentHitDirTag)) return;
        ScanWeaponOverlap();
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        ProcessHit(other);
    }

    private void ProcessHit(Collider other)
    {
        if (string.IsNullOrEmpty(_currentHitDirTag)) return;
        ICombatTarget target = other.GetComponentInParent<ICombatTarget>();
        if (target == null) return;
        if (target.Transform == (_owner?.Transform ?? transform.root)) return;
        if (!_hitTargets.Add(target)) return;
        _hitEffectSpawner?.SpawnAtContact(other);

        Transform ownerTransform = _owner?.Transform ?? transform.root;
        Vector3 dir = GetBladeContactPoint(other) - ownerTransform.position;
        dir.y = 0;
        if (dir.magnitude < 0.01f)
        {
            dir = target.Transform.position - ownerTransform.position;
            dir.y = 0;
        }
        if (dir.magnitude < 0.01f) dir = ownerTransform.forward;

        Vector3 toAttacker = ownerTransform.position - target.Transform.position;
        toAttacker.y = 0;
        float backAngle = toAttacker.magnitude > 0.01f
            ? Vector3.Angle(target.Transform.forward, toAttacker) : 0f;
        bool isBackHit = backAngle >= BackHitAngleThreshold;
        string finalDirTag = isBackHit ? "B" : _currentHitDirTag;

        var hit = new HitContext(finalDirTag, dir, false, ownerTransform, damage, false);
        bool damaged = target.TakeHit(hit);

        if (damaged && _owner?.Vitals != null)
        {
            _owner.Vitals.GainRage(rageGainPerHit);
        }
    }

    private void ScanWeaponOverlap()
    {
        if (_weaponCollider == null || !_weaponCollider.enabled) return;

        Physics.SyncTransforms();

        if (!(_weaponCollider is BoxCollider box)) return;

        Vector3 center = box.transform.TransformPoint(box.center);
        Vector3 scale = box.transform.lossyScale;
        Vector3 halfExtents = new Vector3(
            Mathf.Abs(box.size.x * scale.x) * 0.5f,
            Mathf.Abs(box.size.y * scale.y) * 0.5f,
            Mathf.Abs(box.size.z * scale.z) * 0.5f
        );

        ScanBoxVolume(
            center,
            halfExtents,
            box.transform.rotation
        );

        if (!TryGetLiveBladeSegment(out Vector3 currentBase, out Vector3 currentTip))
        {
            _hasPreviousBladePose = false;
            return;
        }

        float sweepRadius = Mathf.Max(
            Mathf.Min(
                Mathf.Abs(box.size.x * scale.x),
                Mathf.Abs(box.size.z * scale.z)
            ) * 0.5f + 0.02f,
            0.04f
        );

        if (_hasPreviousBladePose)
        {
            SweepBladeSegment(
                _previousBladeBase,
                _previousBladeTip,
                currentBase,
                currentTip,
                sweepRadius
            );
        }
        else
        {
            SweepBladePoint(currentBase, sweepRadius);
            SweepBladePoint(currentTip, sweepRadius);
        }

        _previousBladeBase = currentBase;
        _previousBladeTip = currentTip;
        _hasPreviousBladePose = true;
    }

    private void ScanBoxVolume(
        Vector3 center,
        Vector3 halfExtents,
        Quaternion rotation)
    {
        int count = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            _overlapBuffer,
            rotation,
            ~0,
            QueryTriggerInteraction.Collide
        );

        ProcessOverlapBuffer(count);
    }

    private void SweepBladeSegment(
        Vector3 previousBase,
        Vector3 previousTip,
        Vector3 currentBase,
        Vector3 currentTip,
        float radius)
    {
        SweepPointPath(previousBase, currentBase, radius);
        SweepPointPath(previousTip, currentTip, radius);
    }

    private void SweepBladePoint(Vector3 point, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(
            point,
            radius,
            _overlapBuffer,
            ~0,
            QueryTriggerInteraction.Collide
        );

        ProcessOverlapBuffer(count);
    }

    private void SweepPointPath(
        Vector3 from,
        Vector3 to,
        float radius)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;

        if (distance < 0.0001f)
        {
            SweepBladePoint(from, radius);
            return;
        }

        int count = Physics.SphereCastNonAlloc(
            from,
            radius,
            delta / distance,
            _sweepHitBuffer,
            distance,
            ~0,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < count; i++)
        {
            Collider other = _sweepHitBuffer[i].collider;
            if (other == null || other == _weaponCollider) continue;
            ProcessHit(other);
        }
    }

    private void ProcessOverlapBuffer(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Collider other = _overlapBuffer[i];
            if (other == null || other == _weaponCollider) continue;
            ProcessHit(other);
        }
    }

    private Vector3 GetBladeContactPoint(Collider other)
    {
        if (!TryGetLiveBladeSegment(out Vector3 bladeBase, out Vector3 bladeTip))
        {
            Vector3 fallback = _weaponCollider != null
                ? _weaponCollider.bounds.center
                : transform.position;
            return other.ClosestPoint(fallback);
        }

        Vector3 targetCenter = other.bounds.center;
        Vector3 bladePoint = ClosestPointOnSegment(
            bladeBase,
            bladeTip,
            targetCenter
        );

        return other.ClosestPoint(bladePoint);
    }

    private bool TryGetLiveBladeSegment(
        out Vector3 bladeBase,
        out Vector3 bladeTip)
    {
        if (_hitEffectSpawner != null &&
            _hitEffectSpawner.bladeBase != null &&
            _hitEffectSpawner.bladeTip != null)
        {
            bladeBase = _hitEffectSpawner.bladeBase.position;
            bladeTip = _hitEffectSpawner.bladeTip.position;
            return true;
        }

        bladeBase = _weaponCollider != null
            ? _weaponCollider.bounds.center
            : transform.position;
        bladeTip = bladeBase;
        return false;
    }

    private static Vector3 ClosestPointOnSegment(
        Vector3 segmentStart,
        Vector3 segmentEnd,
        Vector3 point)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float lengthSqr = segment.sqrMagnitude;

        if (lengthSqr < 0.000001f)
            return segmentStart;

        float t = Mathf.Clamp01(
            Vector3.Dot(point - segmentStart, segment) / lengthSqr
        );

        return segmentStart + segment * t;
    }
}
