using UnityEngine;

namespace Dipan.Drama
{
    /// <summary>
    /// 一張立繪的「排版資料」：<b>畫面上的人物</b>在圖檔裡的實際位置與大小，加上這張圖的固定微調。
    ///
    /// <para><b>為什麼需要它</b>：立繪的圖檔外框 ≠ 人物。同一批素材裡，不透明內容佔畫布的比例
    /// 從 0.717 到 1.000 都有、左右留白 0~16%、畫布比例也不統一（1024×1536 之外還有 1122×1402、1149×1369…）。
    /// 舊做法拿「圖檔外框」對齊（高度固定、寬度＝高×圖檔長寬比、左立繪貼圖檔左緣），
    /// 於是換一張圖＝換一組留白，人物就在畫面上飄。<b>改成拿「內容框」對齊之後，留白多寡與畫布大小都被吸收掉</b>。
    /// 見 readme/DRAMA.md〈立繪自動對齊〉。</para>
    ///
    /// <para><b>內容框哪裡來</b>：<see cref="Dipan.MapRuntime.MapSpriteLoader.GetAlphaLocalBox"/>——
    /// 地上物碰撞用的同一支（掃不透明像素的外接框、依 catalog id 快取）。刻意不另外烘進 catalog.json：
    /// 這個專案的 catalog 有四個產生器（兩支 C#、兩支 shell），只有部分會烘，
    /// 走同一支現成的掃描就不會有「不同機器算出不同結果」的問題。</para>
    ///
    /// <para><b>單位</b>：<see cref="canvas"/> / <see cref="content"/> / <see cref="center"/> 三者同單位
    /// （目前是世界單位，因為來源如此），排版時只用它們的<b>比值</b>，所以單位是什麼都不影響結果。</para>
    /// </summary>
    public struct PortraitFit
    {
        /// <summary>內容框有效（圖載得到、不是整張全透明）。false 時排版退回「用圖檔外框」的舊行為。</summary>
        public bool ok;

        /// <summary>整張圖檔的尺寸。</summary>
        public Vector2 canvas;

        /// <summary>不透明內容的尺寸（＝畫面上人物的實際大小）。</summary>
        public Vector2 content;

        /// <summary>內容框中心相對「圖檔中心」的位移（+X 右、+Y 上）。</summary>
        public Vector2 center;

        /// <summary>這張圖的固定縮放（<see cref="PortraitTable"/>，1 = 不變）。</summary>
        public float imgScale;

        /// <summary>這張圖的固定位移（<see cref="PortraitTable"/>，畫面單位，+X 右、+Y 上）。</summary>
        public Vector2 imgOffset;

        public static PortraitFit None => new PortraitFit { ok = false, imgScale = 1f, imgOffset = Vector2.zero };

        /// <summary>內容右緣相對圖檔中心的距離（+ = 在右）。右立繪鏡像後，它會變成畫面上的左緣——
        /// 所以左右兩側都是用這一個值對齊「朝向畫面中央的那一邊」。</summary>
        public float ContentRightFromCenter => center.x + content.x * 0.5f;

        /// <summary>內容底緣相對圖檔中心的距離（+ = 在上，一般為負值）。</summary>
        public float ContentBottomFromCenter => center.y - content.y * 0.5f;
    }
}
