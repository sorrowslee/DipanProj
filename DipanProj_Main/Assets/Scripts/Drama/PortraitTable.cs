using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Drama
{
    /// <summary>
    /// 立繪固定微調表（<c>Assets/Data/PortraitTable.csv</c>，<b>整張表都是選填的</b>）：
    /// 一列 = 一張立繪，記「這張圖要比標準大/小多少、往哪偏多少」。
    ///
    /// <para><b>為什麼要有這張表</b>：微調有兩種，過去被混在一起填在 <c>DramaTalkTable.csv</c> 的每一句上。
    /// 一種是<b>補償素材尺寸誤差</b>——那已經由自動對齊（<see cref="PortraitFit"/>）解掉，不必再填；
    /// 另一種是<b>刻意的構圖選擇</b>（例如邪佛要比人大一點、要更沉進對話框），這是<b>那張圖的屬性、不是那句話的屬性</b>，
    /// 填在句子上的後果是同一張圖出現幾次就要填幾次（改一次要改十幾列）。這張表讓它只設定一次。</para>
    ///
    /// <para><b>查表用「解析後的 catalog id」</b>（例 <c>Main/Talk/Buddha/Buddha_normal</c>），
    /// 不是 CSV 裡寫的原字串——所以 <c>Actor_&lt;情緒&gt;</c> 會查到當下血統的那張圖。</para>
    ///
    /// <para><b>與每句微調的關係</b>：兩者<b>疊加</b>——最終縮放 = 這張表的 Scale × 該句的 Scale，
    /// 位移 = 這張表的 Offset ＋ 該句的 Offset。所以 <c>DramaTalkTable</c> 那 6 欄保留給「這一句的特例」，
    /// 平常留空即可。</para>
    ///
    /// <para><b>沒有這張表也能跑</b>：provider 沒拖檔、或某張圖沒列進來，就是 Scale=1 / Offset=0，
    /// 完全交給自動對齊。</para>
    /// </summary>
    public class PortraitTable
    {
        /// <summary>一張圖的固定微調。</summary>
        public struct Entry
        {
            public float scale;      // 1 = 不變
            public Vector2 offset;   // 畫面單位，+X 右、+Y 上
            public static Entry Default => new Entry { scale = 1f, offset = Vector2.zero };
        }

        static PortraitTable _instance;
        public static PortraitTable Instance
        {
            get
            {
                if (_instance == null) { _instance = new PortraitTable(); _instance.Load(); }
                return _instance;
            }
        }

        /// <summary>進 Play 模式時丟掉單例（已關 Domain Reload）。由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() => _instance = null;

        readonly Dictionary<string, Entry> _byId = new Dictionary<string, Entry>();

        /// <summary>查某張立繪的固定微調；表裡沒有這張圖就回預設（Scale 1 / Offset 0）。</summary>
        public Entry Get(string catalogId)
        {
            if (!string.IsNullOrEmpty(catalogId) && _byId.TryGetValue(catalogId, out var e)) return e;
            return Entry.Default;
        }

        void Load()
        {
            _byId.Clear();

            var provider = Object.FindObjectOfType<DramaTalkTableProvider>();
            var csv = provider != null ? provider.portraitCSV : null;
            if (csv == null) csv = Resources.Load<TextAsset>("Data/PortraitTable");   // 後備（舊位置）
            if (csv == null) return;   // 沒有這張表＝全部走自動對齊，不是錯誤、不印警告

            // 表頭：Id,Scale,OffsetX,OffsetY[,Note]。'#' 開頭 = 註解列。空欄 = 預設值。
            // 從第 0 行開始掃、靠內容認表頭（與 BloodlineTable 一樣，註解區塊可以寫在表頭前面）。
            string[] lines = (csv.text ?? "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;

                string[] v = line.Split(',');
                string id = v.Length > 0 ? v[0].Trim() : "";
                if (id.Length == 0) continue;
                if (id.Equals("Id", System.StringComparison.OrdinalIgnoreCase)) continue;   // 表頭

                _byId[id] = new Entry
                {
                    scale = F(v, 1, 1f),
                    offset = new Vector2(F(v, 2, 0f), F(v, 3, 0f)),
                };
            }

            if (_byId.Count > 0) Debug.Log($"[PortraitTable] 載入 {_byId.Count} 張立繪的固定微調。");
        }

        static float F(string[] v, int i, float fallback)
        {
            if (i >= v.Length) return fallback;
            string s = v[i].Trim();
            if (s.Length == 0) return fallback;
            return float.TryParse(s, System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out float n) ? n : fallback;
        }
    }
}
