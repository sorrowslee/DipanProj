using System.Collections.Generic;

namespace Dipan.Inventory
{
    /// <summary>
    /// 鑲在孔位裡的一顆珠子。
    /// 刻意與「背包裡的一顆珠子」是同一組資料（物品 ID + 等級）——所以鑲上/拆下是**純搬移、不做任何轉換**，
    /// 珠子強化到幾級，拆下來就還是幾級。見 readme/GEM_SOCKET.md。
    /// </summary>
    public class GemRef
    {
        /// <summary>ItemTable 的珠子物品 ID。</summary>
        public int itemId;
        /// <summary>珠子等級（1~3）。</summary>
        public int level = 1;

        public GemRef Clone() => new GemRef { itemId = itemId, level = level };
    }

    /// <summary>一個鑲嵌孔。unlocked = false 代表這個孔沒開（介面上顯示鎖鏈）。</summary>
    public class SocketSlot
    {
        /// <summary>這個孔有沒有開。孔位是**隨機位置**開的，不保證是前面幾個。</summary>
        public bool unlocked;
        /// <summary>鑲在這個孔裡的珠子；null = 空孔。</summary>
        public GemRef gem;

        public bool IsEmpty => gem == null;

        public SocketSlot Clone() => new SocketSlot { unlocked = unlocked, gem = gem?.Clone() };
    }

    /// <summary>
    /// 裝備身上**內建的一條特殊效果**（品質系統，2026-11-17，見 readme/EQUIPMENT.md §5）。
    /// 效果種類跟鑲嵌珠是同一張 GemTable（所以算法、數值、說明文字全部共用），差別只在它長在裝備上、拿不下來。
    /// 一件裝備身上有幾條＝它的品質（0 一般／1 良品／2 珍品／3 極品），**品質不另外存**。
    /// </summary>
    public class Affix
    {
        /// <summary>效果 ID ＝ GemTable 的 GemID（**不是**珠子的物品 ID）。</summary>
        public int effectId;
        /// <summary>這一條的等級（單一來源 1~3）。全身同種效果的等級會相加。</summary>
        public int level = 1;

        public Affix Clone() => new Affix { effectId = effectId, level = level };
    }

    /// <summary>
    /// 「玩家手上這一件」的額外資料。
    ///
    /// 【核心觀念】CSV 表（ItemTable / WeaponTable / RecipeTable）只是**模板**；玩家實際持有的那一件
    /// ＝ 模板 + 這個物件。孔數、鑲了哪些珠子、珠子幾級…這些每一件都不同的東西全放在這裡，
    /// 跟著角色存檔走。
    ///
    /// 【擴充規則】之後裝備要多屬性（附魔、耐久、詞綴、改造次數…）就在這個類別**加欄位**：
    ///   純新增欄位 → Newtonsoft 對缺欄給型別預設 → 舊存檔照樣讀得起來，不需要寫遷移。
    ///   會壞掉的只有「改掉既有欄位的語意」（例如把 sockets 換成別的結構），那才需要遷移。
    ///
    /// 【重要】有實例資料的物品**一律不疊**（見 <see cref="InventorySystem.AddItem"/>）——
    /// 因為兩件的實例內容不同，疊在一起就分不出來了。
    /// </summary>
    public class ItemInstance
    {
        /// <summary>鑲嵌孔的固定格數（介面左三右三）。</summary>
        public const int SocketMax = 6;

        /// <summary>珠子的等級（1~3）。不是珠子就是 0。</summary>
        public int level;

        /// <summary>鑲嵌孔；裝備才有（固定 <see cref="SocketMax"/> 格，沒開的 unlocked = false）。非裝備 = null。</summary>
        public List<SocketSlot> sockets;

        /// <summary>
        /// 內建特殊效果（品質系統）；裝備才有，最多 <see cref="AffixMax"/> 條、同一種效果不重複。
        /// null 或空＝一般。舊存檔沒有這個欄位 → 讀進來是 null ＝ 一般，不用遷移。
        /// </summary>
        public List<Affix> affixes;

