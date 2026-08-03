using UnityEngine;

public class EnemyAttackSkillPlayer_test : MonoBehaviour
{
    private IExecutionService _executionService;
    public void Initialize(IExecutionService s) { _executionService = s; }

    private void OnHitEnemy(GameObject hit)
    {
        if (_executionService != null && _executionService.CanExecute(hit))
            _executionService.TryStartExecution(gameObject, hit);
    }
}
