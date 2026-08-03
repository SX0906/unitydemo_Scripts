using UnityEngine;
using UnityEngine.UI;

public class EnemyVitalsUI_test : MonoBehaviour
{
    public EnemyVitals enemyVitals;
    public Image healthFill;
    public GameObject healthGroup;
    public Image rageFill;
    public Image postureFillLeft;
    public Image postureFillRight;
    public GameObject postureGroup;

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
    private void OnTargetDeath() { if (postureGroup) postureGroup.SetActive(false); }
    public void Show(bool visible) { if (healthGroup) healthGroup.SetActive(visible); if (postureGroup) postureGroup.SetActive(visible); }
    private void UpdateHealth(float c, float m) { if (healthFill) healthFill.fillAmount = m > 0f ? c / m : 0f; }
    private void UpdateRage(float c, float m) { if (rageFill) rageFill.fillAmount = m > 0f ? c / m : 0f; }
    private void UpdatePosture(float c, float m) { float pct = m > 0f ? c / m : 0f; float half = pct * 0.5f; if (postureFillLeft) postureFillLeft.fillAmount = half; if (postureFillRight) postureFillRight.fillAmount = half; }
}
