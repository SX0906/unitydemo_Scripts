using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class HitStopManager_test
{
    private static MonoBehaviour _host;
    private static Coroutine _activeRoutine;
    private static float _originalTimeScale = 1f;
    private static HashSet<Animator> _frozenAnimators = new();

    public static void EnsureHost(MonoBehaviour host)
    {
        if (_host != null)
        {
            if (_host == host) return;
            bool oldAlive = false;
            try { oldAlive = _host.gameObject != null; } catch { }
            if (!oldAlive)
            {
                _host = host;
                return;
            }
            return;
        }
        _host = host;
    }

    public static void Reset()
    {
        if (_activeRoutine != null && _host != null)
        {
            try { _host.StopCoroutine(_activeRoutine); } catch { }
        }
        _activeRoutine = null;
        Time.timeScale = 1f;
        foreach (var anim in _frozenAnimators)
        {
            if (anim != null) anim.speed = 1f;
        }
        _frozenAnimators.Clear();
        _host = null;
    }

    public static void Request(float timeScale, float durationSeconds)
    {
        if (_host == null) return;
        if (_activeRoutine != null) return;
        _originalTimeScale = Time.timeScale;
        _activeRoutine = _host.StartCoroutine(Run(timeScale, durationSeconds));
    }

    public static void Cancel()
    {
        if (_activeRoutine != null && _host != null)
        {
            try { _host.StopCoroutine(_activeRoutine); } catch { }
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

    public static void FreezeAnimator(Animator animator, int frameCount)
    {
        if (_host == null || animator == null) return;
        if (_frozenAnimators.Contains(animator)) return;
        _host.StartCoroutine(FreezeAnimatorRoutine(animator, frameCount));
    }

    private static IEnumerator FreezeAnimatorRoutine(Animator animator, int frameCount)
    {
        _frozenAnimators.Add(animator);
        animator.speed = 0f;
        for (int i = 0; i < frameCount; i++)
            yield return null;
        _frozenAnimators.Remove(animator);
        if (animator != null) animator.speed = 1f;
    }
}
