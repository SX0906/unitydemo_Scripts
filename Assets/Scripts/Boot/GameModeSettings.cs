using UnityEngine;

/// <summary>
/// 主菜单和主场景共用的游戏模式设置。
/// </summary>
public static class GameModeSettings
{
    public const string PlayerPrefsKey = "GameMode";
    public const int Mode1V1 = 1;
    public const int Mode1V2 = 2;
    public const int Mode1V4 = 4;

    public static int CurrentMode => PlayerPrefs.GetInt(PlayerPrefsKey, Mode1V4);

    /// <summary>玩家造成伤害的倍率，1V1 为基准 1.0。</summary>
    public static float PlayerDamageMultiplier
    {
        get
        {
            if (CurrentMode == Mode1V2) return 1.2f;
            if (CurrentMode == Mode1V4) return 1.5f;
            return 1f;
        }
    }

    /// <summary>敌人造成伤害的倍率，1V1 为基准 1.0。</summary>
    public static float EnemyDamageMultiplier
    {
        get
        {
            if (CurrentMode == Mode1V2) return 0.8f;
            if (CurrentMode == Mode1V4) return 0.6f;
            return 1f;
        }
    }

    public static bool Is1V1 => CurrentMode == Mode1V1;
    public static bool Is1V2 => CurrentMode == Mode1V2;
    public static bool Is1V4 => CurrentMode == Mode1V4;

    public static void SetMode(int mode)
    {
        if (mode != Mode1V1 && mode != Mode1V2 && mode != Mode1V4)
            mode = Mode1V4;
        PlayerPrefs.SetInt(PlayerPrefsKey, mode);
    }
}
