using UnityEngine;

/// <summary>
/// 薄适配器。挂到 Player 物体上（与原有 TestFSM 共存），
/// 为 WeaponHitDetector_test 等的 ICombatTarget / ICombatant 查找提供接口实现。
/// 不复制任何 FSM 逻辑，全部委托给原有的 TestFSM。
/// </summary>
public class TestFSM_test : MonoBehaviour, ICombatTarget, ICombatant
{
    private TestFSM _fsm;
    private PlayerVitals _vitals;

    private void Awake()
    {
        _fsm = GetComponent<TestFSM>();
        _vitals = GetComponent<PlayerVitals>();
    }

    // ===== ICombatTarget =====
    Transform ICombatTarget.Transform => transform;
    bool ICombatTarget.IsAlive => _vitals != null && !_vitals.IsDead;

    bool ICombatTarget.TakeHit(HitContext hit)
    {
        if (_fsm == null) return false;
        _fsm.TakeDamage(hit.Damage, hit.Attacker);
        return true;
    }

    // ===== ICombatant =====
    Transform ICombatant.Transform => transform;
    ActorVitals ICombatant.Vitals => _vitals;
}
