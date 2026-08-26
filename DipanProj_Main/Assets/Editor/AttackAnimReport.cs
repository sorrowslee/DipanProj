using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把每個血統的攻擊動畫「總幀／起播幀／結束幀（最大幀＋尾巴）」印到 Console。
/// 換了 AutoSprite 產的序列圖之後掃一眼：結束幀落在第一拳出手到底那格就對了；抓歪了作者拍板是重做圖，不做手填覆寫。
/// 規則與門檻見 PlayerSpriteLibrary.ActionStartPeakRatio / ActionEndPeakRatio / ActionEndTailFrames。
/// </summary>
public static class AttackAnimReport
{
    [MenuItem("Project Tools/角色/攻擊動畫幀數報告", false, 60)]
    static void Report()
    {
        try
        {
            PlayerSpriteLibrary.ResetForPlayMode();   // 丟掉舊快取，重新掃（換圖後才會反映）
            var lib = PlayerSpriteLibrary.Instance;
            var sb = new StringBuilder();
            sb.AppendLine($"[攻擊動畫幀數報告] 起播＝峰值×{PlayerSpriteLibrary.ActionStartPeakRatio:0.##}，結束＝第一次到峰值×{PlayerSpriteLibrary.ActionEndPeakRatio:0.##} 再＋{PlayerSpriteLibrary.ActionEndTailFrames} 幀；-1＝算不出來（播到最後一幀）");
            int n = 0;
            foreach (var bl in lib.BloodlinesWith("attack").OrderBy(x => x))
            {
                int total = lib.GetActionFrameCount(bl, "attack");
                int start = lib.GetActionStartFrame(bl, "attack");
                int end = lib.GetActionEndFrame(bl, "attack");
                string note = total <= 1 ? "（單張／非序列）" : (end < 0 ? "（算不出來，播到最後一幀）" : (end >= total - 1 ? "（結束幀＝最後一幀，等於整段都播）" : ""));
                sb.AppendLine($"  {bl,-16} 總幀 {total,3}　起播 {start,3}　結束 {end,3}　實際播 {(end >= 0 ? end - start + 1 : total - start),3} 幀 {note}");
                n++;
            }
            if (n == 0) sb.AppendLine("  （沒有任何血統有 attack 圖）");
            Debug.Log(sb.ToString());
        }
        catch (System.Exception e)
        {
            Debug.LogError("[攻擊動畫幀數報告] 失敗：" + e.Message + "\n" + e.StackTrace);
        }
    }
}
