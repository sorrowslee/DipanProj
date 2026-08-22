using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 全遊戲統一的「Skip」字樣（右上角、白色粗體、黑外框、無底板）。**樣式的單一真相。**
    ///
    /// 目前的使用者：
    ///   ‧ 序章漫畫／墜落（<c>IntroComicController</c>，開發階段限定）
    ///   ‧ 頭像對話（<see cref="TalkPanel"/>，一句以上且該劇情點允許時）
    ///   ‧ 地圖內劇情演出（<c>CutsceneDirector</c>，該段勾了「可略過」時）
    ///
    /// ⚠ 要改外觀（字級、位置、顏色）就改這裡，不要在呼叫端各改一份——
    /// 這支存在的理由就是「三個地方的 Skip 看起來要一模一樣」。
    /// 座標以 1920×1080 為基準（三個呼叫端的 CanvasScaler 都是這個參考解析度）。
    /// </summary>
    public static class SkipButton
    {
        // ── 樣式常數（改這裡＝三處一起改）──
        public const int FontSize = 78;
        static readonly Vector2 Offset = new Vector2(-52f, -40f);    // 距右上角
        static readonly Vector2 HitSize = new Vector2(380f, 130f);   // 點擊範圍（字本身靠右上對齊）
        static readonly Color TextColor = new Color(1f, 1f, 1f, 0.92f);
        static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.85f);
        static readonly Vector2 OutlineDist = new Vector2(3f, -3f);

        /// <summary>
        /// 在 <paramref name="parent"/> 右上角建一個「Skip」文字。
        /// <paramref name="clickable"/>＝true 時同時掛 Button（需要父層有 GraphicRaycaster）；
        /// false 則只是純顯示（呼叫端自己用滑鼠座標判定，序章就是這樣做的）。
        /// </summary>
        public static Text Create(Transform parent, System.Action onClick = null, bool clickable = true)
        {
            var go = new GameObject("Skip", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);   // 右上角
            rt.anchoredPosition = Offset;
            rt.sizeDelta = HitSize;

            var t = go.AddComponent<Text>();
            t.font = UIBuilder.DefaultFont;
            t.fontSize = FontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.UpperRight;
            t.color = TextColor;
            t.text = "Skip";
            t.raycastTarget = clickable;   // 純顯示時不要擋住底下的點擊

            var outline = go.AddComponent<Outline>();
            outline.effectColor = OutlineColor;
            outline.effectDistance = OutlineDist;

            if (clickable && onClick != null)
            {
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = t;   // 程式建按鈕要手動指 targetGraphic（見 readme/PROBLEMS.md D4）
                btn.onClick.AddListener(() => onClick());
            }
            return t;
        }

        /// <summary>
        /// 建一個「只有 Skip 的獨立覆蓋層」（自帶 Canvas＋Scaler＋Raycaster），回傳整個 GameObject。
        /// 給不是 <see cref="UIPanel"/> 的表演用（劇情演出）。<paramref name="sortingOrder"/> 決定它蓋在誰之上——
        /// 排序帶的配置見 readme/UI_SYSTEM.md 的分層表。
        /// </summary>
        public static GameObject CreateOverlay(string name, int sortingOrder, System.Action onClick)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            Create(go.transform, onClick, clickable: true);
            return go;
        }
    }
}
