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
    /// 每頁格數預設 10×10（依倉庫圖量測）；分頁數預設 5（對應底圖頁籤 I–V）。
    /// </summary>
    public class StorageSystem : MonoBehaviour
    {
        public const int PageCount = 5;
        public const int DefaultCols = 10;
        public const int DefaultRows = 10;

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
