using UnityEngine;

/// <summary>
/// 角色战斗音效播放器
/// 动画事件：PlaySwingSound(int) → 指定下标播放挥刀音效
/// 代码调用：命中/弹刀 → 随机播放
/// 玩家/敌人通用
/// </summary>
public class CombatAudioPlayer : MonoBehaviour
{
    [Header("音效配置文件")]
    public CombatSFXConfig audioConfig;

    [Header("全局音量")]
    [Range(0f, 1f)] public float volume = 0.7f;

    [Header("播放间隔（防止音效重叠）")]
    public float minInterval = 0.06f;

    private AudioSource _audioSource;
    private float _lastPlayTime;

    private void Awake()
    {
        // 自动获取/添加音频组件
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        // 3D空间音效 设置
        _audioSource.spatialBlend = 1f;
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
    }

    #region 动画事件调用（核心：固定方法名 PlaySwingSound）
    /// <summary>
    /// 动画事件固定调用：PlaySwingSound
    /// 传入 int 参数 = 挥刀音效数组下标
    /// </summary>
    public void PlaySwingSound(int index)
    {
        // 基础校验
        if (!CanPlay() || audioConfig == null) return;
        // 下标越界判断
        if (index < 0 || index >= audioConfig.swingNormal.Length) return;

        // 获取指定下标的音效并播放
        AudioClip clip = audioConfig.swingNormal[index];
        if (clip == null) return;

        _audioSource.PlayOneShot(clip, volume);
        _lastPlayTime = Time.time;
    }
    #endregion

    public void PlayHeavySwingSound(int index)
    {
        if (!CanPlay() || audioConfig == null) return;
        if (index < 0 || index >= audioConfig.swingHeavy.Length) return;

        AudioClip clip = audioConfig.swingHeavy[index];
        if (clip == null) return;

        _audioSource.PlayOneShot(clip, volume);
        _lastPlayTime = Time.time;
    }

    #region 代码调用（命中 / 弹刀 随机播放）
    /// <summary>
    /// 攻击命中敌人时调用
    /// </summary>
    public void PlayHitSound()
    {
        PlayRandomClip(audioConfig?.hitEnemy);
    }

    /// <summary>
    /// 弹刀成功时调用
    /// </summary>
    public void PlayParrySound()
    {
        PlayRandomClip(audioConfig?.parrySuccess);
    }
    #endregion

    #region 通用工具
    // 随机播放音效
    private void PlayRandomClip(AudioClip[] clips)
    {
        if (!CanPlay() || clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        _audioSource.PlayOneShot(clip, volume);
        _lastPlayTime = Time.time;
    }

    // 检查是否允许播放
    private bool CanPlay()
    {
        return _audioSource != null && Time.time - _lastPlayTime >= minInterval;
    }
    #endregion
}