        /// <summary>一件裝備最多幾條內建效果（＝最高品質「極品」）。</summary>
        public const int AffixMax = 3;

        // ───────────── 查詢 ─────────────

        /// <summary>內建效果條數（＝品質減一）。</summary>
        public int AffixCount => affixes != null ? affixes.Count : 0;

        /// <summary>這一件身上有沒有某種內建效果（GemTable 的 GemID）。</summary>
        public bool HasAffix(int effectId)
        {
            if (affixes == null) return false;
            for (int i = 0; i < affixes.Count; i++) if (affixes[i] != null && affixes[i].effectId == effectId) return true;
            return false;
        }

        public bool HasSockets => sockets != null && sockets.Count > 0;

        /// <summary>開了幾個孔。</summary>
        public int UnlockedCount
        {
            get
            {
                if (sockets == null) return 0;
                int n = 0;
                for (int i = 0; i < sockets.Count; i++) if (sockets[i] != null && sockets[i].unlocked) n++;
                return n;
            }
        }

        /// <summary>目前鑲了幾顆珠子。</summary>
        public int GemCount
        {
            get
            {
                if (sockets == null) return 0;
                int n = 0;
                for (int i = 0; i < sockets.Count; i++) if (sockets[i] != null && sockets[i].gem != null) n++;
                return n;
            }
        }

        public bool IsUnlocked(int i)
            => sockets != null && i >= 0 && i < sockets.Count && sockets[i] != null && sockets[i].unlocked;

        public GemRef GemAt(int i)
            => (sockets != null && i >= 0 && i < sockets.Count && sockets[i] != null) ? sockets[i].gem : null;

        /// <summary>
        /// 把珠子放進第 i 個孔（越界回 false）。
        /// 這裡刻意**只檢查越界、不檢查孔有沒有開**——「孔開了沒」的把關統一在 <see cref="ForgeSockets"/>／
        /// 孔位面板那一層，這樣開發用的 ForgeSockets.DebugCount 才有辦法強制開孔測試。
        /// </summary>
        public bool SetGem(int i, GemRef gem)
        {
            if (sockets == null || i < 0 || i >= sockets.Count) return false;
            if (sockets[i] == null) sockets[i] = new SocketSlot();
            sockets[i].gem = gem;
            return true;
        }

        /// <summary>取出第 i 個孔的珠子（連同把孔清空）。沒有就回 null。</summary>
        public GemRef TakeGem(int i)
        {
            var g = GemAt(i);
            if (g != null) sockets[i].gem = null;
            return g;
        }

        // ───────────── 建立 ─────────────

        /// <summary>依「哪幾個孔有開」的佈局建立一份裝備實例。layout 長度應為 <see cref="SocketMax"/>。</summary>
        public static ItemInstance FromSocketLayout(bool[] layout)
        {
            var inst = new ItemInstance { sockets = new List<SocketSlot>(SocketMax) };
            for (int i = 0; i < SocketMax; i++)
                inst.sockets.Add(new SocketSlot { unlocked = layout != null && i < layout.Length && layout[i] });
            return inst;
        }

        /// <summary>建立一顆珠子的實例（只有等級）。</summary>
        public static ItemInstance ForGem(int level) => new ItemInstance { level = level };

        public ItemInstance Clone()
        {
            var c = new ItemInstance { level = level };
            if (sockets != null)
            {
                c.sockets = new List<SocketSlot>(sockets.Count);
                for (int i = 0; i < sockets.Count; i++)
                    c.sockets.Add(sockets[i] != null ? sockets[i].Clone() : new SocketSlot());
            }
            if (affixes != null)
            {
                c.affixes = new List<Affix>(affixes.Count);
                for (int i = 0; i < affixes.Count; i++)
                    if (affixes[i] != null) c.affixes.Add(affixes[i].Clone());
            }
            return c;
        }

        /// <summary>
        /// 這份實例是不是「空的」（沒有任何值得保存的內容）。
        /// 用來避免給一般可疊道具掛上無意義的實例、讓它們失去疊堆能力。
        /// </summary>
        public bool IsBlank => level <= 0 && !HasSockets && AffixCount == 0;
    }
}
