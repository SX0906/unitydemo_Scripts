using UnityEngine;

public class TestFSM_test : MonoBehaviour, ICombatTarget, ICombatant
{
    private TestFSM _fsm;
    private PlayerVitals _vitals;

    private void Awake()
    {
        _fsm = GetComponent<TestFSM>();
        _vitals = GetComponent<PlayerVitals>();
    }

    private void Start()
    {
        var ui = FindFirstObjectByType<PlayerVitalsUI_test>();
        if (ui != null && _vitals != null) ui.Bind(_vitals);
    }

    Transform ICombatTarget.Transform => transform;
    bool ICombatTarget.IsAlive => _vitals != null && !_vitals.IsDead;

    bool ICombatTarget.TakeHit(HitContext hit)
    {
        if (_fsm == null) return false;
        _fsm.TakeDamage(hit.Damage, hit.Attacker);
        return true;
    }

    Transform ICombatant.Transform => transform;
    ActorVitals ICombatant.Vitals => _vitals;
}
