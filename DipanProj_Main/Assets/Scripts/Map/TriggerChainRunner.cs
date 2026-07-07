using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 觸發鏈的「延後一幀執行」小幫手（常駐、單例、自動生成）。
///
/// 為什麼需要：對話面板（TalkPanel/DramaPanel）關閉時會在 <c>OnClose</c> 裡同步接觸發鏈，
/// 若鏈的下一步又是「開一段新對話」，等於在「面板正在關」的當下又去開同一個面板 → 重入，
/// 導致關閉流程把剛開好的面板又關掉、但 IsOpen 殘留 true，遊戲永久暫停（玩家卡死）。
/// 解法：把「對話關閉後的接鏈」丟到下一幀執行，等舊面板完全關乾淨再開新的，避開重入。
///
/// 注意：Update 不受 Time.timeScale 影響（暫停時仍會跑），所以即使當下遊戲被面板暫停，
/// 排進來的動作下一幀照樣會執行。
/// </summary>
public class TriggerChainRunner : MonoBehaviour
{
    static TriggerChainRunner _inst;
    readonly Queue<Action> _queue = new Queue<Action>();

    /// <summary>把一個動作排到下一幀執行（用於避開「面板關閉當幀又開面板」的重入）。</summary>
    public static void NextFrame(Action action)
    {
        if (action == null) return;
        Ensure();
        _inst._queue.Enqueue(action);
    }

    static void Ensure()
    {
        if (_inst != null) return;
        var go = new GameObject("[TriggerChainRunner]");
        DontDestroyOnLoad(go);
        _inst = go.AddComponent<TriggerChainRunner>();
    }

    void Update()
    {
        // 只處理「本幀進來時已排好」的動作；動作內若又排新的，留到下一幀（避免同幀連鎖重入）。
        int n = _queue.Count;
        for (int i = 0; i < n && _queue.Count > 0; i++)
        {
            var a = _queue.Dequeue();
            try { a?.Invoke(); }
            catch (Exception e) { Debug.LogError($"[TriggerChainRunner] 延後動作丟出例外：{e}"); }
        }
    }
}
