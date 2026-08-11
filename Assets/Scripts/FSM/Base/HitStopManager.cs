using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局顿帧管理器。
/// </summary>
public static class HitStopManager
{
    private static MonoBehaviour _host;
    private static Coroutine _activeRoutine;
    private static float _originalTimeScale = 1f;
    private static bool _hostSet;

    /// <summary>初始化宿主（由第一个使用者调用）</summary>
    public static void EnsureHost(MonoBehaviour host)
    {
        if (host == null) return;

        if (_hostSet && _host != null)
            return;

        // 旧宿主已销毁时清掉残留状态，再绑定新宿主
        if (_hostSet)
            ClearStaleState();

        _host = host;
        _hostSet = true;
    }

    /// <summary>场景切换时重置，避免旧场景宿主残留。</summary>
    public static void Reset()
    {
        if (_activeRoutine != null && _host != null)
        {
            try { _host.StopCoroutine(_activeRoutine); } catch { }
        }

        ClearStaleState();
        _host = null;
        _hostSet = false;
    }

    /// <summary>
    /// 请求时间滞缓顿帧。如果已有顿帧在跑，直接忽略新请求。
    /// </summary>
    public static void Request(float timeScale, float durationSeconds)
    {
        if (_host == null)
        {
            Debug.LogWarning("[HitStopManager] 未初始化宿主，无法执行顿帧");
            return;
        }

        if (_activeRoutine != null)
            return;

        _originalTimeScale = Time.timeScale;
        _activeRoutine = _host.StartCoroutine(Run(timeScale, durationSeconds));
    }

    /// <summary>立刻取消当前顿帧并恢复时间缩放</summary>
    public static void Cancel()
    {
        if (_activeRoutine != null && _host != null)
        {
            _host.StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }
        Time.timeScale = _originalTimeScale;
    }

    private static IEnumerator Run(float timeScale, float durationSeconds)
    {
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(durationSeconds);
        Time.timeScale = _originalTimeScale;
        _activeRoutine = null;
    }

    // ========== 按帧冻结 Animator（不叠加版本） ==========

    private static HashSet<Animator> _frozenAnimators = new();

    /// <summary>
    /// 冻结指定 Animator N 帧。如果这个 Animator 已经在冻结中，忽略新请求。
    /// </summary>
    public static void FreezeAnimator(Animator animator, int frameCount)
    {
        if (_host == null || animator == null) return;

        // 已经在冻结中 → 不叠加，忽略
        if (_frozenAnimators.Contains(animator))
            return;

        _host.StartCoroutine(FreezeAnimatorRoutine(animator, frameCount));
    }

    private static IEnumerator FreezeAnimatorRoutine(Animator animator, int frameCount)
    {
        _frozenAnimators.Add(animator);
        animator.speed = 0f;

        for (int i = 0; i < frameCount; i++)
            yield return null;

        _frozenAnimators.Remove(animator);
        animator.speed = 1f;
    }

    private static void ClearStaleState()
    {
        _activeRoutine = null;
        _originalTimeScale = 1f;
        Time.timeScale = 1f;

        foreach (var anim in _frozenAnimators)
        {
            if (anim != null) anim.speed = 1f;
        }
        _frozenAnimators.Clear();
    }
}
