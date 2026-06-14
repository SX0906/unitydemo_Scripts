using UnityEngine;
using System;

public class EnemyVitals : ActorVitals
{
    [Header("架势条")]
    public float maxPosture = 80f;
    [SerializeField] private float _currentPosture;
    public float currentPosture
    {
        get => _currentPosture;
        private set
        {
            float prev = _currentPosture;
            _currentPosture = Mathf.Clamp(value, 0f, maxPosture);
            if (!Mathf.Approximately(prev, _currentPosture))
                OnPostureChanged?.Invoke(_currentPosture, maxPosture);

            // ★ 架势攒满了 → 爆
            if (_currentPosture >= maxPosture && prev < maxPosture)
                OnPostureFull?.Invoke();
        }
    }

    [Header("架势获取")]
    [Tooltip("每次格挡攒多少架势")]
    public float postureGainPerBlock = 15f;
    [Tooltip("架势被打空后延迟多久归零")]
    public float postureBreakDelay = 0f;

    public event Action<float, float> OnPostureChanged;   // (current, max)
    public event Action OnPostureFull;                     // ★ 架势满了

    public float PosturePercent => maxPosture > 0f ? _currentPosture / maxPosture : 0f;

    protected override void Awake()
    {
        base.Awake();
        _currentPosture = 0f;   // ★ 开局为空
    }

    /// <summary>格挡成功时调用，攒架势</summary>
    public void GainPostureOnBlock(float amount)
    {
        if (IsDead) return;

        float gain = amount > 0f ? amount : postureGainPerBlock;
        currentPosture += gain;
    }

    /// <summary>架势打满后归零（由 BLOCKBREAK 退出时调用）</summary>
    public void ResetPosture()
    {
        _currentPosture = 0f;
        OnPostureChanged?.Invoke(_currentPosture, maxPosture);
    }
}