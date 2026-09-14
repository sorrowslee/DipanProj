using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Inventory
{
    /// <summary>
    /// 倉庫資料層（純資料、跨場景常駐單例）。倉庫由 **5 個分頁** 組成，每頁是一個獨立的 ItemGridData
    /// （各自的格網）。倉庫屬於「當前角色」，由 SaveManager 在載入角色時 RestoreState、存檔時 CaptureState，
    /// 寫進該角色 CharacterSave 的 storages[]（一頁一筆 StorageDTO）。見 readme/STORAGE.md、SAVE_SYSTEM.md。
    ///
    /// 每頁格數 5×5（2026-09-14 起，為了讓格子與背包一樣大）；分頁數 5（對應頁籤 I–V）。
    /// </summary>
    public class StorageSystem : MonoBehaviour
    {
        public const int PageCount = 5;

        /// <summary>
        /// 每頁的欄 × 列（2026-09-14 由 10×10 改成 5×5）。**改這兩個數字就換掉倉庫版面**——
        /// `StoragePanel` 會照它重排格子、重鋪格線、頁籤也跟著對齊每一欄。
        ///
        /// 為什麼變小：作者要求倉庫格子**與背包一樣大**（背包一格在螢幕上約 89 單位）。
        /// 倉庫格線區量出來是 725×688（底圖像素）、面板縮放 0.72 ⇒ 5×5 時一格 ≈ 88 單位，幾乎完全吻合；
        /// 原本的 10×10 一格只有 61 單位。容量因此由每頁 100 降到 25（五頁共 125）。
        /// </summary>
        public const int DefaultCols = 5;
        public const int DefaultRows = 5;

        static StorageSystem _instance;
        public static StorageSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<StorageSystem>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[StorageSystem]");
                        _instance = go.AddComponent<StorageSystem>();
                    }
                }
                return _instance;
            }
        }

        ItemGridData[] _pages;

        /// <summary>任一頁變動時觸發（聚合各頁的 OnChanged），SaveManager 用來標記待存。</summary>
        public event Action OnChanged;

        public int Pages => _pages != null ? _pages.Length : 0;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Init();
        }

        void Init()
        {
            var db = InventorySystem.Instance != null ? InventorySystem.Instance.Db : null;
            if (db == null) { db = new ItemDatabase(); db.LoadFromResources(); }

            _pages = new ItemGridData[PageCount];
            for (int i = 0; i < PageCount; i++)
            {
                _pages[i] = new ItemGridData(db, DefaultCols, DefaultRows, $"倉庫 {i + 1}");
                _pages[i].OnChanged += Raise;   // 任一頁變動 → 聚合事件
            }
        }

        void Raise() => OnChanged?.Invoke();

        /// <summary>取某分頁（IItemGrid，給 UI / 搬運用）。i 會被夾在範圍內。</summary>
        public ItemGridData Page(int i)
        {
            if (_pages == null || _pages.Length == 0) return null;
            i = Mathf.Clamp(i, 0, _pages.Length - 1);
            return _pages[i];
        }

        public bool HasAnyItem()
        {
            if (_pages == null) return false;
            foreach (var p in _pages) if (p.HasAnyItem()) return true;
            return false;
        }

        // ───────────── 存檔（一頁一筆 StorageDTO）─────────────

        public List<StorageDTO> CaptureState()
        {
            var list = new List<StorageDTO>(PageCount);
            for (int i = 0; i < _pages.Length; i++)
                list.Add(_pages[i].CaptureTo(i.ToString()));
            return list;
        }

        public void RestoreState(List<StorageDTO> list)
        {
            for (int i = 0; i < _pages.Length; i++)
            {
                StorageDTO dto = null;
                if (list != null)
                {
                    // 以 storageId == 頁索引 配對；配不到就用同序位（向下相容）。
                    dto = list.Find(d => d != null && d.storageId == i.ToString());
                    if (dto == null && i < list.Count) dto = list[i];
                }
                _pages[i].RestoreFrom(dto);
            }
        }
    }
}
