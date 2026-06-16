using System.Collections.Generic;
using Newtonsoft.Json;

namespace DipanMapEditor.Data
{
    /// <summary>
    /// 一張地圖的完整資料，對應磁碟上的 .dipanmap (JSON) 檔。
    /// 座標慣例：以 tile 格為單位；(0,0) 為左上角，x 往右、y 往下。
    /// </summary>
    public class MapData
    {
        public string format = "dipanmap";
        public int version = 1;

        public string name = "Untitled";

        /// <summary>此地圖所屬的 module（關卡）；決定可用素材 = Main + 此 module。</summary>
        public string module = "";

        /// <summary>背景圖素材 ID（catalog 的 Background 分類）；空＝不用背景圖、用純黑底+tile。</summary>
        public string backgroundId = "";

        /// <summary>單一 tile 的世界尺寸（單位）。預設 1（= 256px，依主專案推導）。</summary>
        public float tileSize = 1f;

        /// <summary>畫布寬（tile 格數）。預設 18 ≈ 一個螢幕寬。</summary>
        public int width = 18;
        /// <summary>畫布高（tile 格數）。預設 10 ≈ 一個螢幕高。</summary>
        public int height = 10;

        /// <summary>世界原點（左上角錨點）；resize 由右/下邊增減。</summary>
        public Vec2 origin = new Vec2(0, 0);

        /// <summary>有序圖層。目前固定三層：game / walkable / trigger。</summary>
        public List<LayerData> layers = new List<LayerData>();

        // ---- 便利存取 ----

        [JsonIgnore]
        public LayerData GameLayer => layers.Find(l => l.type == LayerType.Game);
        [JsonIgnore]
        public LayerData WalkableLayer => layers.Find(l => l.type == LayerType.Walkable);
        [JsonIgnore]
        public LayerData TriggerLayer => layers.Find(l => l.type == LayerType.Trigger);

        /// <summary>建立一張空白地圖（固定三層；可走層初始全部不可走）。</summary>
        public static MapData CreateBlank(string name, string module, float tileSize, int width, int height, string backgroundId = "")
        {
            var map = new MapData
            {
                name = name,
                module = module,
                backgroundId = backgroundId,
                tileSize = tileSize,
                width = width,
                height = height,
            };

            map.layers.Add(new LayerData
            {
                id = "game-1", name = "遊戲圖層", type = LayerType.Game,
                tiles = new List<TilePlacement>(),
                objects = new List<ObjectInstance>(),
            });

            map.layers.Add(new LayerData
            {
                id = "walk", name = "可走/不可走", type = LayerType.Walkable,
                blocked = MakeAllBlocked(width, height),
            });

            map.layers.Add(new LayerData
            {
                id = "trig", name = "Trigger", type = LayerType.Trigger,
                regions = new List<TriggerRegion>(),
            });

            return map;
        }

        /// <summary>產生「全部不可走」的位元圖（每列一字串，'1' = 不可走）。</summary>
        public static List<string> MakeAllBlocked(int width, int height)
        {
            var rows = new List<string>(height);
            string full = new string('1', width);
            for (int y = 0; y < height; y++) rows.Add(full);
            return rows;
        }
    }

    /// <summary>簡單二維座標（float），供 origin / 物件位置等使用。</summary>
    public class Vec2
    {
        public float x;
        public float y;
        public Vec2() { }
        public Vec2(float x, float y) { this.x = x; this.y = y; }
    }
}
