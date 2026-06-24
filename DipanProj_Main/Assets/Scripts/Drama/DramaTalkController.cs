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
        public static void Play(int groupId)
        {
            var lines = DramaTalkDatabase.Instance.GetGroup(groupId);
            if (lines == null || lines.Count == 0)
            {
                Debug.LogWarning($"[DramaTalk] 對話群組 {groupId} 沒有內容（DramaTalkTable 找不到該群組），不播放。");
                return;
            }

            if (UIManager.Instance != null)
            {
                TalkPanel.Show(lines);
                return;
            }

            // 後備（無 UI 環境）：印出來驗證資料流。
            var sb = new StringBuilder();
            sb.Append($"[DramaTalk] ▶ 對話群組 {groupId}（共 {lines.Count} 句）：\n");
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                string side = l.Side == 2 ? "右" : "左";
                sb.Append($"  {i + 1}. (#{l.Id}) [{l.Name}｜頭像{side}：{l.AvatarPath}] {l.Text}\n");
            }
            Debug.Log(sb.ToString());
        }
    }
}
