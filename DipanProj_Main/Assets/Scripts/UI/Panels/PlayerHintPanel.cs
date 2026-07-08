using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 玩家提示圖（覆蓋層、不擋輸入、不暫停）。**通用元件**：在玩家頭上「左上／右上」各擺一張提示圖，
    /// 可指定哪張閃爍；到達「收起時機」（玩家移動／攻擊／按任意鍵）就自動收起並回呼。
    ///
    /// 給新手教學用（移動教學＝左 WASD 不閃＋右 Press 閃、收起=移動；攻擊教學＝放 MouseLeft、收起=攻擊…），
    /// 但本身不綁教學——由「玩家提示(playerHint)」trigger 資料驅動（見 readme/TRIGGER_CHAIN.md）。
    /// 左右槽的螢幕位移是本檔常數（調好一次、編輯器只選左右不用填 XY）。
    /// </summary>
    public class PlayerHintPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Overlay;
        public override bool BlocksGameplayInput => false;
        public override bool PausesGame => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;

        /// <summary>收起時機。</summary>
        public enum HideMode { Move, Attack, AnyKey }

        // ── 位置／外觀常數（調這裡，編輯器不用填 XY）──
        const float HeadWorldOffset = 1.4f;    // 圖群大致擺在玩家頭上多高（世界單位）
        const float ImageHeight = 390f;        // 圖顯示高度（寬依圖片比例自動）→ 放大 3 倍
        static readonly Vector2 LeftOffset = new Vector2(-260f, 90f);   // 左圖相對頭頂的螢幕位移（左上；放大後往外＋往上一點避免蓋到玩家）
        static readonly Vector2 RightOffset = new Vector2(260f, 90f);   // 右圖（右上）
        const float FlashSpeed = 3.3f;   // 閃爍頻率（原 5，放慢約 1/3）
        const float FlashMin = 0.15f, FlashMax = 1f;
        const float MinVisible = 0.35f;        // 至少顯示這麼久才允許因移動收起（避免剛解鎖輸入就誤收）

        RectTransform _self;
        Image _leftImg, _rightImg;
        bool _leftFlash, _rightFlash;
        Transform _anchor;
        Camera _cam;
        HideMode _hideMode;
        System.Action _onHidden;
        float _shownAt;

        protected override void OnBuild()
        {
            _self = (RectTransform)transform;
            _leftImg = MakeImg("HintLeft");
            _rightImg = MakeImg("HintRight");
        }

        Image MakeImg(string name)
        {
            var img = UIBuilder.Image(transform, name, null, Color.white);
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.enabled = false;
            return img;
        }

        void Setup(Transform anchor, Sprite left, bool flashLeft, Sprite right, bool flashRight, HideMode hideMode, System.Action onHidden)
        {
            _anchor = anchor;
            _hideMode = hideMode;
            _onHidden = onHidden;
            _leftFlash = flashLeft;
            _rightFlash = flashRight;
            ApplyImg(_leftImg, left);
            ApplyImg(_rightImg, right);
            _shownAt = Time.unscaledTime;
        }

        void ApplyImg(Image img, Sprite sp)
        {
            img.sprite = sp;
            img.enabled = sp != null;
            if (sp == null) return;
            float w = ImageHeight * (sp.rect.height > 0f ? sp.rect.width / sp.rect.height : 1f);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, ImageHeight);
        }

        void Update()
        {
            if (!IsOpen || _anchor == null) return;
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

            // 定位：玩家頭頂 → 螢幕 → 面板本地座標，左右各加固定位移（每幀跟著玩家跑）。
            Vector3 headWorld = _anchor.position + Vector3.up * HeadWorldOffset;
            Vector2 screen = _cam.WorldToScreenPoint(headWorld);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_self, screen, null, out Vector2 local))
            {
                if (_leftImg.enabled) _leftImg.rectTransform.anchoredPosition = local + LeftOffset;
                if (_rightImg.enabled) _rightImg.rectTransform.anchoredPosition = local + RightOffset;
            }

            // 閃爍（只有標記要閃的那張；不閃的維持全亮）。
            float a = Mathf.Lerp(FlashMin, FlashMax, Mathf.Abs(Mathf.Sin(Time.unscaledTime * FlashSpeed)));
            if (_leftImg.enabled) SetAlpha(_leftImg, _leftFlash ? a : 1f);
            if (_rightImg.enabled) SetAlpha(_rightImg, _rightFlash ? a : 1f);

            // 收起條件成立 → 收圖 + 回呼（接鏈）。
            if (Time.unscaledTime - _shownAt >= MinVisible && HideConditionMet())
            {
                var cb = _onHidden;
                _onHidden = null;
                UIManager.Instance?.Close(this);
                cb?.Invoke();
            }
        }

        bool HideConditionMet()
        {
            if (UIManager.IsGameplayInputBlocked) return false;   // 有面板擋輸入時不算（例如又跳出對話）
            switch (_hideMode)
            {
                case HideMode.Move:
                    return Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;
                case HideMode.Attack:
                    return Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
                case HideMode.AnyKey:
                    return Input.anyKeyDown;
                default:
                    return false;
            }
        }

        static void SetAlpha(Image img, float a)
        {
            var c = img.color;
            c.a = a;
            img.color = c;
        }

        /// <summary>顯示提示（左右各一張，指定哪張閃、收起時機、收起後回呼）。左/右圖可為 null＝該側不顯示。</summary>
        public static void Show(Transform anchor, Sprite left, bool flashLeft, Sprite right, bool flashRight, HideMode hideMode, System.Action onHidden)
        {
            var p = UIManager.Instance?.Open<PlayerHintPanel>();
            p?.Setup(anchor, left, flashLeft, right, flashRight, hideMode, onHidden);
        }

        public static void HidePanel() => UIManager.Instance?.Close<PlayerHintPanel>();

        protected override void OnClose()
        {
            _anchor = null;
            _onHidden = null;
        }
    }
}
