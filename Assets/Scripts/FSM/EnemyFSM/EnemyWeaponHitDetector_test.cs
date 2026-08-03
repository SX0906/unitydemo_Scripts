using UnityEngine;
using System.Collections.Generic;

public class EnemyWeaponHitDetector_test : MonoBehaviour
{
    private float _currentDamage;
    private bool _hitWindowOpen;
    private ICombatant _owner;
    private readonly HashSet<ICombatTarget> _hitTargets = new();
    private Collider _weaponCollider;
    private HitEffectSpawner _hitEffectSpawner;

    public float damageMultiplier = 1f;

    private void Awake()
    {
        _weaponCollider = GetComponent<Collider>();
        if (_weaponCollider != null) { _weaponCollider.isTrigger = true; _weaponCollider.enabled = false; }
        _owner = GetComponentInParent<ICombatant>();
        _hitEffectSpawner = GetComponent<HitEffectSpawner>();
    }

    public void SetCurrentDamage(float damage) { _currentDamage = damage; }
    public void OnEnemyHitWindowOpen() { _hitWindowOpen = true; _hitTargets.Clear(); if (_weaponCollider != null) _weaponCollider.enabled = true; }
    public void OnEnemyHitWindowClose() { _hitWindowOpen = false; _hitTargets.Clear(); if (_weaponCollider != null) _weaponCollider.enabled = false; }

    private void OnTriggerEnter(Collider other)
    {
        if (!_hitWindowOpen) return;
        ICombatTarget target = other.GetComponentInParent<ICombatTarget>();
        if (target == null) return;
        if (!_hitTargets.Add(target)) return;
        _hitEffectSpawner?.SpawnAtContact(other);

        float finalDamage = _currentDamage * damageMultiplier;
        if (_owner?.Vitals is EnemyVitals ev && ev.RagePercent >= 1f) finalDamage *= 1.05f;

        target.TakeHit(new HitContext(
            "F", (target.Transform.position - transform.root.position).normalized,
            false, transform.root, finalDamage, false));
    }
}
