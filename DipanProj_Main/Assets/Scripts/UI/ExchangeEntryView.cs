using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dipan.UI
{
    /// <summary>
    /// 兌換面板上一個品項（列表列／縮圖格／大圖卡）的滑鼠事件轉接：進、出、點。
    /// 只負責把事件交回 <see cref="ExchangePanel"/>，版面由面板建。
    /// 子物件若有自己的 Button（例如收藏星星），點那顆不會觸發這裡的 OnClick（uGUI 會把點擊交給最近的處理者）。
    /// </summary>
    public class ExchangeEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Action OnEnter, OnExit, OnClick;
        /// <summary>右鍵＝切換收藏（2026-11-16）。</summary>
        public Action OnRightClick;

        public void OnPointerEnter(PointerEventData e) => OnEnter?.Invoke();
        public void OnPointerExit(PointerEventData e) => OnExit?.Invoke();

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) OnClick?.Invoke();
            else if (e.button == PointerEventData.InputButton.Right) OnRightClick?.Invoke();
        }

        void OnDisable() => OnExit?.Invoke();   // 被重建（列表刷新）或面板關閉時，確保 tooltip 收掉
    }
}
