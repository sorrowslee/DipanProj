using UnityEngine;

namespace Dipan.Diagnostics
{
    /// <summary>
    /// 【POC・可整支刪除】角色場景融合技術可行性測試（2026-09-02）。
    ///
    /// 要回答的唯一問題：**透過 runtime rendering，角色能不能有效融進「血狂之爭」這張場景？**
    /// 不是正式架構——確認有效之後才會另外規劃通用的 Character Environment System。
    ///
    /// ── 怎麼用 ──
    /// 遊戲中按 <b>P</b> 開效能面板 → 按 <b>G</b> 循環（<b>Shift+G</b> 反向），面板上顯示目前是哪一種：
    ///   0 原狀     ：完全現狀（本類別的所有效果都不作用，逐位元等於加這功能之前）
    ///   1 色彩     ：角色色彩處理（黑階抬升為主、環境色與亮部抬升為輔）
    ///   2 色彩+邊緣：再加邊緣融合
    ///
    /// ⚠ **原方案的 Test A（腳下接觸陰影）已從這個 POC 移除、影子相關改動全部還原。**
    ///   做的時候發現真正的問題在影子的**定位**：`BlobShadow` 靠掃 alpha 猜「腳在哪」，
    ///   而長袍、披風、背包、爪子這些突出物會把猜測帶偏，不同血統的服裝差異又大到
    ///   沒有一種猜法能全中（試過四種，每種都有反例）。結論是這件事該走專案的資料驅動鐵則
    ///   ——每個角色一組偏移值調一次到位——而不是靠程式猜。待日後另案處理，
    ///   過程與教訓見 readme/PROBLEMS.md **E28**、缺口記在 readme/TODO.md。
    ///
    /// ── 這套東西掛在哪 ──
    /// 刻意**沒有註冊表、沒有 event**：本類別只維護一個 <see cref="Version"/> 版本號，
    /// 由 <c>HitReactionHandler</c> 在自己的 Update 比對版本號後拉參數。
    /// 每幀一次 int 比較，零成本；要拔除這個 POC 只要刪掉這支檔案，
    /// 再把 HitReactionHandler 與 PerfHud 裡標了【POC】的幾小段拿掉即可。
    ///
    /// ── ⚠⚠ 所有數值都是 Linear 空間 ──
    /// 專案跑 Linear 色彩空間（見 readme/PROBLEMS.md <b>E11</b>／<b>E26</b>）。
    /// 室內石材場景的 linear 亮度**整張擠在 0.02~0.20、中位數只有 0.083**——
    /// 照 sRGB 直覺填「0.5＝一半亮」那種門檻，結果是效果完全不參與，
    /// **症狀是「好像沒做」而不是報錯**。Atmosphere 的室內系 16/17 為此白調過一輪。
    /// 要重定數值請照 E26 的方法量一張實機截圖的 linear percentile，不要用眼睛估。
    /// </summary>
    public static class CharacterEnvPoc
    {
        public enum Mode
        {
            Original = 0,   // 完全現狀
            EnvTint,        // 色彩處理
            Full            // 色彩處理 ＋ 邊緣融合
        }

        /// <summary>目前模式。改它請走 <see cref="Cycle"/>／<see cref="SetMode"/>，否則版本號不會動、沒人會更新。</summary>
        public static Mode Current { get; private set; } = Mode.Original;

        /// <summary>每次模式變更 +1。使用端拿它跟自己看過的值比對，不同才重新拉參數。</summary>
        public static int Version { get; private set; } = 1;

        public static void Cycle() => SetMode((Mode)(((int)Current + 1) % 3));

        /// <summary>反向循環。要來回比對相鄰兩個模式（例如 0 ↔ 1 只差接觸陰影）時，
        /// 正向繞一圈要按四次，中間會被別的模式干擾判斷。</summary>
        public static void CycleBack() => SetMode((Mode)(((int)Current + 2) % 3));

        public static void SetMode(Mode m)
        {
            if (Current == m) return;
            Current = m;
            Version++;
        }

