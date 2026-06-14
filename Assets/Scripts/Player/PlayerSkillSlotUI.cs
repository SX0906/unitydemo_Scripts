using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerSkillSlotUI : MonoBehaviour
{
    public enum CooldownMode
    {
        DimmedOnly,      // 只将图标变暗
        DirectIconFill,  // 直接用技能图标做时钟冷却（推荐）
        FilledOverlay    // 使用额外遮罩
    }
    
    [Header("冷却显示模式")]
    public CooldownMode cooldownMode = CooldownMode.DirectIconFill;
    
    [Header("技能图标")]
    public Image skillIcon;
    
    [Header("冷却遮罩 (仅在FilledOverlay模式下使用)")]
    public Image cooldownOverlay;
    
    [Header("冷却时间文本")]
    public Text cooldownText;
    public TextMeshProUGUI cooldownTextTMP;
    
    [Header("按键提示文本")]
    public Text keyHintText;
    public TextMeshProUGUI keyHintTextTMP;
    
    [Header("技能准备好时的颜色")]
    public Color readyColor = Color.white;
    
    [Header("技能冷却中的颜色")]
    public Color cooldownColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    
    private PlayerSkillData skillData;
    private PlayerSkillPlayer skillPlayer;
    private int skillIndex;
    private float maxCooldown;
    private Image.Type originalImageType;
    private Image.FillMethod originalFillMethod;
    private Image.Origin360 originalFillOrigin;
    private bool originalClockwise;
    
    public void Initialize(PlayerSkillPlayer player, int index, PlayerSkillData skill, char key)
    {
        skillPlayer = player;
        skillIndex = index;
        skillData = skill;
        maxCooldown = skill != null ? skill.coolDown : 0f;
        
        if (skillIcon != null && skill != null && skill.skillIcon != null)
        {
            skillIcon.sprite = skill.skillIcon;
            SaveOriginalSettings();
        }
        
        string keyStr = key.ToString();
        if (keyHintText != null)
        {
            keyHintText.text = keyStr;
        }
        if (keyHintTextTMP != null)
        {
            keyHintTextTMP.text = keyStr;
        }
        
        UpdateSlot(true);
    }
    
    private void SaveOriginalSettings()
    {
        if (skillIcon != null)
        {
            originalImageType = skillIcon.type;
            originalFillMethod = skillIcon.fillMethod;
            originalFillOrigin = (Image.Origin360)skillIcon.fillOrigin;
            originalClockwise = skillIcon.fillClockwise;
        }
    }
    
    private void ApplyClockFillSettings()
    {
        if (skillIcon != null)
        {
            skillIcon.type = Image.Type.Filled;
            skillIcon.fillMethod = Image.FillMethod.Radial360;
            skillIcon.fillOrigin = (int)Image.Origin360.Top;
            skillIcon.fillClockwise = true;
        }
    }
    
    private void RestoreOriginalSettings()
    {
        if (skillIcon != null)
        {
            skillIcon.type = originalImageType;
            skillIcon.fillMethod = originalFillMethod;
            skillIcon.fillOrigin = (int)originalFillOrigin;
            skillIcon.fillClockwise = originalClockwise;
        }
    }
    
    private void Update()
    {
        if (skillPlayer != null)
        {
            UpdateSlot(false);
        }
    }
    
    private void UpdateSlot(bool forceRefresh)
    {
        if (skillPlayer == null || skillData == null)
            return;
        
        float cooldownRemaining = skillPlayer.GetSkillCooldownRemaining(skillIndex);
        bool isReady = cooldownRemaining <= 0f;
        string cooldownStr = isReady ? string.Empty : Mathf.CeilToInt(cooldownRemaining).ToString();
        
        // 冷却进度（1 = 刚开始，0 = 冷却结束）
        float cooldownProgress = maxCooldown > 0 ? cooldownRemaining / maxCooldown : 0f;
        
        switch (cooldownMode)
        {
            case CooldownMode.DirectIconFill:
                if (isReady)
                {
                    RestoreOriginalSettings();
                    skillIcon.fillAmount = 1f;
                    skillIcon.color = readyColor;
                }
                else
                {
                    ApplyClockFillSettings();
                    skillIcon.fillAmount = 1f - cooldownProgress;  // 反向填充
                    skillIcon.color = cooldownColor;
                }
                break;
                
            case CooldownMode.DimmedOnly:
                skillIcon.color = isReady ? readyColor : cooldownColor;
                break;
                
            case CooldownMode.FilledOverlay:
                if (cooldownOverlay != null)
                {
                    cooldownOverlay.fillAmount = isReady ? 0f : cooldownProgress;
                }
                skillIcon.color = readyColor;
                break;
        }
        
        if (cooldownText != null)
        {
            cooldownText.text = cooldownStr;
            cooldownText.enabled = !isReady;
        }
        if (cooldownTextTMP != null)
        {
            cooldownTextTMP.text = cooldownStr;
            cooldownTextTMP.enabled = !isReady;
        }
    }
}
