using System.Collections.Generic;
using UnityEngine;
using DipanMapEditor.Data;
using DipanMapEditor.Preview;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 把每個 NPC 擺放畫成「會呼吸的角色」：直讀主專案 GameAssets 的 idle 幀
    /// （<see cref="PreviewSpriteLoader"/>，同劇情演員的預覽管線，不必同步素材），原地播放 idle 動畫。
    /// **所有工具下都畫**（NPC 是地圖內容的一部分，擺地上物時也要看得到它站哪）；
    /// 排序用與遊戲/ObjectView 同一條公式的 zOrder=0 帶（7000 + Y-sort），和地上物正確前後交錯。
    /// 位置每幀同步（拖曳即時跟手）；清單增減／換角色時重建。
    /// </summary>
    public class NpcView : MonoBehaviour
    {
        class View
        {
            public NpcInstance npc;
            public GameObject go;
            public SpriteRenderer sr;
            public Sprite[] frames;
            public float fps = 8f;
            public int npcId;      // 建立當時的角色 id（變了要重載幀）
            public int frame; public float timer;
        }

        readonly List<View> _views = new List<View>();
        MapData _map;

        void OnEnable()
        {
            if (MapSession.Instance != null)
            {
                MapSession.Instance.OnMapChanged += OnMapChanged;
                MapSession.Instance.OnMapRebuilt += OnMapChanged;
            }
        }

        void OnDisable()
        {
            if (MapSession.Instance != null)
            {
                MapSession.Instance.OnMapChanged -= OnMapChanged;
                MapSession.Instance.OnMapRebuilt -= OnMapChanged;
            }
            Clear();
        }

        void OnMapChanged(MapData m) { _map = m; Rebuild(); }

        void Clear()
        {
            foreach (var v in _views) if (v.go != null) Destroy(v.go);
            _views.Clear();
        }

        void Rebuild()
        {
            Clear();
            if (_map?.npcs == null) return;
            foreach (var n in _map.npcs)
                if (n != null) _views.Add(CreateView(n));
        }

        View CreateView(NpcInstance n)
        {
            var v = new View { npc = n, npcId = n.npcId };
            v.go = new GameObject("NpcPreview_" + (string.IsNullOrEmpty(n.name) ? n.id : n.name));
            v.go.transform.SetParent(transform, false);
            v.sr = v.go.AddComponent<SpriteRenderer>();
            LoadFrames(v);
            SyncTransform(v);
            return v;
        }

        void LoadFrames(View v)
        {
            var row = NpcTableEditor.Get(v.npc.npcId);
            float tile = _map != null ? _map.tileSize : 1f;
            // 尺寸正規化交給 PreviewSpriteLoader（依可見高度 ≈ 遊戲的 CharacterWorldHeight 邏輯它已處理近似），
            // 這裡再乘 NpcTable 的 Scale（同遊戲 transform.localScale）。
            var f = row != null ? PreviewSpriteLoader.Load(row.Name, _map?.module, tile) : null;
            v.frames = f?.idle;
            v.fps = row != null && row.AnimFPS > 0f ? row.AnimFPS : 8f;
            v.go.transform.localScale = Vector3.one * (row != null && row.Scale > 0f ? row.Scale : 1f);
            v.sr.sprite = (v.frames != null && v.frames.Length > 0) ? v.frames[0] : null;
            if (v.sr.sprite == null)
                Debug.LogWarning($"[NpcView] NPC 擺放（npcId={v.npc.npcId}）載不到 idle 圖——" +
                                 "確認 NpcTable 有這列、且圖放在 Monsters/SequenceImage/<Name>/idle/。");
        }

        void SyncTransform(View v)
        {
            v.go.transform.position = new Vector3(v.npc.x, v.npc.y, 0f);
            // 與 ObjectView / 遊戲 MapDepthSort 的 zOrder=0 帶同公式（sortKey = y）→ 和地上物正確交錯。
            v.sr.sortingOrder = 7000 + Mathf.Clamp(Mathf.RoundToInt(-v.npc.y * 100f), 0, 5999);
        }

        void Update()
        {
            if (_map == null && MapSession.Instance != null) { _map = MapSession.Instance.Map; Rebuild(); }
            if (_map?.npcs == null) { if (_views.Count > 0) Clear(); return; }

            // 清單有增減 → 重建（清單很短，重建便宜）。
            if (_views.Count != _map.npcs.Count) { Rebuild(); return; }

            for (int i = 0; i < _views.Count; i++)
            {
                var v = _views[i];
                if (v.go == null || v.npc != _map.npcs[i]) { Rebuild(); return; }
                if (v.npcId != v.npc.npcId) { v.npcId = v.npc.npcId; LoadFrames(v); }   // 面板換了角色
                SyncTransform(v);

                // idle 呼吸動畫
                if (v.frames != null && v.frames.Length > 1)
                {
                    v.timer += Time.deltaTime;
                    float dur = 1f / Mathf.Max(1f, v.fps);
                    while (v.timer >= dur) { v.timer -= dur; v.frame = (v.frame + 1) % v.frames.Length; }
                    v.sr.sprite = v.frames[v.frame];
                }
            }
        }
    }
}
