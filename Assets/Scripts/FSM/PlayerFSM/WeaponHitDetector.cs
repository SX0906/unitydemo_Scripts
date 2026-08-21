using UnityEngine;
using System.Collections.Generic;

public class WeaponHitDetector : MonoBehaviour
{
    private string _currentHitDirTag;
    private HashSet<EnemyFSM> _hitTargets = new HashSet<EnemyFSM>();
    private Collider _weaponCollider;
    private ActorVitals _playerVitals;
    private HitEffectSpawner _hitEffectSpawner;   // ← 新增

    [Header("伤害")]
    public float damage = 10f;

    [Header("怒气获取")]
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

        _playerVitals = GetComponentInParent<ActorVitals>();
        _hitEffectSpawner = GetComponent<HitEffectSpawner>();   // ← 新增
    }

    /// <summary>覆盖本次攻击的伤害值，由 TestFSM 按当前攻击状态设置</summary>
    public void SetDamage(float value)
    {
        damage = value;
    }

    public void OnHitWindowOpen(string dirTag)
    {
        _currentHitDirTag = dirTag;
        _hitTargets.Clear();
        if (_weaponCollider != null)
            _weaponCollider.enabled = true;
    }

    public void OnHitWindowClose()
    {
        _currentHitDirTag = string.Empty;
        _hitTargets.Clear();
        if (_weaponCollider != null)
            _weaponCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (string.IsNullOrEmpty(_currentHitDirTag)) return;

        EnemyFSM enemy = other.GetComponent<EnemyFSM>();
        if (enemy == null) return;

        if (_hitTargets.Contains(enemy)) return;
        _hitTargets.Add(enemy);

        // ← 新增：生成命中特效
        _hitEffectSpawner?.SpawnAtContact(other);

        Vector3 dir = enemy.transform.position - transform.position;
        dir.y = 0;
        if (dir.magnitude < 0.01f) dir = transform.root.forward;

        Transform playerTransform = transform.root;

        Vector3 toAttacker = playerTransform.position - enemy.transform.position;
        toAttacker.y = 0;
        float backAngle = toAttacker.magnitude > 0.01f ? Vector3.Angle(enemy.transform.forward, toAttacker) : 0f;
        bool isBackHit = backAngle >= BackHitAngleThreshold;
        string finalDirTag = isBackHit ? "B" : _currentHitDirTag;

        Animator playerAnimator = playerTransform.GetComponentInChildren<Animator>();
        bool isLauncher = false;
        if (playerAnimator != null)
        {
            AnimatorStateInfo state = playerAnimator.GetCurrentAnimatorStateInfo(0);
            isLauncher = state.IsName("Attack_Up_Floor_To_Air")
                    || state.IsName("Attack_Up_Air_To_Air");
        }

        bool damaged = enemy.TakeDamage(finalDirTag, dir, isLauncher, playerTransform, damage);

        if (damaged)
        {
            EnemyVitals enemyVitals = enemy.GetComponent<EnemyVitals>();
            if (enemyVitals != null && enemyVitals.IsDead)
            {
                _playerVitals?.OnKill();
            }

            if (_playerVitals != null)
            {
                _playerVitals.GainRage(rageGainPerHit);
            }
        }
    }
}
