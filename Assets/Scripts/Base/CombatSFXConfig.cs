using UnityEngine;

[CreateAssetMenu(fileName = "CombatSFXConfig", menuName = "Audio/战斗音效配置")]
public class CombatSFXConfig : ScriptableObject
{
    [Header("挥刀音效（动画事件调用）")]
    public AudioClip[] swingNormal;
    public AudioClip[] swingHeavy;

    [Header("命中音效（攻击命中自动播放）")]
    public AudioClip[] hitEnemy;

    [Header("弹刀音效（弹刀自动播放）")]
    public AudioClip[] parrySuccess;
}