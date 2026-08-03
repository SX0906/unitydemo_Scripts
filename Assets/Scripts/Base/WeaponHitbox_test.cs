using UnityEngine;
using System;

public class WeaponHitbox_test : MonoBehaviour
{
    public event Action<GameObject> OnHitEnemy;
    public GameObject owner;
    public float damage = 20f;
    public bool ignoreInvincible;

    private IExecutionService _executionService;

    private void Awake()
    {
        _executionService = FindFirstObjectByType<ExecutionManager_test>(); // cast to interface via MonoBehaviour
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == owner) return;
        ICombatTarget target = other.GetComponentInParent<ICombatTarget>();
        if (target == null) return;

        if (_executionService != null && _executionService.CanExecute(other.gameObject))
        {
            _executionService.TryStartExecution(owner, other.gameObject);
            return;
        }

        target.TakeHit(new HitContext("F",
            (other.transform.position - transform.position).normalized,
            false, owner != null ? owner.transform : transform, damage, ignoreInvincible));
        OnHitEnemy?.Invoke(other.gameObject);
    }
}