        public static string ModeName()
        {
            switch (Current)
            {
                case Mode.EnvTint: return "1 色彩";
                case Mode.Full:    return "2 色彩+邊緣";
                default:           return "0 原狀";
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  色彩參數（Test B / C）
        // ══════════════════════════════════════════════════════════════
        //
        // 色值沿用 Atmosphere.shader 的 **mode 17「莊嚴金輝」**那一組常數（2026-09-02 作者選定 17：
        // 16 的 bloomK 是 0.70、實機看起來「有種朦朧的感覺」，17 只有 0.15 所以清晰得多）。
        // 讓角色跟場景吃同一組色，天然一致；日後場景換氛圍，這裡跟著換同一組就好。
        //
        // ⚠ baseTint/litTint 是**乘法係數不是顏色**：一定要走 SetVector，不能 SetColor
        //   （Linear 專案下 SetColor 會做一次 gamma→linear 轉換，把 1.18 這種係數直接扭掉）。
        // ⚠ 17 跟 16 的性格完全不同：16 是「整張都暖」，**17 是「暗部偏冷、亮部淡金」**——
        //   所以 EnvBase 是冷藍的，別看到 0.86/0.92/1.10 以為填反了。

        static readonly Vector4 EnvBase = new Vector4(0.86f, 0.92f, 1.10f, 1f);  // 暗側乘法色（= mode 17 baseTint，冷藍）
        static readonly Vector4 EnvLit  = new Vector4(1.18f, 1.06f, 0.78f, 1f);  // 亮側乘法色（= mode 17 litTint，金）
        const float EnvPivot = 0.140f;   // 翻色分界（= mode 17 pivot；放在中間調之上＝只有亮部才轉金）
        const float EnvSplit = 0.050f;   // 過渡帶半寬（= mode 17 split；小＝曲線陡＝冷暖分離明顯）

        /// <summary>環境色強度。場景的 mode 16 已經對整個畫面（含角色）套過一次暖色，這裡是**再補一層**，所以要輕。</summary>
        const float EnvMix = 0.35f;

        // ══════════════════════════════════════════════════════════════
        //  黑階抬升 ← **本 POC 的主角**
        // ══════════════════════════════════════════════════════════════
        //
        // 全螢幕後處理對角色與場景一視同仁，永遠不會改變「角色暗部比場景暗多少」，
        // 而那個相對差正是「像貼在背景上」的主因；只能在角色自己的 sprite 上動。
        //
        // ⚠ **抬升量不能是固定常數，必須跟著場景亮度走。** 2026-09-02 兩張圖實測：
        //     暗場景（Atmosphere=16 的石材大廳，linear p50 = 0.078）：角色暗部比場景中位暗 15.3 倍
        //     亮場景（米白石材＋金裝飾，       linear p50 = 0.220）：暗 **46.6 倍**
        //   同一個 0.008 在暗場景能把落差壓到 6.7 倍，在亮場景只到 20.9——場景越亮，
        //   同樣的死黑越突兀，需要的抬升量也越大。所以改成由下面兩個**有物理意義**的數字推算。

        /// <summary>
        /// 目標場景的 linear 亮度中位數。**要照 PROBLEMS E26 的方法量實機截圖**，不要用眼睛估
        /// （截圖上看起來 0.5 的中間灰，linear 可能只有 0.2）。
        /// 目前值＝血狂之爭大廳**套上 Atmosphere 17 之後**量到的 0.429。
        /// （同一張圖：Atmosphere=1 時是 0.220、16 是 0.279、17 是 **0.429**——17 的 dim=1.0 不壓暗、
        ///  又有 stoneLift=0.20 提亮石材，所以亮得多，角色的抬升量也要跟著加倍。）
        /// 換場景或換氛圍就照 E26 重量一次，改這個數字即可。
        /// </summary>
        const float SceneLuma = 0.429f;

        /// <summary>
        /// 目標落差倍數：角色最暗處（p5）**至少**要抬到場景中位的 1/N。
        /// N 越小＝角色越亮越融入、但黑色越容易變灰（角色會開始失去重量感）。
        /// 14 是首次實測後訂的折衷值——暗場景那張在 6.7 倍時已經明顯改善，
        /// 但亮場景直接壓到同一個倍數會把褲子整片洗成灰，所以先取中間。
        /// </summary>
        const float TargetDarkRatio = 14f;

        /// <summary>實際抬升量（linear）。目前 ≈ 0.0306。</summary>
        const float BlackLift = SceneLuma / TargetDarkRatio;

        /// <summary>
        /// 抬升量的**色向量**（歸一化：三通道平均 = 1，所以換色不改變抬升的總亮度）。
        ///
        /// ⚠ 首版用中性灰加法是**方向錯的**：實測角色暗部的 R/B 從 1.81 掉到 1.51，
        ///   而場景暗部是暖褐色（實機量到最暗帶 R/B ≈ 1.93、歸一色 1.34/0.97/0.70），
        ///   等於角色暗部被拉得比場景更冷、**色相上反而更不融入**。
        ///   這裡直接用量到的場景暗部顏色（Atmosphere 17 下實測 R/B = 2.04、歸一 1.361/0.972/0.667）
        ///   ——換場景或換氛圍時照 E26 重量一次即可。
        ///   有趣的是這個值跟 Atmosphere=1 時量到的幾乎一樣：17 的 baseTint 雖然是冷藍的，
        ///   但石材素材本身夠暖，最終的暗部仍然偏暖。**所以要量最終畫面，不能只看 shader 常數。**
        /// </summary>
        static readonly Vector4 LiftTint = new Vector4(1.361f, 0.972f, 0.667f, 1f);

        /// <summary>飽和微調（負＝去飽和，往場景收斂的調性靠）。mode 16 已做 desat 0.05，這裡只再補一點點。</summary>
        const float SatDelta = -0.04f;

        /// <summary>
        /// 亮部抬升：把角色亮部推過 Atmosphere bloom 的抽取門檻（**0.09 linear**）。
        /// 假設是：mode 16 的 bloomK = 0.70（四種室內氛圍最強），場景亮面都在發光瀰漫，
        /// 而角色若整體偏暗就完全不參與 bloom → 角色成了畫面上唯一的硬邊，這才是「剪下來貼上去」的大宗。
        /// 光暈長在角色**外面**，角色本體不會變糊（不違反「不可 blur 角色」的紅線）。
        ///
        /// ⚠ **這個假設實測被證偽**：白外套本來就有 15.9% 的像素過門檻，角色一直都有參與 bloom。
        ///   而且選定的 mode 17 的 bloomK 只有 0.15（16 是 0.70），光暈本來就很淡。
        ///   所以這個參數現在的實際作用只剩「把角色亮部提亮」，從 0.22 降到 0.10——
        ///   場景已經有 90% 的像素過門檻、本身就很亮，角色需要的是跟上整體亮度，不是額外的光暈。
        /// </summary>
        const float LumBoost = 0.10f;

        /// <summary>邊緣融合色（真顏色，走 SetColor 讓 Unity 自動轉 linear）。暖石材色，讓最外圈沾到環境光。</summary>
        static readonly Color EdgeTint = new Color32(0xC6, 0xAA, 0x82, 0xFF);
        const float EdgeAmount = 0.25f;

        // ══════════════════════════════════════════════════════════════
        //  對外查詢
        // ══════════════════════════════════════════════════════════════

        /// <summary>色彩處理有沒有開（Test B 起）。</summary>
        public static bool ColorEnabled => Current >= Mode.EnvTint;

        /// <summary>邊緣融合有沒有開（只有 Test C）。</summary>
        public static bool EdgeEnabled => Current >= Mode.Full;

        /// <summary>
        /// 把本模式的色彩參數寫進 <paramref name="mpb"/>。
        ///
        /// ⚠ 呼叫端**必須把這些跟自己的其他屬性寫在同一個 MaterialPropertyBlock 裡**：
        ///   <c>SetPropertyBlock</c> 是整包覆蓋的，分兩次寫會互相洗掉
        ///   （角色一挨打，受擊白閃那次的 block 就會把色彩處理沖掉一瞬間）。
        /// </summary>
        public static void FillPropertyBlock(MaterialPropertyBlock mpb)
        {
            if (mpb == null) return;

            if (!ColorEnabled)
            {
                mpb.SetFloat("_EnvOn", 0f);   // shader 端整段跳過 = 原本的畫面
                return;
            }

            mpb.SetFloat("_EnvOn", 1f);
            mpb.SetVector("_EnvBase", EnvBase);
            mpb.SetVector("_EnvLit", EnvLit);
            mpb.SetFloat("_EnvPivot", EnvPivot);
            mpb.SetFloat("_EnvSplit", EnvSplit);
            mpb.SetFloat("_EnvMix", EnvMix);
            mpb.SetFloat("_BlackLift", BlackLift);
            mpb.SetVector("_LiftTint", LiftTint);
            mpb.SetFloat("_Sat", SatDelta);
            mpb.SetFloat("_LumBoost", LumBoost);
            mpb.SetColor("_EdgeTint", EdgeTint);
            mpb.SetFloat("_EdgeAmount", EdgeEnabled ? EdgeAmount : 0f);
        }
    }
}
