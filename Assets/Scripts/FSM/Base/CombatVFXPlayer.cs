using UnityEngine;

/// <summary>
/// 角色战斗特效播放器（挂在玩家/敌人根物体上）
/// 动画事件调用：PlaySlashVFX(int) → 指定下标生成挥刀特效
/// 模仿 CombatAudioPlayer 的模式，通过 AnimationEvent 驱动
/// </summary>
public class CombatVFXPlayer : MonoBehaviour
{
    [Header("攻击特效配置")]
    [Tooltip("每个攻击动画事件对应的特效、位置、旋转和缩放")]
    public AttackVFXConfig attackVFXConfig;

    [Header("挥刀特效预制件列表")]
    [Tooltip("把 Sword Slash VFX/Prefabs/ 里的预制件拖入此数组")]
    public GameObject[] slashVFXPrefabs;

    [Header("生成位置")]
    [Tooltip("武器上的空子物体，标记特效生成点和朝向（留空则用自身Transform）")]
    public Transform spawnPoint;

    private void Awake()
    {
        if (spawnPoint == null)
            spawnPoint = transform;
    }

    /// <summary>
    /// 动画事件调用：PlaySlashVFX
    /// 参数 int index = slashVFXPrefabs 数组下标
    /// </summary>
    public void PlaySlashVFX(int index)
    {
        if (slashVFXPrefabs == null || slashVFXPrefabs.Length == 0)
        {
            Debug.LogWarning("[CombatVFXPlayer] slashVFXPrefabs 数组为空，请在 Inspector 中拖入挥刀特效预制件");
            return;
        }

        if (index < 0 || index >= slashVFXPrefabs.Length)
        {
            Debug.LogWarning($"[CombatVFXPlayer] 下标 {index} 越界，数组长度 {slashVFXPrefabs.Length}");
            return;
        }

        GameObject prefab = slashVFXPrefabs[index];
        if (prefab == null)
        {
            Debug.LogWarning($"[CombatVFXPlayer] 下标 {index} 的预制件为空");
            return;
        }

        PlayConfiguredVFX(prefab, Vector3.zero, Vector3.zero, Vector3.one, true, 0f);
    }

    /// <summary>
    /// 动画事件调用：PlayAttackVFX
    /// 参数 string eventKey = AttackVFXConfig.entries 里的事件名
    /// </summary>
    public void PlayAttackVFX(string eventKey)
    {
        if (attackVFXConfig == null)
        {
            Debug.LogWarning("[CombatVFXPlayer] attackVFXConfig 为空，请在 Inspector 中拖入 AttackVFXConfig");
            return;
        }

        if (!attackVFXConfig.TryGetEntry(eventKey, out AttackVFXEntry entry))
        {
            Debug.LogWarning($"[CombatVFXPlayer] 找不到攻击特效配置：{eventKey}");
            return;
        }

        PlayAttackVFX(entry);
    }

    public void PlayAttackVFX(int index)
    {
        if (attackVFXConfig == null || attackVFXConfig.entries == null)
        {
            Debug.LogWarning("[CombatVFXPlayer] attackVFXConfig 为空，请在 Inspector 中拖入 AttackVFXConfig");
            return;
        }

        if (index < 0 || index >= attackVFXConfig.entries.Length)
        {
            Debug.LogWarning($"[CombatVFXPlayer] 攻击特效下标 {index} 越界");
            return;
        }

        PlayAttackVFX(attackVFXConfig.entries[index]);
    }

    private void PlayAttackVFX(AttackVFXEntry entry)
    {
        if (entry == null)
            return;

        PlayConfiguredVFX(
            entry.prefab,
            entry.localPosition,
            entry.localEulerAngles,
            entry.localScale,
            entry.parentToSpawnPoint,
            entry.lifetime);
    }

    /// <summary>
    /// 生成带本地偏移的攻击特效。
    /// localPosition/localEulerAngles/localScale 都相对 spawnPoint。
    /// </summary>
    public GameObject PlayConfiguredVFX(
        GameObject prefab,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale,
        bool parentToSpawnPoint,
        float lifetime)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[CombatVFXPlayer] VFX prefab 为空，无法生成特效");
            return null;
        }

        Transform anchor = spawnPoint != null ? spawnPoint : transform;
        Quaternion localRotation = Quaternion.Euler(localEulerAngles);
        GameObject instance;

        if (parentToSpawnPoint)
        {
            instance = Instantiate(prefab, anchor);
            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = localPosition;
            instanceTransform.localRotation = localRotation;
            instanceTransform.localScale = localScale;
        }
        else
        {
            Vector3 worldPosition = anchor.TransformPoint(localPosition);
            Quaternion worldRotation = anchor.rotation * localRotation;
            instance = Instantiate(prefab, worldPosition, worldRotation);
            instance.transform.localScale = localScale;
        }

        if (lifetime > 0f)
            Destroy(instance, lifetime);

        return instance;
    }
}
