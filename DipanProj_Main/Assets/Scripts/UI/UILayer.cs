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

        /// <summary>覆蓋層：過場、loading、全螢幕黑幕。壓在視窗與彈窗之上。</summary>
        Overlay = 3,

        /// <summary>
        /// 系統訊息層：toast 這類「絕對不能被吃掉」的回饋（<see cref="AlertPanel"/>）。**字面上的最上層**。
        ///
        /// 為什麼要獨立一層：這類訊息原本掛在 <see cref="HUD"/>，於是「開著背包點一個不能用的道具」時
        /// 提示被背包整個蓋住——玩家看到的是「點了沒反應」，關掉背包才發現訊息早就跳過了
        /// （見 [PROBLEMS.md] E18）。系統訊息的價值就在於它一定看得到，放在任何會被蓋住的層都是錯的。
        ///
        /// ⚠ 這層的 sortingOrder 不照 `i * 100` 排，見 <c>UIManager.SystemLayerSortingOrder</c>。
        /// ⚠ 放進這層的東西一律 `raycastTarget = false`——它蓋在所有視窗之上，會擋掉底下的點擊。
        /// </summary>
        System = 4,
    }
}
