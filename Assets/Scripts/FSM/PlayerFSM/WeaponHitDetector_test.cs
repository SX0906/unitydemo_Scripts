using UnityEngine;
using System.Collections.Generic;

public class WeaponHitDetector_test : MonoBehaviour
{
    private string _currentHitDirTag;
    private readonly HashSet<ICombatTarget> _hitTargets = new();
    private Collider _weaponCollider;
    private ICombatant _owner;
    private HitEffectSpawner _hitEffectSpawner;

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
        if (_weaponCollider != null) _weaponCollider.enabled = true;
    }

    public void OnHitWindowClose()
    {
        _currentHitDirTag = string.Empty;
        _hitTargets.Clear();
        if (_weaponCollider != null) _weaponCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (string.IsNullOrEmpty(_currentHitDirTag)) return;
        ICombatTarget target = other.GetComponentInParent<ICombatTarget>();
        if (target == null) return;
        if (!_hitTargets.Add(target)) return;
        _hitEffectSpawner?.SpawnAtContact(other);

        Vector3 dir = target.Transform.position - transform.position;
        dir.y = 0;
        if (dir.magnitude < 0.01f) dir = transform.root.forward;

        Transform ownerTransform = _owner?.Transform ?? transform.root;
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
}
