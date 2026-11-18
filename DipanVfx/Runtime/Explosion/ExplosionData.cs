using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;

namespace Dipan.Vfx
{
    /// <summary>
    /// ExplosionTable 的一列：一種爆炸長什麼樣（2026-11-18，DipanVfx；說明見 readme/DIPAN_VFX.md §4b）。
    /// 武器表 <c>HitBurstID</c> 指向這裡。爆炸大小由呼叫端給的「範圍半徑」決定（拋物線落地＝真正的傷害半徑），
    /// 沒給才用 <see cref="Radius"/>。所有數值都是「留空＝預設」。
    /// </summary>
    public sealed class ExplosionData
    {
        public int ID;
        public string Name;

        public Sprite[] Sprites; public string SpritesRaw = "";   // 火球雲圖（白色＋alpha＝密度），火球與黑煙共用
        public float Radius = 1.5f;          // 呼叫端沒給範圍時用的半徑
        public float MaxVisualRadius = 2.5f; // 火球／黑煙／白光用的半徑上限（衝擊波圈不受限，永遠畫在真正的範圍上）

        public int FireballMin = 3, FireballMax = 5;
        public float FireballSize = 0.9f;    // 每團火球直徑＝半徑 × 這個
        public float FireballSpread = 0.35f; // 火球散開距離＝半徑 × 這個
        public float FireballLife = 0.55f;   // 火球從炸開到燒完（秒）
        public float FireballGrow = 0.35f;   // 剛炸開時的大小（相對最終）
        public float FireballRise = 0.4f;    // 燒完時往上飄多少（×半徑）
        public float Stagger = 0.06f;        // 各團錯開的最大延遲（秒）
        public Color ColorCore = new Color(1f, 0.95f, 0.78f);
        public Color ColorMid = new Color(1f, 0.52f, 0.1f);
        public Color ColorEdge = new Color(0.62f, 0.1f, 0.03f);
        public float Intensity = 1.6f;
        public float Additive = 0.6f;
        public float Swirl = 0.08f;

        public int SmokeCount = 3;
        public float SmokeDelay = 0.12f;     // 炸開後多久冒煙（秒）
        public float SmokeLife = 1.1f;
        public float SmokeSize = 1.0f;       // 直徑＝半徑 × 這個
        public float SmokeRise = 0.9f;       // ×半徑
        public Color SmokeColor = new Color(0.16f, 0.13f, 0.12f, 0.85f);

        public float FlashSize = 1.3f;       // 白光直徑＝半徑 × 這個
        public float FlashTime = 0.14f;
        public Color FlashColor = new Color(1f, 0.92f, 0.7f);

        public float RingTime = 0.32f;       // 衝擊波圈擴到範圍邊緣的時間
        public float RingWidth = 0.06f;
        public Color RingColor = new Color(1f, 0.6f, 0.24f);
        public float RingIntensity = 1.5f;

        public int SparkCount = 24;
        public float SparkSpeed = 4f;        // 火星往外噴的速度（×半徑／1.5）
        public Color SparkColor = new Color(1f, 0.75f, 0.3f);
        public float SparkSize = 0.07f;

        public float Dim = 1f;
        public string Note = "";

        public bool HasSprites => Sprites != null && Sprites.Length > 0;
    }

    /// <summary>ExplosionTable 載入與查詢。主遊戲由 VfxManager 載入；地圖編輯器的特效預覽器直接讀 DipanProj_Main 的 CSV。</summary>
    public static class ExplosionDatabase
    {
        public const string TableName = "ExplosionTable";

        static readonly Dictionary<int, ExplosionData> _rows = new Dictionary<int, ExplosionData>();
        static readonly HashSet<int> _warnedMissing = new HashSet<int>();

        public static void ResetForPlayMode() { _rows.Clear(); _warnedMissing.Clear(); }

        public static ExplosionData Get(int id)
        {
            if (id <= 0) return null;
            if (_rows.TryGetValue(id, out var d)) return d;
            if (_warnedMissing.Add(id))
                Debug.LogWarning($"[Explosion] ExplosionTable 沒有 ID {id}（或還沒載入）。主遊戲：VfxManager 的 Explosion CSV 欄要拖 Assets/Data/ExplosionTable.csv。");
            return null;
        }

