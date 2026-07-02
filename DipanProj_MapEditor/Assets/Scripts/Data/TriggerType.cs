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
    }
}
