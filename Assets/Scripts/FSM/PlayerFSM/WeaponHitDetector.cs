using UnityEngine;
using System.Collections.Generic;

public class WeaponHitDetector : MonoBehaviour
{
    private string _currentHitDirTag;
    private HashSet<EnemyFSM> _hitTargets = new HashSet<EnemyFSM>();
    private Collider _weaponCollider;
    private ActorVitals _playerVitals;

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

        enemy.TakeDamage(finalDirTag, dir, isLauncher, playerTransform, damage);

        // 击中敌人 → 玩家获得怒气
        if (_playerVitals != null)
        {
            _playerVitals.GainRage(rageGainPerHit);
        }
    }
}
