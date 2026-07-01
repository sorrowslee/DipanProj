using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DipanMapEditor.Data
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum LayerType
    {
        Game,       // 遊戲圖層：地板/牆壁 tile + 自由變換物件
        Walkable,   // 可走/不可走位元圖
        Trigger,    // Trigger 區域
    }

    /// <summary>
    /// 統一的圖層資料。依 <see cref="type"/> 使用不同 payload，
    /// 不相關的欄位序列化時為 null（NullValueHandling.Ignore 會略過）。
    /// </summary>
    public class LayerData
    {
        public string id;
        public string name;
        public LayerType type;

        public bool visible = true;
        public float opacity = 1f;
        public bool locked = false;

        // ---- Game payload ----
        /// <summary>格對齊的 tile（地板/牆壁）。</summary>
        public List<TilePlacement> tiles;
        /// <summary>自由變換的物件（家具等）。</summary>
        public List<ObjectInstance> objects;

        // ---- Walkable payload ----
        /// <summary>
        /// 可走層三態位元圖，每列一字串：'0' = 可走、'1' = 牆（擋＋反彈子彈）、'2' = 水/坑（擋腳、子彈穿過）。
        /// 解析度為子格：列數 = height × walkSubdiv，每列長度 = width × walkSubdiv。
        /// </summary>
        public List<string> blocked;

        // ---- Trigger payload ----
        /// <summary>Trigger 區域，允許重疊、同型多塊（以 name 區分）。</summary>
        public List<TriggerRegion> regions;
    }

    /// <summary>單一格 tile 放置：格座標 + 素材 ID（字串，對應 catalog）。</summary>
    public class TilePlacement
    {
        public int x;
        public int y;
        public string tileId;
    }

    /// <summary>自由變換的物件實例：位置（世界單位）、翻轉、縮放、Y-sort。</summary>
    public class ObjectInstance
    {
        public string assetId;   // 對應 catalog 的字串 ID
        public float x;
        public float y;
        public float rot = 0f;   // 角度
        public bool flipX = false;
        public bool flipY = false;
        public float scaleX = 1f;
        public float scaleY = 1f;
        public float sortKey;    // 通常 = y，越小越上層（top-down Y-sort）
        public int zOrder = 0;   // 手動層級：>0 整層往前、<0 往後；同層內仍依 sortKey Y-sort
        public int hp = 1;       // 血量；-1 = 不可被摧毀，>=0 = 可摧毀血量（預設 1，交由遊戲端解讀）
        public float animFps = 8f; // 動畫地上物的每實例播放幀率（僅動畫物件有意義；靜態物件忽略）
        public bool pingPong = false; // 動畫播放模式：false=循環(0→N-1繞回)、true=乒乓(0→N-1→0來回，首尾接不順時用，接縫消失)
    }

    /// <summary>一塊 trigger 區域：型別 + 名稱 + 格集合 + 參數（依 TriggerType schema）。</summary>
    public class TriggerRegion
    {
        public string id;
        public string name;
        public string typeId;    // 對應 TriggerTypeDef.typeId

        /// <summary>組成此區域的格座標，每筆為 [x, y]。</summary>
        public List<int[]> cells = new List<int[]>();

        /// <summary>此區域的參數值（key 對應 TriggerType 的 param schema）。</summary>
        [JsonProperty("params")]
        public Dictionary<string, object> Params = new Dictionary<string, object>();
    }

    /// <summary>
    /// 一個「場景特效」實例：可放置的粒子特效（煙/火/冰/毒…），外觀由 SceneFxTable 的 fxId 決定。
    /// 起點必填、終點選填（有終點＝沿弧線流動，如煙霧圍巾；無終點＝從起點朝上噴）。座標為世界座標。
    /// </summary>
    public class SceneFxInstance
    {
        public string id;
        public int fxId = 1;       // 特效編號（對應 SceneFxTable）
        public float startX, startY;
        public bool hasEnd = false;
        public float endX, endY;
        public float bulge = 0f;   // 弧線外鼓量（垂直起→終連線的偏移；0=直線，正負決定鼓哪邊）
        public float w = 1f, h = 1f; // 煙團大小倍率（X / Y）
        public bool loop = true;      // 是否持續（false=只播一輪，配合 intermittent）
        public bool intermittent = false; // 是否間歇播放
        public float interval = 2f;   // 間歇：開/關各持續幾秒
    }
}