        public static void LoadFromText(string text)
        {
            _rows.Clear();
            _warnedMissing.Clear();
            var table = CsvTable.Parse(text ?? "", TableName);
            table.Require("ID", "Name", "Sprites");
            foreach (var err in table.Errors) Debug.LogError(err);
            foreach (var row in table.Rows)
            {
                int id = row.GetInt("ID", 0);
                if (id <= 0) continue;
                var d = new ExplosionData { ID = id, Name = row.Get("Name", $"#{id}") };
                d.SpritesRaw = row.Get("Sprites", "");
                var list = new List<Sprite>();
                foreach (var raw in d.SpritesRaw.Split('|'))
                {
                    string p = raw.Trim();
                    if (p.Length == 0) continue;
                    var sp = VfxAssets.LoadSprite(p);
                    if (sp == null) { Debug.LogWarning($"[Explosion] {d.Name}：找不到火球雲圖 Resources/{p}"); continue; }
                    list.Add(sp);
                }
                d.Sprites = list.ToArray();

                d.Radius = Mathf.Max(0.05f, row.GetFloat("Radius", d.Radius));
                d.MaxVisualRadius = Mathf.Max(0.05f, row.GetFloat("MaxVisualRadius", d.MaxVisualRadius));
                d.FireballMin = Mathf.Max(0, row.GetInt("FireballMin", d.FireballMin));
                d.FireballMax = Mathf.Max(d.FireballMin, row.GetInt("FireballMax", d.FireballMax));
                d.FireballSize = Mathf.Max(0.01f, row.GetFloat("FireballSize", d.FireballSize));
                d.FireballSpread = Mathf.Max(0f, row.GetFloat("FireballSpread", d.FireballSpread));
                d.FireballLife = Mathf.Max(0.05f, row.GetFloat("FireballLife", d.FireballLife));
                d.FireballGrow = Mathf.Clamp(row.GetFloat("FireballGrow", d.FireballGrow), 0.01f, 1f);
                d.FireballRise = row.GetFloat("FireballRise", d.FireballRise);
                d.Stagger = Mathf.Max(0f, row.GetFloat("Stagger", d.Stagger));
                d.ColorCore = VfxAssets.ParseColor(row.Get("ColorCore", ""), d.ColorCore);
                d.ColorMid = VfxAssets.ParseColor(row.Get("ColorMid", ""), d.ColorMid);
                d.ColorEdge = VfxAssets.ParseColor(row.Get("ColorEdge", ""), d.ColorEdge);
                d.Intensity = Mathf.Max(0f, row.GetFloat("Intensity", d.Intensity));
                d.Additive = Mathf.Clamp01(row.GetFloat("Additive", d.Additive));
                d.Swirl = Mathf.Max(0f, row.GetFloat("Swirl", d.Swirl));
                d.SmokeCount = Mathf.Max(0, row.GetInt("SmokeCount", d.SmokeCount));
                d.SmokeDelay = Mathf.Max(0f, row.GetFloat("SmokeDelay", d.SmokeDelay));
                d.SmokeLife = Mathf.Max(0.05f, row.GetFloat("SmokeLife", d.SmokeLife));
                d.SmokeSize = Mathf.Max(0.01f, row.GetFloat("SmokeSize", d.SmokeSize));
                d.SmokeRise = row.GetFloat("SmokeRise", d.SmokeRise);
                d.SmokeColor = VfxAssets.ParseColor(row.Get("SmokeColor", ""), d.SmokeColor);
                d.FlashSize = Mathf.Max(0f, row.GetFloat("FlashSize", d.FlashSize));
                d.FlashTime = Mathf.Max(0.01f, row.GetFloat("FlashTime", d.FlashTime));
                d.FlashColor = VfxAssets.ParseColor(row.Get("FlashColor", ""), d.FlashColor);
                d.RingTime = Mathf.Max(0.01f, row.GetFloat("RingTime", d.RingTime));
                d.RingWidth = Mathf.Max(0f, row.GetFloat("RingWidth", d.RingWidth));
                d.RingColor = VfxAssets.ParseColor(row.Get("RingColor", ""), d.RingColor);
                d.RingIntensity = Mathf.Max(0f, row.GetFloat("RingIntensity", d.RingIntensity));
                d.SparkCount = Mathf.Max(0, row.GetInt("SparkCount", d.SparkCount));
                d.SparkSpeed = Mathf.Max(0f, row.GetFloat("SparkSpeed", d.SparkSpeed));
                d.SparkColor = VfxAssets.ParseColor(row.Get("SparkColor", ""), d.SparkColor);
                d.SparkSize = Mathf.Max(0.005f, row.GetFloat("SparkSize", d.SparkSize));
                d.Dim = Mathf.Max(0f, row.GetFloat("Dim", d.Dim));
                d.Note = row.Get("Note", "");
                if (!d.HasSprites) Debug.LogWarning($"[Explosion] {d.Name}（ID {id}）沒有任何可用的火球雲圖，只會有白光、衝擊波與火星。");
                _rows[id] = d;
            }
        }
    }
}
