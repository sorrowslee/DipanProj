using System.Text;
using UnityEngine;
using Dipan.UI;

namespace Dipan.Drama
{
    /// <summary>
    /// 頭像對話（劇情 Type=2）的播放入口：撈出該群組的對話（已依流水號排序）後，開啟 <see cref="TalkPanel"/> 播放。
    /// 沒有 UIManager 的環境（例如純測試）才退回 Debug.Log 印出順序。
    /// 由 InteractionManager 在玩家觸發 Type=2 劇情點時呼叫。見 readme/DRAMA.md。
    /// </summary>
    public static class DramaTalkController
    {
        /// <summary>播放某對話群組（依流水號由小到大）。groupId 來自 DramaTable Type=2 的 TalkGroup。</summary>
        /// <param name="allowSkip">
        /// 是否顯示右上角 Skip（略過整組對話）。預設允許；**只有一句的群組不會顯示**（見 TalkPanel.SkipAvailable）。
        /// 劇情觸發點由編輯器的「可略過」欄決定；劇情演出裡的 dialogue 步驟一律傳 false（演出自己有 Skip）。
        /// </param>
        public static void Play(int groupId, bool allowSkip = true)
        {
            var lines = DramaTalkDatabase.Instance.GetGroup(groupId);
            if (lines == null || lines.Count == 0)
            {
                Debug.LogWarning($"[DramaTalk] 對話群組 {groupId} 沒有內容（DramaTalkTable 找不到該群組），不播放。");
                return;
            }

            // 播放當下才解析立繪：Actor_<情緒> 需要「目前血統」才能定位主角情緒立繪。
            DramaTalkDatabase.Instance.ResolveGroupAvatars(lines, CurrentBloodline());

            if (UIManager.Instance != null)
            {
                TalkPanel.Show(lines, allowSkip);
                return;
            }

            // 後備（無 UI 環境）：印出來驗證資料流。
            var sb = new StringBuilder();
            sb.Append($"[DramaTalk] ▶ 對話群組 {groupId}（共 {lines.Count} 句）：\n");
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                string spot = l.SpotlightSide == 2 ? "右" : "左";
                sb.Append($"  {i + 1}. (#{l.Id}) [{l.Name}｜聚光{spot}｜左:{l.LeftAvatarPath} 右:{l.RightAvatarPath}] {l.Text}\n");
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>取目前主角血統（給 Actor_ 情緒立繪定位）；找不到玩家 / 元件退回 "Base"。</summary>
        static string CurrentBloodline()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var pc = player != null ? player.GetComponent<PlayerController>() : null;
            string b = pc != null ? pc.Bloodline : null;
            return string.IsNullOrEmpty(b) ? "Base" : b;
        }
    }
}
