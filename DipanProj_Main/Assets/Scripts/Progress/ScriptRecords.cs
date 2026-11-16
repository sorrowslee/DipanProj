using System;
using System.Collections.Generic;
using UnityEngine;
using Dipan.Localization;
using Dipan.Save;

namespace Dipan.Progress
{
    /// <summary>劇本證明的等級。數字越大越難；拿高階會連帶拿低階（S ⇒ S＋A＋B）。</summary>
    public enum ProofTier
    {
        B = 1,
        A = 2,
        S = 3,
    }

    /// <summary>劇本對這個角色的狀態（給之後的邪佛發牌用，見 readme/CORE_LOOP_DESIGN.md §8.3）。</summary>
    public enum ScriptState
    {
        New,        // 一張證明都沒拿過
        Echo,       // 拿過至少一張、但沒拿齊（業障回響）
        Graduated,  // 三張都拿過：以後不再發
    }

    /// <summary>證明等級的顯示資訊（名稱、存檔鍵、圖示道具、解析）。</summary>
    public static class ProofTierInfo
    {
        /// <summary>由低到高（B → A → S）。</summary>
        public static readonly ProofTier[] All = { ProofTier.B, ProofTier.A, ProofTier.S };

        // 語言表 11001–11003：B級證明／A級證明／S級證明（證明系統段 11001–11099）
        const int TxtNameB = 11001;

        /// <summary>
        /// 每一級在 ItemTable 裡借一列（106／107／108），只拿來顯示名稱與圖示——
        /// 證明本身是錢包裡的數字，不是背包道具（取得時由 RunProgress.GiveStack 攔下轉成數字）。
        /// </summary>
        public const int ItemIdB = 106, ItemIdA = 107, ItemIdS = 108;

        public static int ItemIdOf(ProofTier t)
        {
            switch (t)
            {
                case ProofTier.A: return ItemIdA;
                case ProofTier.S: return ItemIdS;
                default: return ItemIdB;
            }
        }

        /// <summary>道具 id → 證明等級；不是證明道具回 false。</summary>
        public static bool FromItemId(int itemId, out ProofTier t)
        {
            switch (itemId)
            {
                case ItemIdB: t = ProofTier.B; return true;
                case ItemIdA: t = ProofTier.A; return true;
                case ItemIdS: t = ProofTier.S; return true;
            }
            t = ProofTier.B;
            return false;
        }

        /// <summary>「B級證明」這種顯示名稱（語言表）。</summary>
        public static string Name(ProofTier t) => Language.GetText(TxtNameB + Index(t));

        /// <summary>存檔／CSV 用的鍵：B／A／S。</summary>
        public static string Key(ProofTier t) => t == ProofTier.S ? "S" : t == ProofTier.A ? "A" : "B";

        static int Index(ProofTier t) => Mathf.Clamp((int)t - 1, 0, 2);

