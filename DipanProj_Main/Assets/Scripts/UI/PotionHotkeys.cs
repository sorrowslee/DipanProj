using UnityEngine;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 藥水熱鍵：遊戲中按 **1** 用左藥水格、**2** 用右藥水格（綁定在 <see cref="InventorySystem"/>，由背包的 <see cref="PotionSlot"/> 設定）。
    ///
    /// 實際的「喝一瓶」走 <see cref="ItemUse.TryUse"/>——**與背包右鍵是同一段程式**
    /// （套效果、扣一瓶、播喝藥特效、該種類用完清空快捷格、滿血滿魔也照喝照扣）。
    /// 本檔只負責「哪顆鍵對應哪一格」，不要在這裡自己重寫消耗邏輯，
    /// 不然「按 1」和「右鍵」的行為早晚會走鐘（2026-08-19 收攏，見 readme/INVENTORY.md）。
    ///
    /// 自動生成、跨場景常駐、零接線。只在正常遊戲時作用（開背包/暫停/UI 擋輸入時不喝）。見 readme/BOTTOM_HUD.md。
    /// </summary>
    [DisallowMultipleComponent]
    public class PotionHotkeys : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            var go = new GameObject("[PotionHotkeys]");
            go.AddComponent<PotionHotkeys>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            if (UIManager.IsGameplayInputBlocked) return;   // 開背包/暫停時不喝
            if (Input.GetKeyDown(KeyCode.Alpha1)) Use(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) Use(1);
        }

        void Use(int index)
        {
            var inv = InventorySystem.Instance;
            if (inv == null) return;
            int id = inv.GetPotionSlot(index);
            if (id <= 0) return;

            // 這一格綁了一種「已經不存在」的東西 → 清掉再走。兩種情況：
            //   ① 手上沒有了（背包被搬空、或從別處喝掉了）
            //   ② ItemTable 裡那一列被刪了（本專案真的會刪列，見 BloodlineSystem 的舊存檔救生艇）
            //      → GetData 回 null，不清的話這一格會永遠綁著一個鬼魂，按 1 完全沒反應也沒訊息。
            if (inv.CountOf(id) <= 0 || inv.GetData(id) == null) { inv.SetPotionSlot(index, 0); return; }

            // ⚠ 熱鍵這條路**沒有辦法跳確認視窗**（它不是 UI），所以凡是需要確認的東西一律不從這裡用。
            //    今天只有藥水進得了快捷格，所以踩不到；但只要有人在 ItemTable 把血統藥劑誤標成
            //    Category=Potion，按一下數字鍵就會**不可逆地決定本世血統、連問都不問**。擋在這裡最便宜。
            var plan = ItemUse.PlanUse(id);
            if (!plan.Ok || !string.IsNullOrEmpty(plan.ConfirmText)) return;

            // 效果／扣瓶／喝藥特效／用完清空快捷格全部在 ItemUse 裡，與背包右鍵共用同一段。
            // 藥水不需要對玩家說什麼，所以訊息直接丟掉。
            ItemUse.TryUse(id, out _);
        }
    }
}
