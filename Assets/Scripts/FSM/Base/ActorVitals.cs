using UnityEngine;
using System;

/// <summary>
/// 角色属性基类——血量 + 怒气，挂在角色上。
/// 用事件通知 UI，不轮询。
/// </summary>
public class ActorVitals : MonoBehaviour
{
    [Header("生命值")]
    public float maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    public float currentHealth
    {
        get => _currentHealth;
        private set
        {
            float prev = _currentHealth;
            _currentHealth = Mathf.Clamp(value, 0f, maxHealth);
            if (!Mathf.Approximately(prev, _currentHealth))
                OnHealthChanged?.Invoke(_currentHealth, maxHealth);
            if (_currentHealth <= 0f && prev > 0f)
            {
                OnDeath?.Invoke();
                Die();
            }
        }
    }

    [Header("怒气值")]
    public float maxRage = 100f;
    [SerializeField] private float _currentRage;
    public float currentRage
    {
        get => _currentRage;
        private set
        {
            float prev = _currentRage;
            _currentRage = Mathf.Clamp(value, 0f, maxRage);
            if (!Mathf.Approximately(prev, _currentRage))
                OnRageChanged?.Invoke(_currentRage, maxRage);
        }
    }

    [Header("怒气获取")]
    public float rageGainOnHit = 5f;
    public float rageGainOnKill = 30f;

    [Header("无敌")]
    public bool isInvincible = false;

    // === 事件 ===
    public event Action<float, float> OnHealthChanged;   // (current, max)
    public event Action<float, float> OnRageChanged;     // (current, max)
    public event Action<float> OnDamaged;                // (damageAmount)
    public event Action OnDeath;

    public bool IsDead => _currentHealth <= 0f;
    public float HealthPercent => maxHealth > 0f ? _currentHealth / maxHealth : 0f;
    public float RagePercent => maxRage > 0f ? _currentRage / maxRage : 0f;

    protected virtual void Awake()
    {
        _currentHealth = maxHealth;
        _currentRage = 0f;
    }

    /// <summary>受到伤害</summary>
    public virtual void TakeDamage(float damage)
    {
        if (IsDead || isInvincible) return;

        currentHealth -= damage;
        OnDamaged?.Invoke(damage);
    }

    /// <summary>回复生命</summary>
    public virtual void Heal(float amount)
    {
        if (IsDead) return;
        currentHealth += amount;
    }

    /// <summary>增加怒气</summary>
    public void GainRage(float amount)
    {
        if (IsDead) return;
        currentRage += amount;
    }

    /// <summary>消耗怒气，返回是否足够</summary>
    public bool ConsumeRage(float amount)
    {
        if (currentRage < amount) return false;
        currentRage -= amount;
        return true;
    }

    /// <summary>子类可重写死亡行为</summary>
    protected virtual void Die() { }

    /// <summary>外部可调：收到命中时加怒气</summary>
    public void OnHitReceived()
    {
        GainRage(rageGainOnHit);
    }

    /// <summary>外部可调：击杀目标时加怒气</summary>
    public void OnKill()
    {
        GainRage(rageGainOnKill);
    }
}