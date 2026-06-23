using UnityEngine;
using UnityEngine.UI;
using Dipan.Drama;

namespace Dipan.UI
{
    /// <summary>
    /// 劇情檢視介面：一張大圖 + 一段文字，鋪在半透明黑遮罩上。**暫停遊戲、擋輸入、模態**。
    /// 由 InteractionManager 在玩家於「劇情觸發點」按 F 時 <see cref="Show"/>(dramaId) 開啟。
    ///
    /// 關閉：ESC（UIManager 的堆疊最上層 ESC）或**點畫面任意處**（整片透明關閉鈕）。
    /// 圖保持比例盡量放大、文字在圖下方。資料來自 <see cref="DramaDatabase"/>（DramaTable.csv）。
    /// 風格對齊專案：全程式建構、零 prefab/Inspector 接線。
    /// </summary>
    public class DramaPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;          // 模態：暫停遊戲
        public override bool BlocksGameplayInput => true; // 擋住移動/攻擊
        public override bool ShowBackdrop => true;        // 半透明黑遮罩
        public override bool CloseOnEscape => true;       // ESC 關

        Image _image;
        Text _text;
        RectTransform _imageRt;

        /// <summary>開啟並顯示指定劇情（找不到資料則印警告、不開）。任何系統可呼叫。</summary>
        public static void Show(int dramaId)
        {
            if (UIManager.Instance == null) return;
            var data = DramaDatabase.Instance.Get(dramaId);
            if (data == null)
            {
                Debug.LogWarning($"[DramaPanel] DramaTable 找不到 dramaId={dramaId}，不開啟劇情。");
                return;
            }
            var p = UIManager.Instance.Open<DramaPanel>();
            if (p != null) p.Apply(data);
        }

        protected override void OnBuild()
        {
            // 整片透明關閉鈕（點任意處關閉）；放最底層，圖/文在它之上但不擋點擊（raycastTarget=false）。
            var closeBtn = UIBuilder.Button(transform, "ClickToClose", null,
                () => UIManager.Instance.Close(this), new Color(0, 0, 0, 0));
            UIBuilder.Stretch((RectTransform)closeBtn.transform);
            closeBtn.targetGraphic = closeBtn.GetComponent<Image>();   // 程式建按鈕需手動指（見 PROBLEMS D4）

            // 內容容器：置中、佔畫面大部分。
            var box = UIBuilder.Create("Box", transform);
            var boxRt = UIBuilder.Rect(box);
            boxRt.anchorMin = new Vector2(0.5f, 0.5f);
            boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            boxRt.pivot = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(1280, 940);   // 參考解析度 1920×1080 下的大面板

            var vlg = box.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 38f;   // 圖↔文字間距（28→38：文字描述往下移 10 單位）
            vlg.padding = new RectOffset(40, 40, 30, 30);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;    // 讓子物件的 LayoutElement.preferredHeight 生效
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 大圖（保持比例）。
            var imgGo = UIBuilder.Create("Image", box.transform);
            _imageRt = UIBuilder.Rect(imgGo);
            _image = imgGo.AddComponent<Image>();
            _image.preserveAspect = true;
            _image.raycastTarget = false;
            var imgLE = imgGo.AddComponent<LayoutElement>();
            imgLE.preferredWidth = 1200f;
            imgLE.preferredHeight = 680f;   // 圖區高度上限；preserveAspect 會在框內等比縮放

            // 文字（圖下方）。
            _text = UIBuilder.Text(box.transform, "Text", "", 32, new Color(0.95f, 0.93f, 0.85f, 1f),
                                   TextAnchor.UpperCenter);
            _text.raycastTarget = false;
            var txtLE = _text.gameObject.AddComponent<LayoutElement>();
            txtLE.preferredWidth = 1200f;
            txtLE.flexibleHeight = 1f;

            // 底部小提示。
            var hint = UIBuilder.Text(box.transform, "Hint", "按 ESC 或點畫面任意處關閉", 22,
                                      new Color(1f, 1f, 1f, 0.45f), TextAnchor.LowerCenter);
            hint.raycastTarget = false;
            var hintLE = hint.gameObject.AddComponent<LayoutElement>();
            hintLE.preferredHeight = 30f;
        }

        void Apply(DramaData data)
        {
            if (_image != null)
            {
                _image.sprite = data.Image;
                _image.enabled = data.Image != null;   // 沒圖就只顯示文字
            }
            if (_text != null) _text.text = data.Text ?? "";
        }
    }
}
