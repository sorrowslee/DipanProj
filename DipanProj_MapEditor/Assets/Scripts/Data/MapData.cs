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

        /// <summary>
        /// 可走層細分倍率：可走/牆/水的位元圖解析度 = 每個 tile 切成 walkSubdiv×walkSubdiv 子格。
        /// 1 = 與 tile 同解析度（舊地圖）；4 = 每格切 4×4（細膩描邊）。
        /// 注意：只影響「可走層」，地磚/物件/trigger 仍維持 tile 解析度。
        /// </summary>
        public int walkSubdiv = 1;

        /// <summary>有效細分倍率（至少 1）。</summary>
        [JsonIgnore] public int Subdiv => walkSubdiv < 1 ? 1 : walkSubdiv;
        /// <summary>可走層位元圖的寬（子格數）= width × Subdiv。</summary>
        [JsonIgnore] public int FineWidth => width * Subdiv;
        /// <summary>可走層位元圖的高（子格數）= height × Subdiv。</summary>
        [JsonIgnore] public int FineHeight => height * Subdiv;

        /// <summary>世界原點（左上角錨點）；resize 由右/下邊增減。</summary>
        public Vec2 origin = new Vec2(0, 0);

        /// <summary>有序圖層。目前固定三層：game / walkable / trigger。</summary>
        public List<LayerData> layers = new List<LayerData>();

        /// <summary>場景特效（可放置的粒子特效，煙/火/冰/毒…）；獨立於圖層，直接掛在地圖上。</summary>
        public List<SceneFxInstance> sceneFx = new List<SceneFxInstance>();

        /// <summary>
        /// 獨立光源（不綁地上物的照明點）；獨立於圖層，直接掛在地圖上。
        /// 給「火炬/燈籠已經畫在背景圖裡」的情況：不必把它們從背景拆成地上物，把光源點放到火焰中心就會發光。
        /// 舊地圖沒有這個欄位 → 反序列化後是空清單，行為與以前相同。
        /// </summary>
        public List<LightInstance> lights = new List<LightInstance>();

        /// <summary>劇情演出（半演出半漫畫的過場）；null＝此圖無演出。目前一張圖最多一段。</summary>
        public Cutscene cutscene = null;

        // ---- 便利存取 ----

        [JsonIgnore]
        public LayerData GameLayer => layers.Find(l => l.type == LayerType.Game);
        [JsonIgnore]
        public LayerData WalkableLayer => layers.Find(l => l.type == LayerType.Walkable);
        [JsonIgnore]
        public LayerData TriggerLayer => layers.Find(l => l.type == LayerType.Trigger);

        /// <summary>新地圖預設細分倍率（4×4 子格，最細膩）。</summary>
        public const int DefaultSubdiv = 4;

        /// <summary>建立一張空白地圖（固定三層；可走層初始全部為牆）。</summary>
        public static MapData CreateBlank(string name, string module, float tileSize, int width, int height, string backgroundId = "", int subdiv = DefaultSubdiv)
        {
            if (subdiv < 1) subdiv = 1;
            var map = new MapData
            {
                name = name,
                module = module,
                backgroundId = backgroundId,
                tileSize = tileSize,
                width = width,
                height = height,
                walkSubdiv = subdiv,
            };

            map.layers.Add(new LayerData
            {
                id = "game-1", name = "遊戲圖層", type = LayerType.Game,
                objects = new List<ObjectInstance>(),
            });

            map.layers.Add(new LayerData
            {
                id = "walk", name = "可走/牆/水", type = LayerType.Walkable,
                blocked = MakeAllBlocked(width * subdiv, height * subdiv),
            });

            map.layers.Add(new LayerData
            {
                id = "trig", name = "Trigger", type = LayerType.Trigger,
                regions = new List<TriggerRegion>(),
            });

            return map;
        }

        /// <summary>產生「全部為牆」的位元圖（每列一字串，'1' = 牆）。參數為子格數（已乘上 subdiv）。</summary>
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
