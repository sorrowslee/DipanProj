using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Dipan.UI
{
    /// <summary>
    /// 液體血球：一個圓形 <see cref="RawImage"/> ＋ 自繪著色器 <c>Custom/LiquidOrb</c>
    /// （放 <c>Resources/Shaders/LiquidOrb.shader</c>）。液體會隨當前值升降、受擊/耗魔時左右搖晃再回穩。
    ///
    /// 資料來源＝玩家身上的 <see cref="CombatStats"/>（由 <see cref="BottomHudPanel"/> 每幀餵入當前/上限）。
    /// 搖晃用「阻尼彈簧」模型在 C# 端算，把帶正負的搖晃量灌進著色器的 _Slosh。
    /// 時間走 <c>unscaledDeltaTime</c>，所以開背包暫停時液面仍緩緩微動。全程式建構、零 prefab（同專案風格）。
    ///
    /// 滑鼠移到球上（僅圓形範圍內，見 <see cref="IsRaycastLocationValid"/>）會在球上方顯示「當前 / 上限」數字。
    /// 見 readme/COMBAT.md、readme/UI_SYSTEM.md。
    /// </summary>
    [DisallowMultipleComponent]
    public class LiquidOrb : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ICanvasRaycastFilter
    {
        static Shader _shader;

        // ── 表演參數（可調；表演類效果，之後實機微調）──
        public float ApproachSpeed  = 7f;    // 液面追上目標的速度（越大越快貼上）
        public float SloshImpulse   = 22f;   // 液面每次變動 → 搖晃衝量
        public float SloshStiffness = 55f;   // 彈簧回正強度（越大回穩越快、晃得越急）
        public float SloshDamping   = 3.2f;  // 阻尼（越大越快靜下來）
        public float SloshMax       = 1.2f;  // 搖晃量上限（防過衝）

        // ── 亮度旋鈕（暗場景調不刺眼用；越小越暗/越不跳）──
        public float Brightness   = 0.72f;   // 整體亮度倍率
        public float Gloss        = 0.18f;   // 高光白點強度
        public float RimStrength  = 0f;   // 玻璃邊緣亮環
        public float SurfStrength = 0.18f;   // 液面亮邊

        RawImage _img;
        Material _mat;
        float _shown = 1f, _target = 1f, _sloshV, _sloshO, _t;
        bool _inited;

        // ── 懸停數字 ──
        string _label = "";
        float _cur, _max;
        bool _hover;
        GameObject _tip;
        Text _tipText;

        /// <summary>建立血球視覺。liquid = 液體亮色、deep = 深處暗色、label = 懸停顯示的前綴（HP/MP）。</summary>
        public void Init(Color liquid, Color deep, string label)
        {
            _label = label;

            if (_shader == null) _shader = Resources.Load<Shader>("Shaders/LiquidOrb");

            _img = GetComponent<RawImage>();
            if (_img == null) _img = gameObject.AddComponent<RawImage>();
            _img.texture = Texture2D.whiteTexture;   // 著色器不吃貼圖，只需要一張讓 RawImage 能畫
            _img.color = Color.white;
            _img.raycastTarget = true;               // 要收滑鼠懸停（實際命中範圍由 IsRaycastLocationValid 限縮成圓形）

            if (_shader != null)
            {
                _mat = new Material(_shader) { hideFlags = HideFlags.HideAndDontSave };
                _mat.SetColor("_Color", liquid);
                _mat.SetColor("_Color2", deep);
                _mat.SetFloat("_Bright", Brightness);
                _mat.SetFloat("_Gloss", Gloss);
                _mat.SetFloat("_RimStrength", RimStrength);
                _mat.SetFloat("_SurfStrength", SurfStrength);
                _img.material = _mat;
            }
            else
            {
                _img.color = liquid;   // 找不到著色器時的純色退化（不開天窗）
                Debug.LogWarning("[LiquidOrb] 找不到 Resources/Shaders/LiquidOrb，血球退化為純色圓。");
            }

            BuildTooltip();
        }

        void BuildTooltip()
        {
            var holder = UIBuilder.Create("OrbTip", transform);
            var rt = UIBuilder.Rect(holder);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);   // 球頂中央
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 12f);            // 浮在球上方
            rt.sizeDelta = new Vector2(150f, 40f);

            var bg = UIBuilder.Image(holder.transform, "BG", null, new Color(0f, 0f, 0f, 0.72f));
            UIBuilder.Stretch(bg.rectTransform);
            bg.raycastTarget = false;

            _tipText = UIBuilder.Text(holder.transform, "Value", "", 20, Color.white, TextAnchor.MiddleCenter);
            UIBuilder.Stretch(_tipText.rectTransform);

            _tip = holder;
            _tip.SetActive(false);
        }

        /// <summary>由 HUD 每幀餵入當前值 / 上限。</summary>
        public void SetStats(float current, float max)
        {
            _cur = current;
            _max = max;
            _target = (max > 0f) ? Mathf.Clamp01(current / max) : 0f;
            if (!_inited) { _shown = _target; _inited = true; }   // 首次直接對齊，不從 0 補滿
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            _t += dt;

            // 液面平滑追上目標
            float prev = _shown;
            _shown += (_target - _shown) * Mathf.Min(1f, dt * ApproachSpeed);

            // 液面變動 → 搖晃衝量；再走阻尼彈簧回穩
            float drop = prev - _shown;                       // 正 = 下降（受擊/耗魔）
            _sloshV += drop * SloshImpulse;
            _sloshV += -_sloshO * SloshStiffness * dt;
            _sloshV *= Mathf.Exp(-SloshDamping * dt);
            _sloshO += _sloshV * dt;
            _sloshO = Mathf.Clamp(_sloshO, -SloshMax, SloshMax);

            if (_mat != null)
            {
                _mat.SetFloat("_Fill", _shown);
                _mat.SetFloat("_Slosh", _sloshO);
                _mat.SetFloat("_T", _t);
            }

            // 懸停時即時更新數字（受擊時會跟著跳）
            if (_hover && _tipText != null)
                _tipText.text = $"{_label}  {Mathf.CeilToInt(_cur)} / {Mathf.CeilToInt(_max)}";
        }

        // ── 懸停：只在圓形範圍內觸發 ──
        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            if (_tip != null) _tip.SetActive(true);
        }
        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            if (_tip != null) _tip.SetActive(false);
        }

        /// <summary>把 RawImage 的方形命中範圍限縮成「球的圓形」——角落不算命中。</summary>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            var rt = (RectTransform)transform;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, eventCamera, out local))
                return false;
            float r = rt.rect.width * 0.5f;
            // pivot 置中，local 以中心為原點；圓內才算命中
            return local.sqrMagnitude <= r * r;
        }

        /// <summary>外部想手動踢一下搖晃（例如喝血瓶、被大招打中）可呼叫。</summary>
        public void Jolt(float strength) { _sloshV += strength; }

        void OnDestroy() { if (_mat != null) Destroy(_mat); }
    }
}
