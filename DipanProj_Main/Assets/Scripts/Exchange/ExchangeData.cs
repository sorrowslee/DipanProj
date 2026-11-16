using System;
using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;
using Dipan.Localization;
using Dipan.Save;

namespace Dipan.Exchange
{
    // ═══════════════════════════════════════════════════════════════════════
    //  稀有度（全遊戲通用定義，2026-11-15 定）：白 → 藍 → 黃 → 紫
    //  規格與配色的單一真相是這裡＋readme/EXCHANGE.md §2。
    //  目前只有兌換表（ExchangeTable.csv 的 Rarity 欄）在用；之後掉落、抽選、背包要顯示稀有度時，
    //  一律引用 ItemRarity / RarityInfo，不要再各自定義一套顏色。
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>物品稀有度。數字越大越稀有；0 ＝ 未指定（當成白）。</summary>
    public enum ItemRarity
    {
        White = 1,   // 白・凡品
        Blue = 2,    // 藍・良品
        Yellow = 3,  // 黃・珍品
        Purple = 4,  // 紫・神品
    }

    /// <summary>稀有度的顯示資訊：顏色、色名、品名、CSV 解析。</summary>
    public static class RarityInfo
    {
        public const ItemRarity Min = ItemRarity.White;
        public const ItemRarity Max = ItemRarity.Purple;

        /// <summary>依序列出四個稀有度（UI 建篩選鈕用）。</summary>
        public static readonly ItemRarity[] All = { ItemRarity.White, ItemRarity.Blue, ItemRarity.Yellow, ItemRarity.Purple };

        // 語言表：色名 10013–10016、品名 10017–10020（兌換系統段 10001–10099）
        const int TxtColorWhite = 10013, TxtGradeWhite = 10017;

        /// <summary>
        /// 稀有度色。這是**語意色**（給文字、細外框、小菱形用），不是面板材質色——
        /// UI 規範（UI_ICON_GUIDELINE §1.2）禁止紫色指的是面板與框體，稀有度紫是作者指定的例外，
        /// 只能以細線／文字／小色塊出現，不可大面積鋪色。
        /// </summary>
        public static Color ColorOf(ItemRarity r)
        {
            switch (r)
            {
                case ItemRarity.Blue: return new Color(0.40f, 0.62f, 0.92f);
                case ItemRarity.Yellow: return new Color(0.88f, 0.72f, 0.32f);
                case ItemRarity.Purple: return new Color(0.74f, 0.52f, 0.93f);
                default: return new Color(0.86f, 0.83f, 0.77f);   // 白：淡骨色（不用純白）
            }
        }

        /// <summary>色名（白／藍／黃／紫）。</summary>
        public static string ColorName(ItemRarity r) => Language.GetText(TxtColorWhite + Index(r));

        /// <summary>品名（凡品／良品／珍品／神品）。</summary>
        public static string GradeName(ItemRarity r) => Language.GetText(TxtGradeWhite + Index(r));

        /// <summary>「黃・珍品」這種完整標籤。</summary>
        public static string Label(ItemRarity r) => ColorName(r) + "・" + GradeName(r);

        static int Index(ItemRarity r) => Mathf.Clamp((int)Normalize(r) - 1, 0, 3);

        /// <summary>0 或超出範圍的值一律當白。</summary>
        public static ItemRarity Normalize(ItemRarity r)
            => (r < Min || r > Max) ? ItemRarity.White : r;

