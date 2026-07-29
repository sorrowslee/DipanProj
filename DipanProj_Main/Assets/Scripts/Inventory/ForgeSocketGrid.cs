using System;

namespace Dipan.Inventory
{
    /// <summary>
    /// 鍛造介面「左三右三共 6 個鑲嵌孔」＝一個容量 6 的道具容器（實作 IItemGrid）。
    ///
    /// 與一般格網唯一的差別是**孔位有鎖**：只有 index &lt; UnlockedCount 的孔才收得下東西，
    /// 其餘全部視為上鎖（介面上蓋鎖鏈圖）。UnlockedCount 由 ForgingPanel 依鐵砧上那件裝備的
    /// 孔位數（ForgeSockets.Of）設定；台面空著就是 0 ＝ 全鎖。
    ///
    /// 目前遊戲裡還沒有「寶石」這種道具，所以實際上放不進東西——但整條鏈路（解鎖 → 收放 → 退回背包）
    /// 都已經打通，等寶石與 SocketCount 欄位做好就能直接接上。見 readme/FORGING.md。
    /// </summary>
    public class ForgeSocketGrid : IItemGrid
    {
        readonly ItemStack[] _cells = new ItemStack[ForgeSockets.MaxSockets];
        int _unlocked;

        public string DisplayName => "鑲嵌孔";
        public int Capacity => _cells.Length;

        /// <summary>目前開啟的孔位數（0~6）。設定時會把被關掉的孔內容清出（由 ForgingPanel 負責退回背包）。</summary>
        public int UnlockedCount
        {
            get => _unlocked;
            set
            {
                int v = UnityEngine.Mathf.Clamp(value, 0, _cells.Length);
                if (v == _unlocked) return;
                _unlocked = v;
                OnChanged?.Invoke();
            }
        }

        /// <summary>這一孔是不是上鎖的（介面用來決定要不要蓋鎖鏈圖、要不要收拖放）。</summary>
        public bool IsLocked(int index) => index < 0 || index >= _unlocked;

        public ItemStack GetAt(int index) =>
            (index >= 0 && index < _cells.Length) ? _cells[index] : ItemStack.Empty;

        public void SetAt(int index, ItemStack stack)
        {
            if (index < 0 || index >= _cells.Length) return;
            if (IsLocked(index)) return;   // 上鎖的孔不收（拖放的把關另在 UI 端，這裡只是保險）
            _cells[index] = stack;
            OnChanged?.Invoke();
        }

        public int AddItem(int itemId, int count)
        {
            var d = GetData(itemId);
            if (d == null || count <= 0) return count;
            int left = count;
            for (int i = 0; i < _unlocked && left > 0; i++)
            {
                if (!_cells[i].IsEmpty) continue;
                _cells[i] = new ItemStack { ItemId = itemId, Count = 1 };
                left--;
            }
            if (left != count) OnChanged?.Invoke();
            return left;
        }

        public bool RemoveAt(int index, int count)
        {
            if (index < 0 || index >= _cells.Length || _cells[index].IsEmpty) return false;
            _cells[index] = ItemStack.Empty;
            OnChanged?.Invoke();
            return true;
        }

        public bool MoveWithin(int from, int to)
        {
            if (from < 0 || from >= _cells.Length || to < 0 || to >= _cells.Length) return false;
            if (IsLocked(to)) return false;
            var t = _cells[from]; _cells[from] = _cells[to]; _cells[to] = t;
            OnChanged?.Invoke();
            return true;
        }

        public ItemData GetData(int itemId) =>
            InventorySystem.Instance != null ? InventorySystem.Instance.GetData(itemId) : null;

        public event Action OnChanged;

        /// <summary>清空全部孔位並回傳原本的內容（給「換裝備／關面板 → 退回背包」用）。</summary>
        public ItemStack[] TakeAll()
        {
            var outp = (ItemStack[])_cells.Clone();
            for (int i = 0; i < _cells.Length; i++) _cells[i] = ItemStack.Empty;
            OnChanged?.Invoke();
            return outp;
        }
    }
}
