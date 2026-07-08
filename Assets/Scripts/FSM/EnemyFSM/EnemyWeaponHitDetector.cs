using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 敌人武器攻击检测。伤害由 EnemyAttackState 按技能配置的连击段传入。
/// </summary>
public class EnemyWeaponHitDetector : MonoBehaviour
{
    private float _currentDamage;
    private bool _hitWindowOpen;
    private EnemyVitals _enemyVitals;
    private HashSet<TestFSM> _hitTargets = new HashSet<TestFSM>();
    private Collider _weaponCollider;
    private HitEffectSpawner _hitEffectSpawner;   // ← 新增

    [Header("伤害倍率")]
    public float damageMultiplier = 1f;

    private void Awake()
    {
        _weaponCollider = GetComponent<Collider>();
        if (_weaponCollider != null)
        {
            _weaponCollider.isTrigger = true;
            _weaponCollider.enabled = false;
        }

        _enemyVitals = GetComponentInParent<EnemyVitals>();
        _hitEffectSpawner = GetComponent<HitEffectSpawner>();   // ← 新增
    }

    /// <summary>由 EnemyFSM.SetEnemyWeaponDamage 调用，设置当前连击段伤害</summary>
    public void SetCurrentDamage(float damage)
    {
        _currentDamage = damage;
    }

    public void OnEnemyHitWindowOpen()
    {
        _hitWindowOpen = true;
        _hitTargets.Clear();
        if (_weaponCollider != null)
            _weaponCollider.enabled = true;
    }

    public void OnEnemyHitWindowClose()
    {
        _hitWindowOpen = false;
        _hitTargets.Clear();
        if (_weaponCollider != null)
            _weaponCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_hitWindowOpen) return;

        TestFSM player = other.GetComponent<TestFSM>();
        if (player == null)
            player = other.GetComponentInParent<TestFSM>();
        if (player == null) return;

        if (_hitTargets.Contains(player)) return;
        _hitTargets.Add(player);

        // ← 新增：生成命中特效
        _hitEffectSpawner?.SpawnAtContact(other);

        float finalDamage = _currentDamage * damageMultiplier;
        if (_enemyVitals != null && _enemyVitals.RagePercent >= 1f)
            finalDamage *= 1.05f;
        player.TakeDamage(finalDamage, transform.root);
    }
}