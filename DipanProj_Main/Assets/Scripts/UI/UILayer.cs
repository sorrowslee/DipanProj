namespace Dipan.UI
{
    /// <summary>
    /// UI 分層。每一層是一個獨立 Canvas，sortingOrder 依序遞增（值越大畫越上層）。
    /// 面板用 UIPanel.Layer 宣告自己屬於哪一層；UIManager 依此把面板掛到對應 Canvas。
    /// </summary>
    public enum UILayer
    {
        /// <summary>常駐抬頭顯示：血條、武器、金錢等。不入視窗堆疊、通常不擋輸入。</summary>
        HUD = 0,

        /// <summary>主視窗：背包、地圖、角色、設定…。會入堆疊、ESC 可逐層關閉。</summary>
        Window = 1,

        /// <summary>彈窗：確認框、提示（tooltip）。永遠壓在視窗之上。</summary>
        Popup = 2,

        /// <summary>覆蓋層：過場、loading、全螢幕黑幕。壓在最上層。</summary>
        Overlay = 3,
    }
}
