namespace Dipan.MapRuntime
{
    /// <summary>
    /// 地圖素材「分類白名單」的<b>單一事實來源</b>（哪些資料夾會被同步進 StreamingAssets/MapAssets 並收進 catalog）。
    ///
    /// <para><b>為什麼要收斂成一份</b>：這份清單原本在遊戲端寫了三次
    /// （<c>MapIO.BuildFromGameAssets</c>、<c>MapAssetSyncTool</c>、<c>Tools/sync_map_assets.sh</c>），
    /// 加一個分類要改三處、漏改不會報錯只會「靜默少同步」。
    /// readme/PROBLEMS.md 的 C1／C3／C5／I4／F16 都是這條線長出來的坑。
    /// 兩處 C# 已於 2026-07-27 改為引用本檔；shell 版仍是獨立實作（見下）。</para>
    ///
    /// <para><b>改分類時要同步的地方（目前共 2 處）</b>：
    /// ① 本檔的 <see cref="All"/>（兩支 C# 自動跟著變）；
    /// ② <c>DipanProj_Main/Tools/sync_map_assets.sh</c> 的 <c>CATS=(...)</c>（獨立的 shell/python 實作，改不動它時至少要記得同步）。</para>
    ///
    /// <para><b>編輯器專案不在此列</b>：<c>DipanProj_MapEditor</c> 的 <c>AssetSyncTool</c> 只同步
    /// <see cref="Environment"/> / <see cref="Tiles"/> / <see cref="Background"/> ——這是<b>刻意</b>的，
    /// 因為 <see cref="Drama"/> / <see cref="Talk"/> 是遊戲端的劇情大圖與對話立繪，
    /// 地圖編輯器的素材調色盤用不到。<b>不要「順手幫它補上」。</b></para>
    /// </summary>
    public static class MapAssetCategories
    {
        /// <summary>地上物（直接放的單張＝靜態物件；子資料夾＝一個動畫地上物，多幀收成一筆）。</summary>
        public const string Environment = "Environment";
        /// <summary>地磚。</summary>
        public const string Tiles = "Tiles";
        /// <summary>背景大圖。</summary>
        public const string Background = "Background";
        /// <summary>劇情大圖（DramaPanel 的 Type 1）。</summary>
        public const string Drama = "Drama";
        /// <summary>對話立繪（TalkPanel 的 Type 2）。允許每個 NPC 一個子資料夾，見 <see cref="IsRecursive"/>。</summary>
        public const string Talk = "Talk";

        /// <summary>會被掃描並收進 catalog 的分類（順序不影響行為）。</summary>
        public static readonly string[] All = { Environment, Tiles, Background, Drama, Talk };

        /// <summary>
        /// 這個分類是否要<b>遞迴</b>收子資料夾裡的 PNG。
        /// <para>目前只有 <see cref="Talk"/> 要（允許「每個 NPC 一個子資料夾」，見 readme/PROBLEMS.md C5）。
        /// 其餘一律只收第一層——特別是 <see cref="Environment"/>，它的子資料夾另有「動畫地上物」語意，
        /// 遞迴收會把動畫幀打散成一堆獨立靜態物件，<b>不能混用</b>。</para>
        /// </summary>
        public static bool IsRecursive(string category) => category == Talk;
    }
}
