using UnityEngine;

public class EnemyFSM_test : MonoBehaviour, ICombatTarget, ICombatant
{
    private EnemyFSM _fsm;
    private EnemyVitals _vitals;

    private void Awake()
    {
        _fsm = GetComponent<EnemyFSM>();
        _vitals = GetComponent<EnemyVitals>();
    }

    private void Start()
    {
        var ui = FindFirstObjectByType<EnemyVitalsUI_test>();
        if (ui != null && _vitals != null) ui.Bind(_vitals);
    }

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

    Transform ICombatant.Transform => transform;
    ActorVitals ICombatant.Vitals => _vitals;
}
