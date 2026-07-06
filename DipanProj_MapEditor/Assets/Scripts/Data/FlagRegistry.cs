using System.Collections.Generic;

namespace DipanMapEditor.Data
{
    /// <summary>
    /// 一個具名旗標的定義：名稱 + 生命週期。存進全域 flags.json（見 IO/FlagRegistryStore）。
    /// 生命週期是旗標「自己的屬性」——觸發點只存名字，遊戲端查這份登記表決定 周目/永久，
    /// 所以之後在管理器改一次生命週期，所有用到它的觸發點都跟著改（單一來源）。
    /// </summary>
    public class FlagDef
    {
        public int id;                      // 系統自動配置的唯一編號（觸發點用 id 查表填名稱）
        public string name;
        public string scope = ScopeCycle;   // "cycle"（周目，輪迴清）| "life"（永久，跨輪迴）
        public string note;                 // 備註（可選，只在編輯器顯示）

        public const string ScopeCycle = "cycle";
        public const string ScopeLife = "life";

        public bool IsLife => scope == ScopeLife;
        /// <summary>生命週期的中文顯示（管理器切換鈕用）。</summary>
        public string ScopeLabel => IsLife ? "永久" : "周目";
    }

    /// <summary>全域旗標登記表（所有地圖共用；旗標本來就跨地圖存在存檔裡）。</summary>
    public class FlagRegistry
    {
        public List<FlagDef> flags = new List<FlagDef>();

        public FlagDef Find(string name) => string.IsNullOrEmpty(name) ? null : flags.Find(f => f.name == name);
        public FlagDef FindById(int id) => id <= 0 ? null : flags.Find(f => f.id == id);
        public bool Contains(string name) => Find(name) != null;
        public bool IsLife(string name) { var f = Find(name); return f != null && f.IsLife; }

        int NextId()
        {
            int max = 0;
            foreach (var f in flags) if (f.id > max) max = f.id;
            return max + 1;
        }

        /// <summary>新增一個旗標（已存在則不重複），自動配 id。回傳是否真的加了。</summary>
        public bool Add(string name, string scope = FlagDef.ScopeCycle)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || Contains(name)) return false;
            flags.Add(new FlagDef { id = NextId(), name = name, scope = scope });
            return true;
        }

        /// <summary>補齊 id（舊檔沒有 id、或 id 重複時），確保每個旗標都有唯一 id。載入後呼叫一次。</summary>
        public void NormalizeIds()
        {
            var used = new HashSet<int>();
            foreach (var f in flags) if (f.id > 0 && !used.Add(f.id)) f.id = 0;   // 重複的清掉重配
            foreach (var f in flags) if (f.id <= 0) { f.id = NextId(); used.Add(f.id); }
        }

        /// <summary>依名稱排序（管理器顯示用）。id 不受影響。</summary>
        public void SortByName() => flags.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
    }
}
