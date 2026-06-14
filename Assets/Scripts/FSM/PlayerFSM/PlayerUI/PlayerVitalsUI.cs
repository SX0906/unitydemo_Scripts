using UnityEngine;
using UnityEngine.UI;

public class PlayerVitalsUI : MonoBehaviour
{
    [Header("数据源")]
    public PlayerVitals playerVitals;

    [Header("血条")]  public Image healthFill;
    [Header("体力条")] public Image staminaFill;
    [Header("怒气条")] public Image rageFill;

    private void Start()
    {
        if (playerVitals == null)
            playerVitals = FindFirstObjectByType<PlayerVitals>();

        if (playerVitals != null)
        {
            playerVitals.OnHealthChanged += UpdateBar;
            playerVitals.OnStaminaChanged += (c, m) => { if (staminaFill) staminaFill.fillAmount = m > 0 ? c / m : 0; };
            playerVitals.OnRageChanged += (c, m) => { if (rageFill) rageFill.fillAmount = m > 0 ? c / m : 0; };
            UpdateBar(playerVitals.currentHealth, playerVitals.maxHealth);
            if (staminaFill) staminaFill.fillAmount = playerVitals.StaminaPercent;
            if (rageFill) rageFill.fillAmount = playerVitals.RagePercent;
        }
    }

    private void OnDestroy()
    {
        if (playerVitals != null)
        {
            playerVitals.OnHealthChanged -= UpdateBar;
            playerVitals.OnStaminaChanged -= null;
            playerVitals.OnRageChanged -= null;
        }
    }

    private void UpdateBar(float current, float max)
    {
        if (healthFill) healthFill.fillAmount = max > 0 ? current / max : 0;
    }
}