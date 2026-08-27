using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把每個血統的攻擊動畫「總幀／起播幀／結束幀（最大幀＋尾巴）」與 walk/attack 相對 idle 的「尺度縮放」印到 Console。
/// 換了 AutoSprite 產的序列圖之後掃一眼：結束幀落在第一拳出手到底那格就對了；抓歪了作者拍板是重做圖，不做手填覆寫。
/// 尺度縮放＝進遊戲時 walk/attack 會被乘上的倍率（&lt;1 代表那組圖畫得比 idle 大、會被縮小）；離 1 很遠代表那組圖大小抓歪了。
/// 規則與門檻見 PlayerSpriteLibrary.ActionStartPeakRatio / ActionEndPeakRatio / ActionEndTailFrames / GetActionSize。
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
                sb.AppendLine($"  {string.Empty,-16} 尺度縮放（相對 idle）：walk ×{ScaleText(lib, bl, "walk")}　attack ×{ScaleText(lib, bl, "attack")}　{SizeText(lib, bl)}");
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

    // 該動作進遊戲會被乘上的縮放倍率（= idle 尺度 ÷ 該動作尺度），與 PlayerAnimator.StateTile 同一條公式。
    static string ScaleText(PlayerSpriteLibrary lib, string bl, string state)
    {
        var idle = lib.GetActionSize(bl, "idle");
        var act = lib.GetActionSize(bl, state);
        if (!idle.ok || !act.ok || idle.Scale <= 0f || act.Scale <= 0f) return "  —  ";
        return (idle.Scale / act.Scale).ToString("0.000");
    }

    // 原始量測值（中位可見高 / 中位√面積，像素），方便看是「高度」還是「體積」抓歪了。
    static string SizeText(PlayerSpriteLibrary lib, string bl)
    {
        string One(string st)
        {
            var a = lib.GetActionSize(bl, st);
            return a.ok ? $"{st} 高 {a.medianHeightPx:0}px/√面積 {a.medianSqrtAreaPx:0}px" : $"{st} —";
        }
        return $"（{One("idle")}；{One("walk")}；{One("attack")}）";
    }
}
