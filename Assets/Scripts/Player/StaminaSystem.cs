using UnityEngine;

public class StaminaSystem : MonoBehaviour
{
    [Header("耐力配置")]
    public float maxStamina = 100f;
    public float currentStamina;
    
    [Header("恢复配置")]
    public float staminaRegenRate = 10f;
    public float staminaRegenDelay = 1f;
    
    [Header("消耗配置")]
    public float rollStaminaCost = 30f;
    public float sprintStaminaCost = 15f;
    
    public event System.Action OnStaminaChanged;
    public event System.Action OnStaminaDepleted;
    
    private float staminaRegenTimer;
    private bool isRegenerating;
    
    public bool HasEnoughStamina(float cost) => currentStamina >= cost;
    public float GetStaminaPercent() => currentStamina / maxStamina;
    
    private void Awake()
    {
        currentStamina = maxStamina;
    }
    
    private void Update()
    {
        if (staminaRegenTimer > 0f)
        {
            staminaRegenTimer -= Time.deltaTime;
        }
        else if (currentStamina < maxStamina)
        {
            RegenerateStamina();
        }
    }
    
    public bool TryUseStamina(float amount)
    {
        if (!HasEnoughStamina(amount))
        {
            return false;
        }
        
        currentStamina -= amount;
        staminaRegenTimer = staminaRegenDelay;
        
        OnStaminaChanged?.Invoke();
        
        if (currentStamina <= 0f)
        {
            OnStaminaDepleted?.Invoke();
        }
        
        return true;
    }
    
    public bool TryRoll()
    {
        return TryUseStamina(rollStaminaCost);
    }
    
    public bool TrySprint()
    {
        return TryUseStamina(sprintStaminaCost);
    }
    
    private void RegenerateStamina()
    {
        currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
        OnStaminaChanged?.Invoke();
    }
    
    public void RestoreFullStamina()
    {
        currentStamina = maxStamina;
        OnStaminaChanged?.Invoke();
    }
    
    public void SetMaxStamina(float newMaxStamina, bool restoreToFull = true)
    {
        maxStamina = newMaxStamina;
        if (restoreToFull)
        {
            currentStamina = maxStamina;
        }
        OnStaminaChanged?.Invoke();
    }
}