        /// <summary>
        /// 解析 CSV 的 Rarity 欄：接受數字 1–4、中文色名（白藍黃紫）、中文品名、英文色名。留空＝白。
        /// </summary>
        public static ItemRarity Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return ItemRarity.White;
            s = s.Trim();
            if (int.TryParse(s, out int n)) return Normalize((ItemRarity)n);
            switch (s.ToLowerInvariant())
            {
                case "白": case "凡品": case "white": case "w": return ItemRarity.White;
                case "藍": case "蓝": case "良品": case "blue": case "b": return ItemRarity.Blue;
                case "黃": case "黄": case "珍品": case "yellow": case "y": return ItemRarity.Yellow;
                case "紫": case "神品": case "purple": case "p": return ItemRarity.Purple;
            }
            Debug.LogWarning($"[RarityInfo] 看不懂的稀有度「{s}」，當成白。可用：1–4／白藍黃紫。");
            return ItemRarity.White;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  兌換分類（下拉選單的選項）
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>兌換面板的分類。All 只用在篩選，不會出現在資料上。</summary>
    public enum ExchangeCategory
    {
        All = 0,
        Bloodline,   // 血統（系列起始藥劑＋進階藥劑）
        Weapon,      // 武器
        Armor,       // 盔甲（胸甲／鞋子／手套）
        Amulet,      // 護身符
        Ring,        // 戒指
        Gem,         // 能力珠
        Consumable,  // 道具（消耗品、藥水）
        Material,    // 材料
        Other,       // 以上都不是
    }

    public static class ExchangeCategoryInfo
    {
        /// <summary>下拉選單的順序。</summary>
        public static readonly ExchangeCategory[] MenuOrder =
        {
            ExchangeCategory.All, ExchangeCategory.Bloodline, ExchangeCategory.Weapon, ExchangeCategory.Armor,
            ExchangeCategory.Amulet, ExchangeCategory.Ring, ExchangeCategory.Gem, ExchangeCategory.Consumable,
            ExchangeCategory.Material, ExchangeCategory.Other,
        };

        // 語言表 10003（全部）、10004–10012（血統…其他）
        public static string Name(ExchangeCategory c)
            => Language.GetText(c == ExchangeCategory.All ? 10003 : 10003 + (int)c);

        /// <summary>CSV 的 Category 欄：填英文分類名（大小寫不拘）；留空或看不懂就依 ItemTable 推斷。</summary>
        public static ExchangeCategory Parse(string s, ItemData d)
        {
            if (!string.IsNullOrWhiteSpace(s)
                && Enum.TryParse(s.Trim(), true, out ExchangeCategory c) && c != ExchangeCategory.All)
                return c;
            if (!string.IsNullOrWhiteSpace(s))
                Debug.LogWarning($"[ExchangeTable] 看不懂的分類「{s}」，改依物品表推斷。");
            return Infer(d);
        }

