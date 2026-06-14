using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusUI : MonoBehaviour
{
    [Header("血量条")]
    public Slider healthSlider;
    public Image healthFillImage;
    public Color healthFullColor = Color.green;
    public Color healthEmptyColor = Color.red;

    [Header("耐力条")]
    public Slider staminaSlider;
    public Image staminaFillImage;
    public Color staminaFullColor = Color.cyan;
    public Color staminaEmptyColor = Color.gray;

    [Header("引用")]
    public HealthSystem healthSystem;
    public StaminaSystem staminaSystem;

    private void Start()
    {
        if (healthSystem == null)
        {
            healthSystem = GetComponentInParent<HealthSystem>();
        }

        if (staminaSystem == null)
        {
            staminaSystem = GetComponentInParent<StaminaSystem>();
        }

        // 初始化UI
        UpdateHealthUI();
        UpdateStaminaUI();

        // 订阅事件
        if (healthSystem != null)
        {
            healthSystem.OnHealthChanged += UpdateHealthUI;
        }

        if (staminaSystem != null)
        {
            staminaSystem.OnStaminaChanged += UpdateStaminaUI;
        }
    }

    private void OnDestroy()
    {
        if (healthSystem != null)
        {
            healthSystem.OnHealthChanged -= UpdateHealthUI;
        }

        if (staminaSystem != null)
        {
            staminaSystem.OnStaminaChanged -= UpdateStaminaUI;
        }
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null && healthSystem != null)
        {
            float healthPercent = healthSystem.GetHealthPercent();
            healthSlider.value = healthPercent;

        }
    }

    private void UpdateStaminaUI()
    {
        if (staminaSlider != null && staminaSystem != null)
        {
            float staminaPercent = staminaSystem.GetStaminaPercent();
            staminaSlider.value = staminaPercent;
           
        }
    }
}
