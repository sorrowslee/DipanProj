using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Localization
{
    /// <summary>
    /// **「圖片型文字」的語系解析。** 字串走 <see cref="Language.GetText"/>，
    /// 但有些字是**畫進圖裡**的（標題、牌匾上的字、按鈕上的字）——那些沒辦法翻譯，
    /// 只能每種語言各出一張圖。這一支負責「同一個邏輯名稱 → 當前語言的那一張」。
    ///
    /// <b>資料夾長這樣</b>（<c>Assets/Resources/UI/Texts/</c>）：
    /// <code>
    /// UI/Texts/tw/ClearStagePanel_Title.png   ← 繁中（母版）
    /// UI/Texts/en/ClearStagePanel_Title.png   ← 英文（還沒畫就先不放）
    /// </code>
    ///
    /// <b>⚠ 同一張圖在每個語言資料夾裡必須「同名」</b>——不要加 <c>_tw</c> / <c>_en</c> 尾綴。
    /// 整套機制就是「換資料夾、不換檔名」，加了尾綴就得為每種語言各寫一次檔名對照，
    /// 這條規則也就沒有意義了。（`TitlePanel_TW` / `TitlePanel_EN` 原本就是這樣，
    /// 2026-08-19 一起改名成兩邊都叫 `TitlePanel_Title`。）
    ///
    /// <b>怎麼用</b>：呼叫端**照舊寫邏輯路徑** <c>"UI/Texts/ClearStagePanel_Title"</c>（不含語言資料夾），
    /// 由各面板的 <c>LoadSprite</c> / <c>UIBuilder.LoadSprite</c> 在載入前呼叫 <see cref="ResolveExisting"/>。
    /// 這樣「哪一種語言」只有這一支知道，之後加語言不用回頭改任何呼叫端。
    /// 全專案 7 支 <c>LoadSprite</c> 都已經接上，所以**任何面板都能直接載 UI/Texts/ 的圖**。
    ///
    /// ⚠ <b>面板是建一次就快取的</b>，圖在 OnBuild 當下就載定了。所以切語言要能生效，
    /// 靠的是 <c>UIManager</c> 訂閱 <c>Language.OnLanguageChanged</c> 後把面板全部丟掉重建——
    /// 不是靠這一支。新增「會顯示圖片型文字」的面板時不用做什麼，但**不要自己另外快取 Sprite**。
    ///
    /// <b>找不到就退回母版</b>（<see cref="MasterFolder"/>＝繁中）。所以英文版可以**一張一張慢慢補**，
    /// 沒畫的自動顯示中文，不會開天窗——這比「缺圖就變透明」好抓也好上線。
    /// </summary>
    public static class LocalizedArt
    {
        /// <summary>圖片型文字的根目錄（Resources 下）。路徑以這個開頭才會被語系改寫。</summary>
        public const string Root = "UI/Texts/";

        /// <summary>母版語言的資料夾。缺圖時一律退回這裡，所以**這個資料夾必須是最齊全的**。</summary>
        public const string MasterFolder = "tw";

        /// <summary>
        /// 語言 → 資料夾名。
        /// ⚠ 注意 <c>Lang.CN</c> 對應的資料夾叫 <b>tw</b> 不是 cn——語言表的欄位名是 <c>cn</c>，
        /// 但作者的圖片資料夾用 <c>tw</c>（內容是繁體中文）。兩邊命名不一致是既成事實，
        /// **這個對照表就是唯一的接縫**，不要在別處再寫一份。
        /// </summary>
        public static string FolderOf(Lang lang) => lang == Lang.EN ? "en" : MasterFolder;

        /// <summary>目前語言的資料夾名。</summary>
        public static string CurrentFolder => FolderOf(Language.Current);

        /// <summary>
        /// 把邏輯路徑改寫成當前語言的實際路徑：
        /// <c>"UI/Texts/Foo"</c> → <c>"UI/Texts/tw/Foo"</c>。
        ///
        /// **不是 <see cref="Root"/> 底下的路徑原樣回傳**，所以載圖函式可以無腦包一層，
        /// 不用去判斷這次載的是不是文字圖。已經自己帶了語言資料夾的路徑也原樣回傳
        /// （避免變成 <c>UI/Texts/tw/tw/Foo</c>）。
        /// </summary>
        public static string Resolve(string resourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath)) return resourcesPath;
            if (!resourcesPath.StartsWith(Root, System.StringComparison.Ordinal)) return resourcesPath;

            string rest = resourcesPath.Substring(Root.Length);
            if (rest.Length == 0) return resourcesPath;
            // 已經指名語言資料夾了（呼叫端硬要指定某語言）→ 不要再包一層
            if (rest.IndexOf('/') >= 0) return resourcesPath;

            return Root + CurrentFolder + "/" + rest;
        }

        /// <summary>
        /// 母版路徑（缺當前語言時的退路）。
        /// </summary>
        public static string MasterPath(string resourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath)) return resourcesPath;
            if (!resourcesPath.StartsWith(Root, System.StringComparison.Ordinal)) return null;
            string rest = resourcesPath.Substring(Root.Length);
            if (rest.Length == 0 || rest.IndexOf('/') >= 0) return null;
            return Root + MasterFolder + "/" + rest;
        }

        /// <summary>
        /// 解析成「實際存在的」路徑：當前語言有就用當前語言，沒有就回母版路徑。
        ///
        /// 給**自己已經有一條後備鏈**的載圖函式用（例如各面板的 <c>LoadSprite</c> 是
        /// 「先試 Sprite、再試 Texture2D、都沒有才警告」）——它們只需要一個最終路徑，
        /// 不需要本類別再幫忙載一次。不是文字圖的路徑原樣回傳。
        /// </summary>
        public static string ResolveExisting(string resourcesPath)
        {
            string p = Resolve(resourcesPath);
            if (ReferenceEquals(p, resourcesPath) || p == resourcesPath) return p;   // 不是 UI/Texts/ 底下的
            if (CurrentFolder == MasterFolder) return p;                             // 本來就是母版，沒得退

            if (Resources.Load<Sprite>(p) != null || Resources.Load<Texture2D>(p) != null) return p;

            string master = MasterPath(resourcesPath);
            if (string.IsNullOrEmpty(master)) return p;

            // ⚠ 只有「母版真的存在」才算是缺翻譯。兩邊都沒有＝這張圖根本不存在
            //   （例如某個 module 還沒畫關卡名圖），那是呼叫端要處理的事，
            //   在這裡喊「沒有 en 版」會誤導人去找一張本來就不該有的圖。
            if (Resources.Load<Sprite>(master) == null && Resources.Load<Texture2D>(master) == null) return p;

            WarnOnce(resourcesPath,
                $"[LocalizedArt]「{resourcesPath}」沒有 {CurrentFolder} 版，先用 {MasterFolder} 版頂著。");
            return master;
        }

        // ── 缺圖警告只印一次（不然每次開面板都洗版）──
        // static 集合，Domain Reload 已關 ⇒ 要註冊 PlayModeStaticReset，否則下一輪 Play 就不再提醒了。
        static readonly HashSet<string> _warned = new HashSet<string>();

        static void WarnOnce(string key, string msg)
        {
            if (!_warned.Add(key)) return;
            Debug.LogWarning(msg);
        }

        /// <summary>進 Play 時清「已警告過」的名單（Domain Reload 已關）。由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() => _warned.Clear();
    }
}
