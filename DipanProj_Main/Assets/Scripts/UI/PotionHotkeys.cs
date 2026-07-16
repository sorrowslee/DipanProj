using UnityEngine;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 藥水熱鍵：遊戲中按 **1** 用左藥水格、**2** 用右藥水格（綁定在 <see cref="InventorySystem"/>，由背包的 <see cref="PotionSlot"/> 設定）。
    /// 一瓶＝套效果（回血/回魔）＋扣背包一瓶＋在玩家身上播喝藥特效；該種類用完自動清空該格。滿血/滿魔也照喝照扣（依討論）。
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
            int id = inv.GetPotionSlot(index);
            if (id <= 0) return;
            var d = inv.GetData(id);
            if (d == null || inv.CountOf(id) <= 0) { inv.SetPotionSlot(index, 0); return; }

            var player = GameObject.FindGameObjectWithTag("Player");
            var stats = player != null ? player.GetComponent<CombatStats>() : null;
            if (stats != null)
            {
                if (d.HealHp > 0) stats.Heal(d.HealHp);
                if (d.HealMp > 0) stats.RestoreMana(d.HealMp);
            }
            inv.RemoveItem(id, 1);

            var pc = player != null ? player.GetComponent<PlayerController>() : null;
            if (pc != null) pc.PlayDrinkPotionVfx();   // 不管喝哪種藥都播

            if (inv.CountOf(id) <= 0) inv.SetPotionSlot(index, 0);   // 該種類用完 → 清空該格
        }
    }
}
