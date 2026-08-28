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

        // NPC（編輯器「NPC」分頁放：站位/行為/路徑/對話/介面/接鏈）。缺欄＝空清單（舊地圖行為不變）。
        public List<NpcInstance> npcs = new List<NpcInstance>();

        // 獨立光源（不綁地上物的照明點；編輯器「照明」分頁放）。缺欄＝空清單（舊地圖行為不變）。
        // 給「火炬/燈籠已經畫在背景圖裡」的情況：不必把它們從背景拆成地上物，把光源點放到火焰中心就會發光。
        public List<LightInstance> lights = new List<LightInstance>();

        /// <summary>劇情演出清單；空＝此圖無演出。目前一張圖只會有一段，但格式先做成清單（見編輯器端 MapData 註解）。</summary>
        public List<Cutscene> cutscenes = new List<Cutscene>();

        /// <summary>⚠ 舊格式相容欄位（單一物件）。讀檔後由 <see cref="NormalizeCutscenes"/> 搬進 cutscenes，不要直接讀。</summary>
        public Cutscene cutscene = null;

        /// <summary>（第一段）演出；沒有就回 null。</summary>
        [JsonIgnore] public Cutscene MainCutscene => (cutscenes != null && cutscenes.Count > 0) ? cutscenes[0] : null;

        /// <summary>依 id 找一段演出；id 留空＝取第一段。找不到回 null。</summary>
        public Cutscene FindCutscene(string id)
        {
            if (cutscenes == null || cutscenes.Count == 0) return null;
            if (string.IsNullOrWhiteSpace(id)) return cutscenes[0];
            string want = id.Trim();
            for (int i = 0; i < cutscenes.Count; i++)
                if (cutscenes[i] != null && string.Equals(cutscenes[i].id?.Trim(), want, System.StringComparison.OrdinalIgnoreCase))
                    return cutscenes[i];
            return null;
        }

        /// <summary>讀檔後呼叫：把舊格式的單一 cutscene 搬進 cutscenes 清單。</summary>
        public void NormalizeCutscenes()
        {
            if (cutscenes == null) cutscenes = new List<Cutscene>();
            if (cutscenes.Count == 0 && cutscene != null) cutscenes.Add(cutscene);
            cutscene = null;
        }

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
        public List<ObjectInstance> objects;

        // Walkable payload：每列一字串，'1' = 不可走、'0' = 可走。
        public List<string> blocked;

        // Trigger payload
        public List<TriggerRegion> regions;
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
        public bool walkable = false; // 勾選＝不設碰撞、不擋路（走地圖可走層判定）；也因此不掛可破壞。畫在角色腳下（固定低排序）。
        public bool passThrough = false; // 勾選＝不設碰撞（可穿過）但『照常 Y-sort』依 Y 前後遮蔽（站立的鬼魂/煙/光用）。與 walkable 的差別：walkable 固定畫在角色腳下、passThrough 依 Y 和角色/彼此正確交錯。
        public string breakFlag = ""; // 被破壞時把此旗標設為 true（給觸發鏈 requireFlag 用，例：打破供品→改變劇情）；空＝不寫。僅可破壞物件有效
        // 出現條件：完成 N 關後才出現（用「完成關卡數」判定）。0（或缺欄）＝一開始就出現。MapLoader 進圖當下判定，未達則不生此物件。
        public int appearAfterClears = 0;
        // 出現條件範圍：cycle＝每周目重算（本周目完成數）、lifetime＝曾達到過就永久出現。缺欄＝cycle。
        public string appearScope = "cycle";

        // 動畫地上物：true＝播一次到最後一幀就停住（不循環，例：跪拜停在跪姿）；false＝依 pingPong 循環/乒乓。
        public bool playOnce = false;
        // 出現條件（旗標）：此旗標為 true 才顯示（空＝不看旗標）。與 appearAfterClears 同時設＝兩者都滿足才顯示(AND)。
        // 進圖時旗標未成立則先建好藏起來，旗標中途成立時由 MapObjectRevealer 現身（動畫從第0幀起播）。
        public string appearFlag = "";
        // 旗標中途成立後，延遲幾秒才現身（0＝立刻）。只對「靠旗標中途現身」有意義。
        public float appearDelaySeconds = 0f;
        // 現身時淡入（只對「靠旗標中途現身」有意義；進圖旗標已成立＝直接顯示，不淡入）。
        public bool appearFade = false;
        // 消失旗標：此旗標為 true 時這個地上物消失（與 appearFlag 相反）。空＝永不因旗標消失。
        // 進圖時旗標已成立＝根本不生此物件；旗標於關卡中途成立＝由 MapObjectRevealer 立即銷毀。
        // 搭配觸發鏈 setFlag 用（例：pickup 撿起佛燈 → setFlag → 佛燈地上物消失）。
        public string disappearFlag = "";
        // ── 照明（火把/燈籠/香爐/地上的佛燈…）──
        // 由 MapLoader 掛 LightSource；AtmosphereController 每幀取最近的 N 盞餵給氛圍 shader（多光源）。
        // 只有「暗氛圍地圖（2 幽暗/3 噩夢/9 深海恐怖）」或「MapsTable 環境亮度<100 的地圖」看得到效果。
        // 發光半徑（世界單位）：>0＝這個地上物擺在原地時發光照亮周遭。0/空＝不發光（以下欄位一律忽略）。
        public float lightRadius = 0f;
        // 亮度倍率：1＝標準；<1 微光（快熄的燭火）；>1 刺眼（爐火）。缺欄＝1。
        public float lightIntensity = 1f;
        // 光色（6 碼 16 進位 RRGGBB，不含 #）。空/無效＝預設暖橘（火把）。例：FFC785 暖橘、CFE4FF 冷月、7CFFB0 鬼火綠。
        public string lightColor = "";
        // 搖晃強度：0＝完全不動（電燈/月光）；1＝標準燭火；2＝狂亂火焰。缺欄＝1。
        public float lightFlicker = 1f;
        // 搖晃速度倍率：小＝油燈慢慢晃；大＝營火急促跳動。缺欄＝1。
        public float lightFlickerSpeed = 1f;
        // 邊緣柔和度＝內圈(全亮)半徑佔外圈的比例 0~1。小＝瀰漫柔邊；大＝範圍內均勻、邊緣硬（聚光燈）。缺欄＝0.46。
        public float lightSoftness = 0.46f;
    }

    /// <summary>
    /// 獨立光源實例（編輯器「照明」分頁放置，不綁任何地上物）。座標為世界座標。
    /// 用途：火炬／燈籠這類**已經畫在背景圖裡**的照明物——不必為了發光把它們從背景拆成地上物，
    /// 把光源點放到火焰中心即可。遊戲端由 MapLoader 生一個空物件掛 <c>LightSource</c>。
    /// 另一條路是地上物自己的「發光半徑」（見 ObjectInstance 的照明六欄），適合本身就是可撿/可破壞物件的燈
    /// （例：柴房地上的佛燈，撿走光就跟著消失）。兩者最後都變成 LightSource，餵進同一份光源清單。
    /// </summary>
    public class LightInstance
    {
        public string id;
        public string name = "";        // 好認的名字，只給編輯器清單顯示，遊戲端不讀
        public float x, y;              // 世界座標
        public float radius = 3f;       // 發光半徑（世界單位）
        public float intensity = 1f;    // 亮度倍率
        public string color = "";       // 光色 6 碼 16 進位 RRGGBB（空＝預設暖橘）
        public float flicker = 1f;      // 搖晃強度（0＝完全不動）
        public float flickerSpeed = 1f; // 搖晃速度倍率
        public float softness = 0.46f;  // 邊緣柔和度＝內圈佔外圈的比例
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

    /// <summary>
    /// 一個 NPC 擺放實例（編輯器「NPC」分頁放置；種類資料查 NpcTable.csv）。座標為世界座標。
    /// 行為：idle＝原地不動；patrol＝沿 [站位→waypoints…] 乒乓來回（走到尾倒著走回站位）。
    /// dramaId＝按 F 交談播的對話（DramaTable，Type 1/2 皆可；0＝不對話）；
    /// panelId/panelArg＝對話結束後開的介面（沒填 dramaId 時＝按 F 直接開）；
    /// next/setFlag＝對話結束後接觸發鏈（每次進圖只跑第一次）。見 readme/NPC_SYSTEM.md。
    /// </summary>
    public class NpcInstance
    {
        public const string BehaviorIdle = "idle";
        public const string BehaviorPatrol = "patrol";

        public string id;
        public string name = "";          // 好認的名字（編輯器清單顯示；遊戲端只在警告訊息用）
        public int npcId;                 // NpcTable.csv 的 ID
        public float x, y;                // 站位（世界座標）
        public string behavior = BehaviorIdle;   // idle / patrol（未來擴充：escort…）
        public List<Vec2> waypoints = new List<Vec2>();   // patrol 的路徑點（不含站位）
        public float speed = 0f;          // 走動速度覆寫；0＝用 NpcTable 的 Speed
        public float dwellSeconds = 0f;   // 抵達每個路徑點停留秒數
        public int dramaId = 0;           // 按 F 交談的對話（0＝不對話）
        public string panelId = "";       // 對話結束後開的介面（見 InteractionManager.OpenPanelById）
        public string panelArg = "";      // 介面參數（例：抽選池代號、未來的 shopId）
        public string next = "";          // 對話結束後啟動的 trigger（名稱，同觸發鏈 next）
        public string setFlag = "";       // 對話結束後寫的旗標
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

        /// <summary>取布林參數：找不到/空 → fallback；"true"/"1" → true；"false"/"0" → false（也吃編輯器存的 bool）。</summary>
        public bool GetBool(string key, bool fallback = false)
        {
            if (Params == null || !Params.TryGetValue(key, out var v) || v == null) return fallback;
            string s = v.ToString().Trim().ToLowerInvariant();
            if (s == "true" || s == "1") return true;
            if (s == "false" || s == "0") return false;
            return fallback;
        }

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

        // ---- 地上物佔位遮罩（讓碰撞貼合圖形、透明處不擋路）----
        // 由 Project Tools → Sync Map Assets 烘進來（只烘 Environment 分類；動畫物件取第一幀）。
        // 缺欄 = null：MapSpriteLoader 會當場掃一次當退路，結果相同、只是載入慢一點
        // （shell 版同步腳本不會烘，所以這條退路一定會被用到）。見 FootprintMask / ObjectFootprint。
        public FootprintMask footprint;

        [JsonIgnore] public bool IsAnimated => frameCount > 1 && frames != null && frames.Count > 1;
    }
}
