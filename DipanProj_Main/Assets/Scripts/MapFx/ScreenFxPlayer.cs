using UnityEngine;
using Dipan.UI;

/// <summary>
/// 一次性「全螢幕過場特效」的 id 分派（給觸發鏈的 <c>playScreenFx</c> 動作用）。
///
/// 這類特效＝進圖/離場時播一次就結束的全螢幕後處理（與睜眼醒來 EyeOpen 同家族），
/// **不是**世界端的 SceneEffect（火雨）/ SceneFx（煙霧、傳送門）那套持續環境特效——別混淆。
///
/// 資料驅動：編輯器「播放螢幕特效」trigger 填一個 <c>effectId</c>，遊戲端在這裡依 id 叫對應控制器播、播完回呼接鏈。
///
/// ★ HUD 統一處理（所有 id 自動套用，控制器不必各自處理）：
///   全螢幕過場都是**相機後處理**，碰不到 ScreenSpaceOverlay 的 HUD（血球/血瓶那條會浮在特效上）。
///   所以這裡一律「開始時把 HUD 層藏到特效之下、過場結束(接鏈)後復原」。新增特效不必再自己藏 HUD。
///   （EyeOpen 走的是地圖 EnterEffect 進圖路徑、非本分派器；若要同款處理需另接。）
///
/// ★ 新增一種螢幕特效的三個維護點（缺一不可，請一起改）：
///   1) 寫該特效的 shader ＋ 控制器（仿 <see cref="IllusionShatterController"/> / EyeOpenController，提供 static Play(onDone, duration)）。
///      控制器**務必在所有結束路徑（正常播完＋被場景切換打斷）都呼叫傳入的 onDone**，HUD 復原才不會漏。
///   2) 在下面 <see cref="Play"/> 的 switch 加一個 case（把包好的 <c>done</c> 傳進去，不要傳原始 onDone）。
///   3) 更新編輯器「螢幕特效表」清單（EditorUI 的 ScreenFxCatalog）＋ readme/MAP_ENTER_EFFECT.md 附節。
///
/// 目前登記：
///   1 = 破幻術（幻境崩碎回歸現實；IllusionShatterController）。
/// </summary>
public static class ScreenFxPlayer
{
    /// <summary>
    /// 依 <paramref name="id"/> 播一次全螢幕過場特效；播完呼叫 <paramref name="onDone"/>（觸發鏈在這裡接 next）。
    /// <paramref name="duration"/> ≥ 0 覆寫該特效總長（留空傳 -1）。未知/為 0 的 id：警告並直接呼叫 onDone（鏈照常往下走，不卡住）。
    /// HUD 層在開始時自動藏起、結束後自動復原（見類別註解）。
    /// </summary>
    public static void Play(int id, System.Action onDone, float duration = -1f)
    {
        // 開始：把 HUD 層藏到特效之下（相機後處理碰不到 Overlay UI）。
        bool hidHud = false;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetLayerVisible(UILayer.HUD, false);
            hidHud = true;
        }

        // 包一層：先接鏈（通常 teleportTo→GoToMap 會同步開載入頁蓋住畫面），再復原 HUD——
        // 這樣即使過場收尾停在全白幀，HUD 也是在載入頁(Overlay 層，在 HUD 之上)之下恢復，不會閃出。
        System.Action done = () =>
        {
            onDone?.Invoke();
            if (hidHud && UIManager.Instance != null)
                UIManager.Instance.SetLayerVisible(UILayer.HUD, true);
        };

        switch (id)
        {
            case 1:
                IllusionShatterController.Play(done, duration);   // 破幻術
                break;
            default:
                Debug.LogWarning($"[ScreenFxPlayer] 未知的螢幕特效 id={id}（trigger 的 effectId 沒填或填錯）。略過特效、鏈照常接 next。");
                done();
                break;
        }
    }
}
