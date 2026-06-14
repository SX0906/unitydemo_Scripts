using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

/// <summary>
/// Animator State 完整路径调试器。
/// 运行时自动扫描 AnimatorController 内所有 State，
/// 并根据当前 AnimatorStateInfo.fullPathHash 匹配出当前 State 的完整路径。
/// </summary>
public class AnimatorStatePathDebugger : MonoBehaviour
{
    public Animator animator;
    public int layerIndex = 0;
    public bool printOnlyOnStateChanged = true;
    public bool includeNextStateWhenTransition = true;

    private int lastCurrentHash;
    private int lastNextHash;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (!animator)
            return;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layerIndex);
        int currentHash = current.fullPathHash;

        if (!printOnlyOnStateChanged || currentHash != lastCurrentHash)
        {
            lastCurrentHash = currentHash;
            PrintHashPath("Current", currentHash);
        }

        if (includeNextStateWhenTransition && animator.IsInTransition(layerIndex))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layerIndex);
            int nextHash = next.fullPathHash;

            if (!printOnlyOnStateChanged || nextHash != lastNextHash)
            {
                lastNextHash = nextHash;
                PrintHashPath("Next", nextHash);
            }
        }
    }

    private void PrintHashPath(string label, int hash)
    {
        string path = FindStatePathByHash(hash);

        if (string.IsNullOrEmpty(path))
        {
            Debug.Log($"[{name}] {label} State Hash={hash}, Path 未找到");
        }
        else
        {
            Debug.Log($"[{name}] {label} State Path = {path}");
        }
    }

    private string FindStatePathByHash(int hash)
    {
#if UNITY_EDITOR
        if (!animator || !animator.runtimeAnimatorController)
            return null;

        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;

        if (!controller)
        {
            Debug.LogWarning($"[{name}] 当前 AnimatorController 不是 AnimatorController，可能是 OverrideController。");
            return null;
        }

        if (layerIndex < 0 || layerIndex >= controller.layers.Length)
            return null;

        string layerName = controller.layers[layerIndex].name;
        AnimatorStateMachine rootStateMachine = controller.layers[layerIndex].stateMachine;

        return FindStatePathRecursive(rootStateMachine, layerName, hash);
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    private string FindStatePathRecursive(AnimatorStateMachine stateMachine, string currentPath, int hash)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            string path = currentPath + "." + childState.state.name;

            if (Animator.StringToHash(path) == hash)
                return path;
        }

        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
        {
            string path = currentPath + "." + childStateMachine.stateMachine.name;

            string result = FindStatePathRecursive(childStateMachine.stateMachine, path, hash);

            if (!string.IsNullOrEmpty(result))
                return result;
        }

        return null;
    }
#endif
}

