using UnityEngine;

/// <summary>
/// 一次性「全螢幕過場特效」的 id 分派（給觸發鏈的 <c>playScreenFx</c> 動作用）。
///
/// 這類特效＝進圖/離場時播一次就結束的全螢幕後處理（與睜眼醒來 EyeOpen 同家族），
/// **不是**世界端的 SceneEffect（火雨）/ SceneFx（煙霧、傳送門）那套持續環境特效——別混淆。
///
/// 資料驅動：編輯器「播放螢幕特效」trigger 填一個 <c>effectId</c>，遊戲端在這裡依 id 叫對應控制器播、播完回呼接鏈。
///
/// ★ 新增一種螢幕特效的三個維護點（缺一不可，請一起改）：
///   1) 寫該特效的 shader ＋ 控制器（仿 <see cref="IllusionShatterController"/> / EyeOpenController，提供 static Play(onDone, duration)）。
///   2) 在下面 <see cref="Play"/> 的 switch 加一個 case。
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
    /// </summary>
    public static void Play(int id, System.Action onDone, float duration = -1f)
    {
        switch (id)
        {
            case 1:
                IllusionShatterController.Play(onDone, duration);   // 破幻術
                break;
            default:
                Debug.LogWarning($"[ScreenFxPlayer] 未知的螢幕特效 id={id}（trigger 的 effectId 沒填或填錯）。略過特效、鏈照常接 next。");
                onDone?.Invoke();
                break;
        }
    }
}
