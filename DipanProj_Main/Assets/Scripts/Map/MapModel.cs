using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Dipan.MapRuntime
{
    /// <summary>
    /// .dipanmap（編輯器輸出）的 runtime 資料模型。欄位與 DipanProj_MapEditor 的 MapData 一致，
    /// 主遊戲端只需要「讀」，故省略編輯期專用的便利方法。
    /// 座標慣例：以 tile 格為單位，(0,0) 在左上角，x 往右、y 往下。
    /// </summary>
    public class MapData
    {
        public string format = "dipanmap";
        public int version = 1;
        public string name = "Untitled";
        public string module = "";
        public string backgroundId = "";
        public float tileSize = 1f;
        public int width = 18;
        public int height = 10;
        // 可走層細分倍率：可走/牆/水位元圖 = 每個 tile 切 walkSubdiv×walkSubdiv 子格。1 = 舊地圖（tile 解析度）。
        public int walkSubdiv = 1;
        public Vec2 origin = new Vec2(0, 0);
        public List<LayerData> layers = new List<LayerData>();

        // 場景特效（可放置的粒子特效；編輯器放、遊戲端依 SceneFxTable 生成）。缺欄＝空清單。
        public List<SceneFxInstance> sceneFx = new List<SceneFxInstance>();

        [JsonIgnore] public int Subdiv => walkSubdiv < 1 ? 1 : walkSubdiv;
        [JsonIgnore] public int FineWidth => width * Subdiv;
        [JsonIgnore] public int FineHeight => height * Subdiv;

        [JsonIgnore] public LayerData GameLayer => layers.Find(l => l.type == LayerType.Game);
        [JsonIgnore] public LayerData WalkableLayer => layers.Find(l => l.type == LayerType.Walkable);
        [JsonIgnore] public LayerData TriggerLayer => layers.Find(l => l.type == LayerType.Trigger);
    }

    public class Vec2
    {
        public float x;
        public float y;
        public Vec2() { }
        public Vec2(float x, float y) { this.x = x; this.y = y; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum LayerType { Game, Walkable, Trigger }

    public class LayerData
    {
        public string id;
        public string name;
        public LayerType type;
        public bool visible = true;
        public float opacity = 1f;
        public bool locked = false;

        // Game payload
        public List<TilePlacement> tiles;
        public List<ObjectInstance> objects;

        // Walkable payload：每列一字串，'1' = 不可走、'0' = 可走。
        public List<string> blocked;

        // Trigger payload
        public List<TriggerRegion> regions;
    }

    public class TilePlacement
    {
        public int x;
        public int y;
        public string tileId;   // "catalogId#index"
    }

    public class ObjectInstance
    {
        public string assetId;
        public float x;
        public float y;
        public float rot = 0f;
        public bool flipX = false;
        public bool flipY = false;
        public float scaleX = 1f;
        public float scaleY = 1f;
        public float sortKey;
        public int zOrder = 0;
        public int hp = 1;       // 可破壞血量;編輯器寫入,缺省 = 1（打一下就壞）
        public float animFps = 8f; // 動畫地上物的每實例播放幀率（僅動畫物件有意義;靜態物件忽略）
        public bool pingPong = false; // 動畫播放模式：false=循環(0→N-1繞回)、true=乒乓(0→N-1→0來回，接縫不跳變)
    }

    /// <summary>場景特效實例（編輯器放置、遊戲端依 SceneFxTable 的 fxId 生成粒子）。座標為世界座標。</summary>
    public class SceneFxInstance
    {
        public string id;
        public int fxId = 1;
        public float startX, startY;
        public bool hasEnd = false;
        public float endX, endY;
        public float bulge = 0f;       // 弧線外鼓量（垂直起→終連線的偏移）
        public float w = 1f, h = 1f;   // 煙團大小倍率
        public bool loop = true;
        public bool intermittent = false;
        public float interval = 2f;
    }

    public class TriggerRegion
    {
        public string id;
        public string name;
        public string typeId;
        public List<int[]> cells = new List<int[]>();

        [JsonProperty("params")]
        public Dictionary<string, object> Params = new Dictionary<string, object>();

        /// <summary>取字串參數，找不到回 fallback。</summary>
        public string GetString(string key, string fallback = "")
            => (Params != null && Params.TryGetValue(key, out var v) && v != null) ? v.ToString() : fallback;

        /// <summary>取整數參數（值在 .dipanmap 以字串存，這裡 parse），找不到/無效回 fallback。</summary>
        public int GetInt(string key, int fallback = 0)
            => (Params != null && Params.TryGetValue(key, out var v) && v != null && int.TryParse(v.ToString(), out int n)) ? n : fallback;

        /// <summary>取浮點參數（值在 .dipanmap 以字串存，這裡 parse），找不到/無效回 fallback。</summary>
        public float GetFloat(string key, float fallback = 0f)
            => (Params != null && Params.TryGetValue(key, out var v) && v != null
                && float.TryParse(v.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float n))
               ? n : fallback;
    }

    // ---- 素材目錄（catalog.json，由 sync_map_assets.sh 生成）----

    public class Catalog
    {
        public List<CatalogItem> items = new List<CatalogItem>();
        public CatalogItem Find(string id) => items.Find(i => i.id == id);
    }

    public class CatalogItem
    {
        public string id;        // = 相對路徑去副檔名（與 .dipanmap 的 assetId 一致）。動畫物件 = 資料夾相對路徑。
        public string path;      // StreamingAssets/MapAssets 內的相對路徑（含副檔名）。動畫物件 = 第一幀。
        public string category;  // Tiles / Environment / Background
        public string module = "Main";
        public int pixelSize;
        public int ppu = 256;

        // ---- 動畫地上物（多張圖做成一個物件）----
        public int frameCount = 1;          // 1（或缺欄）= 靜態單張；>1 = 動畫物件。
        public List<string> frames;         // 各幀相對路徑（依序，含第一幀）；靜態 = null。

        [JsonIgnore] public bool IsAnimated => frameCount > 1 && frames != null && frames.Count > 1;
    }
}
