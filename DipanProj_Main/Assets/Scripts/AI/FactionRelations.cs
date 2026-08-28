/// <summary>
/// 「A 陣營能不能傷害 B 陣營」的**單一真相**。接觸傷害、友軍找目標都查這裡；
/// 玩家子彈打誰目前仍由 Layer 決定（子彈打 Enemy 層；Ally/Neutral 都在 Ally 層＝打不到）。
///
/// 現況（第一波）寫死三條規則：
///  ‧ 同陣營不互傷。
///  ‧ Neutral（NPC）不傷人、也不被傷。
///  ‧ 其餘（Enemy ↔ PlayerAlly）互為敵對。
///
/// 🔮 未來多方陣營（玩家/狼人/血族三方互打、玩家中途變陣營）：把這兩個函式改成查
/// 「陣營關係表」（CSV 或程式表：faction × faction → 敵對/友好），呼叫端一行都不用動。
/// 屆時玩家自己的陣營歸屬掛在存檔、子彈命中改成也查這裡（而不是只靠 Layer）。
/// </summary>
public static class FactionRelations
{
    /// <summary>a 能不能傷害 b（接觸傷害、怪打怪、友軍選目標都用這條）。</summary>
    public static bool Hostile(MonsterFaction a, MonsterFaction b)
    {
        if (a == b) return false;
        if (a == MonsterFaction.Neutral || b == MonsterFaction.Neutral) return false;
        return true;   // Enemy ↔ PlayerAlly
    }

    /// <summary>這個陣營會不會攻擊「玩家本人」。</summary>
    public static bool AttacksPlayer(MonsterFaction a) => a == MonsterFaction.Enemy;
}
