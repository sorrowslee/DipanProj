using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DipanMapEditor.Data
{
    /// <summary>
    /// Trigger 類型定義集合，對應 triggerTypes.json（資料驅動、編輯器內可增刪）。
    /// 新增一種 trigger 筆刷 = 加一筆 TriggerTypeDef（含參數 schema）。
    /// 出生點（玩家/怪物）也是這裡的一種類型。
    /// </summary>
    public class TriggerTypeSet
    {
        public List<TriggerTypeDef> types = new List<TriggerTypeDef>();

        public TriggerTypeDef Find(string typeId) => types.Find(t => t.typeId == typeId);

        /// <summary>內建預設類型（首次無檔時生成）。</summary>
        public static TriggerTypeSet Defaults()
        {
            var set = new TriggerTypeSet();
            set.types.Add(new TriggerTypeDef
            {
                typeId = "teleport", displayName = "傳送點", color = "#33AAFF",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "entranceId",     type = ParamType.String },
                    new TriggerParam { key = "targetMapId",    type = ParamType.Int },
                    new TriggerParam { key = "targetEntrance", type = ParamType.String },
                    new TriggerParam { key = "showMarker",     type = ParamType.Bool, boolDefault = true, label = "使用傳送點外型" },
                    new TriggerParam { key = "linkedFx",       type = ParamType.String, label = "連動特效id" },   // 場景特效 id：停用時隱藏（綠幕）、解鎖時顯示
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                typeId = "pickup", displayName = "道具拾取點", color = "#FFCC33",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "itemId", type = ParamType.String },
                    new TriggerParam { key = "count",  type = ParamType.Int },   // 一次給幾個（留空 = 1）
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                typeId = "playerSpawn", displayName = "玩家出生點", color = "#33FF88",
                paramSchema = new List<TriggerParam>()
            });
            set.types.Add(new TriggerTypeDef
            {
                typeId = "monsterSpawn", displayName = "怪物出生點", color = "#FF5555",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "monsterId", type = ParamType.String },
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                typeId = "drama", displayName = "劇情觸發點", color = "#AA66FF",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "dramaId", type = ParamType.Int },   // 對應 DramaTable.csv 的 ID
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                typeId = "cutscene", displayName = "事件/過場觸發點", color = "#22CC99",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "cutsceneId",     type = ParamType.String },   // 哪段過場（目前任意值＝穿隧道）
                    new TriggerParam { key = "video",          type = ParamType.String },   // 過場後要播的影片（mp4，之後接）
                    new TriggerParam { key = "targetMapId",    type = ParamType.Int },      // 過場完傳送到的地圖
                    new TriggerParam { key = "targetEntrance", type = ParamType.String },   // 目標地圖的落點
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：被觸發鏈啟動時「直接給物品進背包」（不用按 F、不綁位置；格子畫在角落即可）。
                typeId = "giveItem", displayName = "給予物品(鏈動作)", color = "#FFE08A",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "itemId", type = ParamType.String },
                    new TriggerParam { key = "count",  type = ParamType.Int },   // 留空 = 1
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：被觸發鏈啟動時「直接傳送」（不用踩傳送點、不綁位置；格子畫在角落即可）。
                typeId = "teleportTo", displayName = "直接傳送(鏈動作)", color = "#7FD4FF",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "targetMapId",    type = ParamType.Int },
                    new TriggerParam { key = "targetEntrance", type = ParamType.String },
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 傳送門互動點：玩家靠近按 F → 開啟「放劇本」傳送門 UI；linkTeleport 指向按下開啟後要解鎖的傳送點。
                typeId = "portal", displayName = "傳送門互動", color = "#FF8844",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "linkTeleport", type = ParamType.String, label = "要開的傳送點名" },
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                typeId = "camZone", displayName = "鏡頭區", color = "#66CCFF",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "zoom",    type = ParamType.Float },   // 縮放倍率（>1 拉遠、<1 拉近、留空=1）
                    new TriggerParam { key = "offsetX", type = ParamType.Float },   // 鏡頭右(+)/左(-)位移（世界單位）
                    new TriggerParam { key = "offsetY", type = ParamType.Float },   // 鏡頭上(+)/下(-)位移（世界單位）
                }
            });
            return set;
        }

        /// <summary>
        /// 觸發鏈通用欄位（**每種類型都有**，編輯器在類型參數下方統一顯示；遊戲端 TriggerChain 解讀）：
        ///   next          完成後啟動的 trigger（填同地圖另一個 trigger 的名稱或 id）
        ///   startDisabled 初始停用（等鏈解鎖才生效；配 enableFlag 可跨存檔記住已解鎖）
        ///   enableFlag    解鎖狀態旗標名（解鎖時寫 1；重進地圖旗標成立就自動啟用）
        ///   requireFlag   旗標成立才可觸發；前綴 "!" 表否定（例 "!killedFamily"）
        ///   setFlag       完成後寫 1 的旗標名
        ///   requireCycleMax 周目 ≤ 此值才成立（初始限定填 1；留空=不限）
        ///   requireCycleMin 周目 ≥ 此值才成立（老手限定用；留空=不限）
        ///   requireItem     背包道具條件：填 itemId=「須有此道具」；前綴 "!"（如 !104）=「須無此道具」；留空=不檢查
        ///   repeat          重複規則：每次進場(預設)/每次/每周目/永久，見主專案 readme/TRIGGER_CHAIN.md
        /// 以上所有條件（含 requireFlag）以 AND 結算，全成立才觸發。旗標名可加 "永久:" 前綴＝跨輪迴保存。
        /// </summary>
        public static readonly List<TriggerParam> ChainParams = new List<TriggerParam>
        {
            // ── 條件（可不可以觸發）──
            new TriggerParam { key = "requireFlag",     type = ParamType.String, label = "條件旗標",   group = "條件", isFlagRef = true, flagNegatable = true },
            new TriggerParam { key = "requireCycleMax", type = ParamType.Int,    label = "周目上限",   group = "條件" },
            new TriggerParam { key = "requireCycleMin", type = ParamType.Int,    label = "周目下限",   group = "條件" },
            new TriggerParam { key = "requireItem",     type = ParamType.String, label = "道具條件",   group = "條件" },
            // ── 一次性（會不會重複觸發）──
            new TriggerParam { key = "repeat",          type = ParamType.String, label = "重複規則",   group = "一次性",
                               options = new [] { "每次進場", "每次", "每周目", "永久" } },
            // ── 流程（觸發後做什麼）──
            new TriggerParam { key = "next",            type = ParamType.String, label = "接續觸發",   group = "流程" },
            new TriggerParam { key = "setFlag",         type = ParamType.String, label = "完成寫旗標", group = "流程", isFlagRef = true },
            // ── 解鎖（位置型：等鏈解鎖）──
            new TriggerParam { key = "startDisabled",   type = ParamType.Bool,   label = "初始停用",   group = "解鎖" },
            new TriggerParam { key = "enableFlag",      type = ParamType.String, label = "解鎖旗標",   group = "解鎖", isFlagRef = true },
        };
    }

    public class TriggerTypeDef
    {
        public string typeId;
        public string displayName;
        public string color = "#FFFFFF";   // 編輯器上的塗刷顏色

        [JsonProperty("params")]
        public List<TriggerParam> paramSchema = new List<TriggerParam>();
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ParamType { String, Int, Float, Bool }

    public class TriggerParam
    {
        public string key;
        public ParamType type = ParamType.String;
        public string label;             // 面板顯示用（空＝顯示 key）
        public bool boolDefault = false; // Bool 參數新建區域時的預設勾選狀態
        public string group;             // 面板分組小節標題（同組連續顯示在同一節；空＝不分組）
        public string[] options;         // 有值時＝以「循環按鈕」在這幾個選項間切換（避免打錯字），取代文字輸入
        public bool isFlagRef;           // 此欄的值是「旗標名」→ 面板改用旗標登記表下拉選（不手打）
        public bool flagNegatable;       // 旗標欄可否定（條件旗標）：加「有/沒有」切換，沒有＝存成 "!名字"
    }
}
