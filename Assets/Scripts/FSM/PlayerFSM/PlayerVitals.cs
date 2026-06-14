using UnityEngine;
using System;

/// <summary>
/// 玩家专属属性——体力系统。
/// </summary>
public class PlayerVitals : ActorVitals
{
    [Header("体力")]
    public float maxStamina = 100f;
    [SerializeField] private float _currentStamina;
    public float currentStamina
    {
        get => _currentStamina;
        private set
        {
            float prev = _currentStamina;
            _currentStamina = Mathf.Clamp(value, 0f, maxStamina);
            if (!Mathf.Approximately(prev, _currentStamina))
                OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
        }
    }

    [Header("体力恢复")]
    public float staminaRegenRate = 20f;     // 每秒回复量
    public float staminaRegenDelay = 0.5f;   // 消耗后延迟多少秒开始回复

    public event Action<float, float> OnStaminaChanged;

    private float staminaRegenTimer;

    public float StaminaPercent => maxStamina > 0f ? _currentStamina / maxStamina : 0f;

    protected override void Awake()
    {
        base.Awake();
        _currentStamina = maxStamina;
    }

    private void Update()
    {
        // 体力自动回复
        if (currentStamina < maxStamina)
        {
            staminaRegenTimer -= Time.deltaTime;
            if (staminaRegenTimer <= 0f)
                currentStamina += staminaRegenRate * Time.deltaTime;
        }
    }

    /// <summary>消耗体力，返回是否足够</summary>
    public bool UseStamina(float amount)
    {
        if (currentStamina < amount) return false;
        currentStamina -= amount;
        staminaRegenTimer = staminaRegenDelay;  // 重置回复延迟
        return true;
    }

    /// <summary>直接扣除体力（不检查是否足够）</summary>
    public void DrainStamina(float amount)
    {
        currentStamina -= amount;
        staminaRegenTimer = staminaRegenDelay;
    }
}