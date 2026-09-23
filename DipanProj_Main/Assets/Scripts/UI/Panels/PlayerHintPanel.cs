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
    ///
    /// **暫停模式**（trigger 的「暫停遊戲」欄，2026-09-22 加）：進來就鎖輸入＋停住遊戲，
    /// 玩家做出指定動作（收起時機）才解鎖往下——強制新手教學用的，語氣同柴房佛燈／儲藏室藥水那兩段。
    /// 搭配「提示文字(語言表 id)」欄會同時顯示 <see cref="TutorialHintPanel"/> 的上方文字條。
    /// ⚠ 既有沒填這兩欄的 playerHint（洞窟 WASD、初始森林）行為完全不變。
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

        /// <summary>暫停模式的輸入鎖持有者名（**具名**鎖：解除時不會動到別人掛的，見 PROBLEMS D13）。</summary>
        const string HoldOwner = "PlayerHintPanel";

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
        bool _paused;      // 目前掛著暫停模式的輸入鎖（要有人收，見 Release）
        bool _hasText;     // 目前開著 TutorialHintPanel 的文字條（同上）
        bool _needRelease; // 暫停模式：開場時鍵已經按著 → 要先放開，才認下一次「按下」（見 Setup）

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

        void Setup(Transform anchor, Sprite left, bool flashLeft, Sprite right, bool flashRight,
                   HideMode hideMode, bool pause, string hintText, System.Action onHidden)
        {
            _anchor = anchor;
            _hideMode = hideMode;
            _onHidden = onHidden;
            _leftFlash = flashLeft;
            _rightFlash = flashRight;
            ApplyImg(_leftImg, left);
            ApplyImg(_rightImg, right);
            _shownAt = Time.unscaledTime;

            // 上方文字條（與柴房佛燈／儲藏室藥水教學同一條）：留空＝不顯示。
            _hasText = !string.IsNullOrEmpty(hintText);
            if (_hasText) TutorialHintPanel.Show(hintText);

            // ⚠ 暫停模式要的是「**按下**」這個動作，但收起條件讀的是「當下按著沒」。
            //   這顆常接在對話後面，而玩家多半是用**左鍵**點掉對話的——面板一開他手還按著，
            //   MinVisible 一過就被當成「做到了」秒收，教學等於沒出現過。
            //   所以開場已經按著的話，先要求放開，之後那一次按下才算數。
            _needRelease = pause && AttackHeld();
            SetPause(pause);
        }

        /// <summary>開火鍵（左鍵／空白鍵）當下按著沒——與 <see cref="HideMode.Attack"/> 的判定同一份。</summary>
        static bool AttackHeld() => Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);

        /// <summary>掛上／解除暫停模式的輸入鎖（鎖輸入＋停住遊戲）。</summary>
        void SetPause(bool on)
        {
            if (_paused == on) return;
            _paused = on;
            // 第三個參數＝暫停：這裡**要**暫停（強制教學就是要玩家停下來看），與 DreamTutorialFlow
            // 那種「只鎖輸入不暫停」（對話面板自己就是模態）不同。
            UIManager.Instance?.SetExternalHold(HoldOwner, on, on);
        }

        /// <summary>
        /// 收掉暫停模式留下的東西（輸入鎖、文字條）。
        /// ⚠ **一定要有這層保險**：面板若被別的流程關掉（換圖／死亡／別的教學插隊），沒解鎖的話
        ///   玩家會帶著「不能動＋timeScale=0」進下一場，而且**完全沒有錯誤訊息**
        ///   （同骨牢 <c>PlayerBind.OnDisable</c> 的理由）。所以 OnClose 也會呼叫它。
        /// </summary>
        void Release()
        {
            SetPause(false);
            if (_hasText) { TutorialHintPanel.Hide(); _hasText = false; }
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
                bool needFire = _paused && _hideMode == HideMode.Attack;
                Release();                      // 先解鎖（下面補射與接鏈都要遊戲是動的）
                if (needFire) RequestPlayerFire();
                UIManager.Instance?.Close(this);
                cb?.Invoke();
            }
        }

        bool HideConditionMet()
        {
            // 有面板擋輸入時不算（例如又跳出對話）——但**暫停模式下擋輸入的正是自己**，那要照算，
            // 否則收起條件永遠不成立＝玩家按左鍵沒反應、卡在暫停畫面。
            if (!_paused && UIManager.IsGameplayInputBlocked) return false;
            switch (_hideMode)
            {
                case HideMode.Move:
                    return Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;
                case HideMode.Attack:
                    bool held = AttackHeld();
                    if (_needRelease) { if (!held) _needRelease = false; return false; }   // 先放開，下一次按下才算
                    return held;
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

        /// <summary>
        /// 暫停教學「按左鍵發射武器」的補射：解除暫停的那一刻玩家**很可能已經放開左鍵了**
        /// （<c>HandleFiring</c> 讀的是 <c>Input.GetMouseButton</c>＝「當下按著沒」，不是「剛剛按過」），
        /// 那樣教學過了卻一發都沒射出去。所以主動請 PlayerController 開一段**強制開火窗口**
        /// （<c>RequestFireOnce</c>，預設 0.35 秒，過期作廢）。
        /// ⚠ **是窗口不是一幀**：雷射／佛光這種持續型武器不走 <c>Shoot</c>，只給一幀等於開一瞬間又關，
        ///   玩家根本看不出來有發射過（2026-09-22 作者回報「按了只解除暫停、沒射出東西」的成因之一）。
        /// 只在「暫停 ＋ 收起時機=攻擊」時做：不暫停的情況下收起那一刻玩家本來就按著，會自己射。
        /// </summary>
        void RequestPlayerFire()
        {
            if (_anchor == null) return;
            var pc = _anchor.GetComponent<PlayerController>();
            if (pc != null) pc.RequestFireOnce();
            else Debug.LogWarning("[PlayerHint] 找不到 PlayerController，按下左鍵不會補射出武器。");
        }

        /// <summary>顯示提示（左右各一張，指定哪張閃、收起時機、收起後回呼）。左/右圖可為 null＝該側不顯示。</summary>
        /// <param name="pause">true＝鎖輸入並暫停遊戲，做出動作才解鎖（強制教學）。</param>
        /// <param name="hintText">上方文字條要顯示的字（已翻好的字串）；留空＝不顯示文字條。</param>
        public static void Show(Transform anchor, Sprite left, bool flashLeft, Sprite right, bool flashRight,
                                HideMode hideMode, bool pause, string hintText, System.Action onHidden)
        {
            var p = UIManager.Instance?.Open<PlayerHintPanel>();
            p?.Setup(anchor, left, flashLeft, right, flashRight, hideMode, pause, hintText, onHidden);
        }

        public static void HidePanel() => UIManager.Instance?.Close<PlayerHintPanel>();

        protected override void OnClose()
        {
            Release();          // 保險：被別的流程關掉也一定解鎖（否則玩家永久暫停、沒有錯誤訊息）
            _anchor = null;
            _onHidden = null;
        }
    }
}
