using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [Header("血量配置")]
    public float maxHealth = 100f;
    public float currentHealth;
    
    [Header("无敌设置")]
    public bool isInvincible = false;
    public float invincibleTime = 0.2f;
    
    [Header("受伤时是否受击反应")]
    public bool enableHitReaction = true;
    
    public event System.Action OnHealthChanged;
    public event System.Action OnDeath;
    public event System.Action<float> OnDamaged;
    
    private float invincibleTimer;
    
    public bool IsDead => currentHealth <= 0f;
    
    private void Awake()
    {
        currentHealth = maxHealth;
    }
    
    private void Update()
    {
        if (invincibleTimer > 0f)
        {
            invincibleTimer -= Time.deltaTime;
            if (invincibleTimer <= 0f)
            {
                isInvincible = false;
            }
        }
    }
    
    public void TakeDamage(float damage)
    {
        if (IsDead || isInvincible) return;
        
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        
        OnDamaged?.Invoke(damage);
        OnHealthChanged?.Invoke();
        
        if (currentHealth <= 0f)
        {
            Die();
        }
    }
    
    public void Heal(float amount)
    {
        if (IsDead) return;
        
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke();
    }
    
    public void SetMaxHealth(float newMaxHealth, bool healToFull = true)
    {
        maxHealth = newMaxHealth;
        if (healToFull)
        {
            currentHealth = maxHealth;
        }
        OnHealthChanged?.Invoke();
    }
    
    public void SetInvincible(float duration)
    {
        isInvincible = true;
        invincibleTimer = duration;
    }
    
    private void Die()
    {
        OnDeath?.Invoke();
    }
    
    public float GetHealthPercent()
    {
        return currentHealth / maxHealth;
    }
}
