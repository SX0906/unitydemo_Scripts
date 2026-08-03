using UnityEngine;

/// <summary>
/// 薄适配器。挂到 Enemy 物体上（与原有 EnemyFSM 共存），
/// 为 EnemyWeaponHitDetector_test 等的 ICombatTarget / ICombatant 查找提供接口实现。
/// 不复制任何 FSM 逻辑，全部委托给原有的 EnemyFSM。
/// </summary>
public class EnemyFSM_test : MonoBehaviour, ICombatTarget, ICombatant
{
    private EnemyFSM _fsm;
    private EnemyVitals _vitals;

    private void Awake()
    {
        _fsm = GetComponent<EnemyFSM>();
        _vitals = GetComponent<EnemyVitals>();
    }

    // ===== ICombatTarget =====
    Transform ICombatTarget.Transform => transform;
    bool ICombatTarget.IsAlive => _vitals != null && !_vitals.IsDead;

    bool ICombatTarget.TakeHit(HitContext hit)
    {
        if (_fsm == null || _vitals == null || _vitals.IsDead) return false;

        Vector3 dir = hit.Direction;
        if (dir == Vector3.zero && hit.Attacker != null)
            dir = (transform.position - hit.Attacker.position).normalized;

        return _fsm.TakeDamage(hit.DirTag, dir, hit.IsLauncher, hit.Attacker, hit.Damage, hit.IgnoreBlock);
    }

    // ===== ICombatant =====
    Transform ICombatant.Transform => transform;
    ActorVitals ICombatant.Vitals => _vitals;
}
