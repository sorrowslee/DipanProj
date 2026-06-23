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
                typeId = "environment", displayName = "環境/牆", color = "#B5651D",
                paramSchema = new List<TriggerParam>()
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
    }
}
