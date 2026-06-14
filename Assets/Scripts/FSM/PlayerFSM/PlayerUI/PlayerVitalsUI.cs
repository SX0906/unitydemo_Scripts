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
            playerVitals.OnHealthChanged += UpdateHealthBar;
            playerVitals.OnStaminaChanged += UpdateStaminaBar;
            playerVitals.OnRageChanged += UpdateRageBar;

            UpdateHealthBar(playerVitals.currentHealth, playerVitals.maxHealth);
            if (staminaFill) staminaFill.fillAmount = playerVitals.StaminaPercent;
            if (rageFill) rageFill.fillAmount = playerVitals.RagePercent;
        }
    }

    private void OnDestroy()
    {
        if (playerVitals != null)
        {
            playerVitals.OnHealthChanged -= UpdateHealthBar;
            playerVitals.OnStaminaChanged -= UpdateStaminaBar;
            playerVitals.OnRageChanged -= UpdateRageBar;
        }
    }

    private void UpdateHealthBar(float current, float max)
    {
        if (healthFill) healthFill.fillAmount = max > 0 ? current / max : 0;
    }

    private void UpdateStaminaBar(float current, float max)
    {
        if (staminaFill) staminaFill.fillAmount = max > 0 ? current / max : 0;
    }

    private void UpdateRageBar(float current, float max)
    {
        if (rageFill) rageFill.fillAmount = max > 0 ? current / max : 0;
    }
}
