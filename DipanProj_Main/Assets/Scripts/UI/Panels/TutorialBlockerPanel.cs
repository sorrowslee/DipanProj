using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 新手教學遮罩：蓋一層看不見的全螢幕擋板，把所有點擊吃掉——只留「指定的那個 UI 元件」可以點。
    /// 作法：擋板在覆蓋層（最上層之一），要放行的目標暫時掛一個更高 sortingOrder 的 Canvas＋Raycaster，
    /// 讓它浮到擋板之上仍可點；其餘面板都在擋板底下、點不到。解除時把那個暫時 Canvas 拿掉。
    /// </summary>
    public class TutorialBlockerPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Overlay;
        public override bool BlocksGameplayInput => false;   // 遊戲此時已被劇本/背包面板暫停
        public override bool PausesGame => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;

        Image _blocker;
        GameObject _raised;
        Canvas _raisedCanvas;
        GraphicRaycaster _raisedRc;
        bool _raisedHadCanvas;

        protected override void OnBuild()
        {
            _blocker = UIBuilder.SolidPanel(transform, "Blocker", new Color(0f, 0f, 0f, 0.001f));
            _blocker.raycastTarget = true;   // 吃掉所有點擊
        }

        /// <summary>開啟遮罩並只放行 allowed 這個元件（傳 null＝全擋）。</summary>
        public static void LockTo(GameObject allowed)
        {
            var p = UIManager.Instance?.Open<TutorialBlockerPanel>();
            p?.SetAllowed(allowed);
        }

        public static void Unlock() => UIManager.Instance?.Close<TutorialBlockerPanel>();

        public void SetAllowed(GameObject allowed)
        {
            if (allowed == _raised) return;   // 已經鎖在同一個元件 → 不重做（每幀重做會閃爍）
            Restore();
            if (allowed == null) return;

            _raised = allowed;
            _raisedCanvas = allowed.GetComponent<Canvas>();
            _raisedHadCanvas = _raisedCanvas != null;
            if (_raisedCanvas == null) _raisedCanvas = allowed.AddComponent<Canvas>();
            _raisedCanvas.overrideSorting = true;
            _raisedCanvas.sortingOrder = 450;   // 高於覆蓋層(300) → 浮在擋板之上仍可點
            _raisedRc = allowed.GetComponent<GraphicRaycaster>();
            if (_raisedRc == null) _raisedRc = allowed.AddComponent<GraphicRaycaster>();
        }

        protected override void OnClose() => Restore();

        void Restore()
        {
            if (_raised == null) return;
            if (_raisedRc != null) { Destroy(_raisedRc); _raisedRc = null; }
            if (!_raisedHadCanvas && _raisedCanvas != null) Destroy(_raisedCanvas);
            _raisedCanvas = null; _raised = null; _raisedHadCanvas = false;
        }
    }
}
