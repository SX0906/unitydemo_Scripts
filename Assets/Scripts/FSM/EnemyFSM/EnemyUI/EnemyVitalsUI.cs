using UnityEngine;
using UnityEngine.UI;

public class EnemyVitalsUI : MonoBehaviour
{
    [Header("数据源")]
    public EnemyVitals enemyVitals;

    [Header("血条")]
    public Image healthFill;
    public GameObject healthGroup;

    [Header("怒气条")]
    public Image rageFill;

    [Header("架势条（左右两段，从中间向两边涨）")]
    public Image postureFillLeft;
    public Image postureFillRight;
    public GameObject postureGroup;

    private void Start()
    {
        if (enemyVitals == null)
        {
            var fsm = FindFirstObjectByType<EnemyFSM>();
            if (fsm != null)
            {
                var vitals = fsm.GetComponent<EnemyVitals>();
                if (vitals != null)
                    Bind(vitals);
            }
        }
        else
        {
            Bind(enemyVitals);
        }
    }

    public void Bind(EnemyVitals vitals)
    {
        if (enemyVitals != null)
        {
            enemyVitals.OnHealthChanged -= UpdateHealth;
            enemyVitals.OnRageChanged -= UpdateRage;
            enemyVitals.OnPostureChanged -= UpdatePosture;
            enemyVitals.OnDeath -= OnTargetDeath;
        }

        enemyVitals = vitals;

        if (enemyVitals != null)
        {
            enemyVitals.OnHealthChanged += UpdateHealth;
            enemyVitals.OnRageChanged += UpdateRage;
            enemyVitals.OnPostureChanged += UpdatePosture;
            enemyVitals.OnDeath += OnTargetDeath;

            UpdateHealth(enemyVitals.currentHealth, enemyVitals.maxHealth);
            UpdateRage(enemyVitals.currentRage, enemyVitals.maxRage);
            UpdatePosture(enemyVitals.currentPosture, enemyVitals.maxPosture);

            Show(true);
        }
    }

    private void OnDestroy() { Bind(null); }

    private void OnTargetDeath()
    {
        if (postureGroup) postureGroup.SetActive(false);
    }

    public void Show(bool visible)
    {
        if (healthGroup) healthGroup.SetActive(visible);
        if (postureGroup) postureGroup.SetActive(visible);
    }

    private void UpdateHealth(float current, float max)
    {
        if (healthFill) healthFill.fillAmount = max > 0f ? current / max : 0f;
    }

    private void UpdateRage(float current, float max)
    {
        if (rageFill) rageFill.fillAmount = max > 0f ? current / max : 0f;
    }

    private void UpdatePosture(float current, float max)
    {
        float pct = max > 0f ? current / max : 0f;
        float half = pct * 0.5f;
        if (postureFillLeft)  postureFillLeft.fillAmount = half;
        if (postureFillRight) postureFillRight.fillAmount = half;
    }
}
