using System;

/// <summary>
/// 一種 NPC 的「種類」資料（NpcTable.csv 一列）。
/// 與 MonsterData 刻意分表：NPC 是劇情/功能角色，欄位少、語意不同（沒有攻擊/掉寶/說話門檻），
/// 混在 MonsterData 裡會讓兩邊都長出一堆互相無關的欄位。
/// 外觀沿用「角色序列圖庫」：GameAssets/(Main|Modules/&lt;關卡&gt;)/Monsters/SequenceImage/&lt;Name&gt;/idle·walk/
/// ——與怪物、劇情演出演員同一個圖庫、同一條同步管線（劇情演員早已這樣用，非怪物角色放這裡是既有慣例）。
/// </summary>
[Serializable]
public class NpcData
{
    public int ID;
    public string Name;             // 程式鍵＝序列圖資料夾名（Monsters/SequenceImage/<Name>/）
    public string DisplayName = ""; // 顯示名稱（互動提示、未來對話/血條用；留空＝退回 Name）
    public float Scale = 1f;        // 整體縮放（同 MonsterData.Scale：transform.localScale 倍率）
    public float AnimFPS = 8f;      // 程式逐格動畫幀率（留空＝8）
    public float Speed = 2f;        // 走動速度（留空＝2；編輯器每個擺放可覆寫）
    public float HP = 50f;          // 血量（留空＝50）。第一波 NPC 不會被打到；給未來護送任務預留

    /// <summary>顯示用名稱（DisplayName 留空退回 Name）。</summary>
    public string ShownName => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;
}
