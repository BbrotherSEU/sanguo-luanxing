using UnityEngine;

/// <summary>
/// 键位查询：所有输入都从这里走，玩家改键立刻生效。
/// 方向键始终可用（除了被玩家明确占用的键），这样改键后不至于完全动不了。
/// </summary>
public static class Bind
{
    static KeyCode Key(string action, KeyCode fallback)
    {
        var s = Settings.Current;
        return s != null ? s.Key(action, fallback) : fallback;
    }

    // 战斗
    public static KeyCode Attack => Key(SettingsUI.ActAttack, KeyCode.J);
    public static KeyCode Heavy => Key(SettingsUI.ActHeavy, KeyCode.K);
    public static KeyCode Skill1 => Key(SettingsUI.ActSkill1, KeyCode.Q);
    public static KeyCode Skill2 => Key(SettingsUI.ActSkill2, KeyCode.E);
    public static KeyCode Skill3 => Key(SettingsUI.ActSkill3, KeyCode.R);
    public static KeyCode Dash => Key(SettingsUI.ActDash, KeyCode.Space);
    public static KeyCode Interact => Key(SettingsUI.ActInteract, KeyCode.L);
    public static KeyCode Map => Key(SettingsUI.ActMap, KeyCode.M);
    public static KeyCode Panel => Key(SettingsUI.ActPanel, KeyCode.C);

    public static KeyCode MoveUp => Key(SettingsUI.ActMoveUp, KeyCode.W);
    public static KeyCode MoveDown => Key(SettingsUI.ActMoveDown, KeyCode.S);
    public static KeyCode MoveLeft => Key(SettingsUI.ActMoveLeft, KeyCode.A);
    public static KeyCode MoveRight => Key(SettingsUI.ActMoveRight, KeyCode.D);

    /// <summary>某个键是否被「非方向类」动作占用了。占用了就不能再当方向键的备用。</summary>
    static bool TakenByAction(KeyCode k)
    {
        return k == Attack || k == Heavy || k == Skill1 || k == Skill2 || k == Skill3
            || k == Dash || k == Interact || k == Map || k == Panel;
    }

    /// <summary>按下的键 = 玩家设定键，或没被占用的方向键。</summary>
    public static bool Down(string action, KeyCode fallback)
    {
        if (Input.GetKeyDown(Key(action, fallback))) return true;
        // 方向键：只要没被别的动作占用就照样能用
        KeyCode alt = AltFor(action);
        if (alt != KeyCode.None && !TakenByAction(alt) && Input.GetKeyDown(alt)) return true;
        return false;
    }

    public static bool Held(string action, KeyCode fallback)
    {
        if (Input.GetKey(Key(action, fallback))) return true;
        KeyCode alt = AltFor(action);
        if (alt != KeyCode.None && !TakenByAction(alt) && Input.GetKey(alt)) return true;
        return false;
    }

    /// <summary>动作对应的方向键备用键（玩家改键后，方向键仍能操作）。</summary>
    static KeyCode AltFor(string action)
    {
        if (action == SettingsUI.ActMoveLeft) return KeyCode.LeftArrow;
        if (action == SettingsUI.ActMoveRight) return KeyCode.RightArrow;
        if (action == SettingsUI.ActMoveUp) return KeyCode.UpArrow;
        if (action == SettingsUI.ActMoveDown) return KeyCode.DownArrow;
        return KeyCode.None;
    }
}
