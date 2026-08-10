using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人战斗协调器：负责敌人间警报共享和攻击名额控制。
/// 无需挂到场景，由 EnemyFSM 在启用/销毁时注册或注销。
/// </summary>
public static class EnemyCombatCoordinator
{
    private static readonly HashSet<EnemyFSM> Registered = new();
    private static readonly HashSet<EnemyFSM> AttackHolders = new();

    /// <summary>最多同时攻击的敌人数量</summary>
    public static int MaxAttackers = 1;

    /// <summary>最后一次直接看到玩家后，共享警报保留的秒数</summary>
    public static float SharedAlertDuration = 5f;

    /// <summary>攻击名额释放后，下一个攻击者需要等待的秒数</summary>
    public static float AttackHandoffDelay = 0.5f;

    /// <summary>上一个技能禁止重复使用的秒数</summary>
    public static float SkillLockDuration = 1.5f;

    private static Transform sharedTarget;
    private static Vector3 sharedLastKnownPos;
    private static float sharedAlertEndTime;
    private static float attackReadyTime;
    private static string lastUsedSkillID;
    private static float skillLockEndTime;

    public static bool HasSharedTarget =>
        sharedTarget != null && Time.time < sharedAlertEndTime;

    public static bool AttackSlotsFull => AttackHolders.Count >= MaxAttackers;

    public static string LastUsedSkillID => lastUsedSkillID;

    public static void Register(EnemyFSM enemy)
    {
        if (enemy == null) return;
        Registered.Add(enemy);
    }

    public static void Unregister(EnemyFSM enemy)
    {
        if (enemy == null) return;
        Registered.Remove(enemy);
        ReleaseAttackSlot(enemy);
    }

    public static bool TryAcquireAttackSlot(EnemyFSM enemy)
    {
        if (enemy == null) return false;
        if (AttackHolders.Contains(enemy)) return true;
        if (Time.time < attackReadyTime) return false;
        if (AttackHolders.Count >= MaxAttackers) return false;
        AttackHolders.Add(enemy);
        return true;
    }

    public static void ReleaseAttackSlot(EnemyFSM enemy)
    {
        if (enemy == null) return;
        if (AttackHolders.Remove(enemy))
            attackReadyTime = Time.time + AttackHandoffDelay;
    }

    /// <summary>记录本次使用的技能，短时间内禁止其它敌人重复使用</summary>
    public static void NotifySkillUsed(string skillID)
    {
        if (string.IsNullOrEmpty(skillID)) return;
        lastUsedSkillID = skillID;
        skillLockEndTime = Time.time + SkillLockDuration;
    }

    /// <summary>该技能是否仍在禁止重复使用时间内</summary>
    public static bool IsSkillRecentlyUsed(string skillID)
    {
        return !string.IsNullOrEmpty(skillID)
            && skillID == lastUsedSkillID
            && Time.time < skillLockEndTime;
    }

    /// <summary>镜头外敌人放弃本次攻击时调用，立即让其它敌人可以竞争攻击名额</summary>
    public static void NotifyAttackDeclined()
    {
        attackReadyTime = Time.time;
    }

    /// <summary>判断敌人是否在玩家主相机视野内</summary>
    public static bool IsInCamera(Transform target)
    {
        Camera cam = Camera.main;
        if (cam == null || target == null) return true;

        Vector3 viewport = cam.WorldToViewportPoint(target.position + Vector3.up * 1f);
        return viewport.z > 0f
            && viewport.x >= 0f && viewport.x <= 1f
            && viewport.y >= 0f && viewport.y <= 1f;
    }

    /// <summary>
    /// 有敌人首次发现玩家时调用：记录共享目标，并通知范围内尚未发现玩家的敌人。
    /// </summary>
    public static void ReportPlayerSpotted(
        EnemyFSM source, Transform player, Vector3 position, float alertRadius)
    {
        if (source == null || player == null) return;

        RefreshSharedTarget(player, position);

        Vector3 sourcePos = source.transform.position;
        foreach (EnemyFSM enemy in Registered)
        {
            if (enemy == null || enemy == source) continue;
            if (enemy.hasTarget) continue;
            if (Vector3.Distance(sourcePos, enemy.transform.position) > alertRadius) continue;
            enemy.ReceiveSharedAlert(player, position);
        }
    }

    /// <summary>玩家持续被看到时，每帧刷新共享位置与过期时间</summary>
    public static void RefreshSharedTarget(Transform player, Vector3 position)
    {
        if (player == null) return;
        sharedTarget = player;
        sharedLastKnownPos = position;
        sharedAlertEndTime = Time.time + SharedAlertDuration;
    }

    /// <summary>获取仍有效的共享警报（队内还有人知道玩家位置）</summary>
    public static bool TryGetSharedAlert(out Transform target, out Vector3 position)
    {
        target = sharedTarget;
        position = sharedLastKnownPos;
        return HasSharedTarget;
    }
}