        /// <summary>依 ItemTable 的 Category／EquipSlot 推斷分類。</summary>
        public static ExchangeCategory Infer(ItemData d)
        {
            if (d == null) return ExchangeCategory.Other;
            if (d.IsBloodline) return ExchangeCategory.Bloodline;
            switch (d.EquipSlot)
            {
                case EquipSlot.Weapon: return ExchangeCategory.Weapon;
                case EquipSlot.Chest:
                case EquipSlot.Boots:
                case EquipSlot.Gloves: return ExchangeCategory.Armor;
                case EquipSlot.Amulet: return ExchangeCategory.Amulet;
                case EquipSlot.Ring: return ExchangeCategory.Ring;
            }
            if (d.IsGem) return ExchangeCategory.Gem;
            if (d.Category == "Consumable" || d.Category == "Potion") return ExchangeCategory.Consumable;
            if (d.Category == "Material") return ExchangeCategory.Material;
            return ExchangeCategory.Other;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  貨幣登記表
    //  現在只有「銅錢」真的能用（＝SaveManager 的金錢數字）。勳章、點數先登記名字與排序權重，
    //  等貨幣規範定案後在這裡補上 Held / TrySpend / Refund 三個委派就能用，兌換表與面板都不用改。
    // ═══════════════════════════════════════════════════════════════════════

    public sealed class ExchangeCurrency
    {
        public string Key;          // CSV 用的代號（copper／medal／point）
        public int NameLangId;      // 顯示名稱（語言表）
        public int IconItemId;      // 圖示借用哪個道具的 icon（0 ＝ 沒有，只顯示文字）
        public float SortWeight;    // 價格排序時換算成「銅錢等值」的倍率
        public Func<int> Held;            // 玩家持有多少；null ＝ 尚未開放
        public Func<int, bool> TrySpend;  // 扣款；不足回 false 且不扣
        public Action<int> Refund;        // 退款（多種貨幣一起付、後面那種失敗時把前面的退回去）

        public bool Available => Held != null && TrySpend != null;
        public string Name => Language.GetText(NameLangId);
        public int HeldNow => Held != null ? Held() : 0;
    }

    public static class ExchangeCurrencies
    {
        public const string Copper = "copper", Medal = "medal", Point = "point";

        static Dictionary<string, ExchangeCurrency> _map;

        static Dictionary<string, ExchangeCurrency> Map
        {
            get
            {
                if (_map != null) return _map;
                _map = new Dictionary<string, ExchangeCurrency>(StringComparer.OrdinalIgnoreCase);
                Add(new ExchangeCurrency
                {
                    Key = Copper, NameLangId = 10046, IconItemId = 101, SortWeight = 1f,   // 101 ＝ 銅錢（RunProgress.MoneyItemId）
                    Held = () => SaveManager.Instance != null ? SaveManager.Instance.Currency : 0,
                    TrySpend = amt => SaveManager.Instance != null && SaveManager.Instance.TrySpendCurrency(amt),
                    Refund = amt => { if (SaveManager.Instance != null) SaveManager.Instance.AddCurrency(amt); },
                });
                // ⏳ 之後的新貨幣：先登記，Held/TrySpend/Refund 留 null ＝ 尚未開放（面板會顯示「尚未開放」、不能兌換）。
                Add(new ExchangeCurrency { Key = Medal, NameLangId = 10047, SortWeight = 1000f });
                Add(new ExchangeCurrency { Key = Point, NameLangId = 10048, SortWeight = 1f });
                return _map;
            }
        }

        static void Add(ExchangeCurrency c) => _map[c.Key] = c;

        /// <summary>依代號取貨幣；找不到回 null。</summary>
        public static ExchangeCurrency Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return Map.TryGetValue(key.Trim(), out var c) ? c : null;
        }

        /// <summary>貨幣變動時通知 UI。目前只有銅錢會變，所以直接轉接 SaveManager 的事件。</summary>
        public static void Subscribe(Action onChanged)
        {
            if (SaveManager.Instance != null) SaveManager.Instance.OnCurrencyChanged += onChanged;
        }

        public static void Unsubscribe(Action onChanged)
        {
            if (SaveManager.Instance != null) SaveManager.Instance.OnCurrencyChanged -= onChanged;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  兌換表的一列
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>價格的一個組成：多少個哪種貨幣。一個品項可以要好幾種（例：勳章 2＋點數 300）。</summary>
    public struct CostPart
    {
        public string CurrencyKey;
        public int Amount;
        public ExchangeCurrency Currency => ExchangeCurrencies.Get(CurrencyKey);
    }

    public sealed class ExchangeEntry
    {
        public int ExchangeId;
        public int ItemId;
        public ExchangeCategory Category;
        public ItemRarity Rarity;
        public List<CostPart> Cost = new List<CostPart>();
        public int SortOrder;

        /// <summary>物品定義（ItemTable）。表載入時就查好；找不到的列會被略過，所以這裡不會是 null。</summary>
        public ItemData Item;

        public string Name => Item != null ? Item.Name : $"#{ItemId}";

        /// <summary>價格排序用的「銅錢等值」：每種貨幣 × 它的 SortWeight 加總。</summary>
        public float PriceSortKey
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < Cost.Count; i++)
                {
                    var c = Cost[i].Currency;
                    sum += Cost[i].Amount * (c != null ? c.SortWeight : 1f);
                }
                return sum;
            }
        }

        /// <summary>所有要付的貨幣都已開放（否則面板顯示「尚未開放」）。</summary>
        public bool CurrenciesAvailable
        {
            get
            {
                for (int i = 0; i < Cost.Count; i++)
                {
                    var c = Cost[i].Currency;
                    if (c == null || !c.Available) return false;
                }
                return true;
            }
        }
    }
}