        /// <summary>解析 B／A／S（不分大小寫，也接受 1／2／3、「B級」）。</summary>
        public static bool TryParse(string s, out ProofTier t)
        {
            t = ProofTier.B;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().ToUpperInvariant().Replace("級", "").Replace("证明", "").Replace("證明", "");
            switch (s)
            {
                case "B": case "1": t = ProofTier.B; return true;
                case "A": case "2": t = ProofTier.A; return true;
                case "S": case "3": t = ProofTier.S; return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 角色紀錄層的查詢與寫入入口——**其他系統一律透過這裡讀寫劇本紀錄，不要直接碰存檔欄位**。
    /// 規則與架構見 readme/SAVE_SYSTEM.md §15、readme/CORE_LOOP_DESIGN.md §8。
    ///
    /// <para>資料本體在 <see cref="CharacterSave.scriptRecords"/>（角色存檔頂層＝輪迴不清，開新角色才空）。
    /// 手上的證明「數量」是貨幣，在 <see cref="StatsDTO"/>（輪迴會清）；兩者不要混。</para>
    ///
    /// <para>沒有存檔（單場景測試）時退回記憶體，行為一樣、只是不保存。</para>
    /// </summary>
    public static class ScriptRecords
    {
        /// <summary>
        /// 某劇本的某一級證明**這個角色第一次領到**時發出（連帶取得的低階也各發一次）。
        /// 之後的成就系統訂閱它即可即時解鎖（SAVE_SYSTEM §15.2 第 4 條）。
        /// </summary>
        public static event Action<string, ProofTier> OnProofClaimed;

        // 無存檔時的後備（單場景測試）
        static readonly Dictionary<string, ScriptRecordDTO> _mem = new Dictionary<string, ScriptRecordDTO>();
        static readonly int[] _memHeld = new int[4];

        // 這一趟關卡新拿到的（結算畫面播動畫用）；RunProgress.BeginRun 時清空
        static readonly List<KeyValuePair<string, ProofTier>> _grantedThisRun = new List<KeyValuePair<string, ProofTier>>();

        /// <summary>進 Play 時清掉（Domain Reload 已關）。由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode()
        {
            OnProofClaimed = null;
            _mem.Clear();
            Array.Clear(_memHeld, 0, _memHeld.Length);
            _grantedThisRun.Clear();
        }

        // ───────────── 查詢 ─────────────

        static Dictionary<string, ScriptRecordDTO> Map
        {
            get
            {
                var sm = SaveManager.Instance;
                var map = sm != null ? sm.ScriptRecordsData : null;
                return map ?? _mem;
            }
        }

        static ProofClaimDTO Find(string module, ProofTier tier)
        {
            if (string.IsNullOrEmpty(module)) return null;
            if (!Map.TryGetValue(module, out var rec) || rec == null || rec.proofs == null) return null;
            return rec.proofs.TryGetValue(ProofTierInfo.Key(tier), out var c) ? c : null;
        }

        /// <summary>這個角色領過這個劇本的這一級沒（一輩子一次）。</summary>
        public static bool IsClaimed(string module, ProofTier tier) => Find(module, tier) != null;

        /// <summary>領取紀錄（第幾世、什麼時間）；沒領過回 null。</summary>
        public static ProofClaimDTO GetClaim(string module, ProofTier tier) => Find(module, tier);

        /// <summary>這個劇本已領了幾級（0～3）。</summary>
        public static int ClaimedCount(string module)
        {
            int n = 0;
            foreach (var t in ProofTierInfo.All) if (IsClaimed(module, t)) n++;
            return n;
        }

        /// <summary>新劇本／迴響／畢業（CORE_LOOP §8.3）。</summary>
        public static ScriptState StateOf(string module)
        {
            int n = ClaimedCount(module);
            return n == 0 ? ScriptState.New : n >= ProofTierInfo.All.Length ? ScriptState.Graduated : ScriptState.Echo;
        }

        /// <summary>手上持有幾張某級證明（貨幣）。</summary>
        public static int Held(ProofTier tier)
        {
            var sm = SaveManager.Instance;
            return sm != null && sm.Current != null ? sm.GetProofs(tier) : _memHeld[(int)tier];
        }

        /// <summary>這一趟關卡新拿到的（劇本, 等級）；依取得順序。</summary>
        public static IReadOnlyList<KeyValuePair<string, ProofTier>> GrantedThisRun => _grantedThisRun;

        /// <summary>這一趟是不是新拿到了這一級。</summary>
        public static bool GrantedThisRunContains(string module, ProofTier tier)
        {
            for (int i = 0; i < _grantedThisRun.Count; i++)
                if (_grantedThisRun[i].Value == tier && _grantedThisRun[i].Key == module) return true;
            return false;
        }

        // ───────────── 寫入 ─────────────

        /// <summary>新的一趟關卡開始（RunProgress.BeginRun 呼叫）：清「這一趟新拿到的」。</summary>
        public static void BeginRun() => _grantedThisRun.Clear();

        /// <summary>
        /// 發證明：<paramref name="tier"/> 以下（含）每一級，沒領過的就入帳＋寫紀錄；已領過的跳過。
        /// 有新拿到就**立刻存檔**（打完頭目閃退也不會少給），並對每一級發 <see cref="OnProofClaimed"/>。
        /// 回傳這次新拿到的等級（由低到高）；全都領過回空清單。
        /// </summary>
        public static List<ProofTier> Grant(string module, ProofTier tier)
        {
            var newly = new List<ProofTier>();
            if (string.IsNullOrEmpty(module))
            {
                Debug.LogWarning("[ScriptRecords] Grant：沒有劇本 module（不在關卡裡？），不發證明。");
                return newly;
            }

            var sm = SaveManager.Instance;
            bool hasSave = sm != null && sm.Current != null;
            int gen = hasSave ? Mathf.Max(1, sm.Cycle) : 1;
            string utc = DateTime.UtcNow.ToString("o");

            foreach (var t in ProofTierInfo.All)
            {
                if ((int)t > (int)tier) break;
                if (IsClaimed(module, t)) continue;

                if (hasSave)
                {
                    sm.RecordProofClaim(module, ProofTierInfo.Key(t), gen, utc);
                    sm.AddProofs(t, 1);
                }
                else
                {
                    if (!_mem.TryGetValue(module, out var rec) || rec == null) _mem[module] = rec = new ScriptRecordDTO();
                    rec.proofs[ProofTierInfo.Key(t)] = new ProofClaimDTO { gen = gen, utc = utc };
                    _memHeld[(int)t]++;
                }
                newly.Add(t);
                _grantedThisRun.Add(new KeyValuePair<string, ProofTier>(module, t));
            }

            if (newly.Count == 0)
            {
                Debug.Log($"[ScriptRecords] {module} 的 {ProofTierInfo.Key(tier)} 級（含以下）都領過了，這次不再給。");
                return newly;
            }

            if (hasSave) sm.SaveNow();
            else Debug.LogWarning("[ScriptRecords] 沒有存檔（單場景測試？），證明只記在記憶體、不會保存。");

            Debug.Log($"[ScriptRecords] {module}：取得 {string.Join("、", newly.ConvertAll(t => ProofTierInfo.Key(t)))} 級證明（第 {gen} 世）。");
            foreach (var t in newly) OnProofClaimed?.Invoke(module, t);
            return newly;
        }

        /// <summary>單場景測試（沒有存檔）時的錢包：給兌換所的貨幣登記表用。</summary>
        internal static bool TrySpendMem(ProofTier t, int n)
        {
            if (n < 0 || _memHeld[(int)t] < n) return false;
            _memHeld[(int)t] -= n;
            return true;
        }

        internal static void AddMem(ProofTier t, int n) => _memHeld[(int)t] = Mathf.Max(0, _memHeld[(int)t] + n);
    }
}
