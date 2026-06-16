using System.Collections.Generic;
using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 把 GameLayer.objects（自由變換的地上物）渲染成 SpriteRenderer。
    /// 依 y 做 top-down Y-sort：越下方（y 越小）越前面。畫在地磚之上。
    /// </summary>
    public class ObjectView : MonoBehaviour
    {
        const int SortBase = 1000000;  // 大基底，確保即使往後移仍排在 Tilemap(0) 之上
        const int BandStep = 10000;    // 一個 zOrder 層級 = 跨過整個 Y-sort 範圍
        const float SortScale = 100f;

        readonly Dictionary<ObjectInstance, SpriteRenderer> _renderers = new Dictionary<ObjectInstance, SpriteRenderer>();
        Transform _root;

        void OnEnable()
        {
            if (MapSession.Instance != null)
            {
                MapSession.Instance.OnMapChanged += Rebuild;
                MapSession.Instance.OnMapResized += Rebuild;
                MapSession.Instance.OnMapRebuilt += Rebuild;
            }
        }

        void OnDisable()
        {
            if (MapSession.Instance != null)
            {
                MapSession.Instance.OnMapChanged -= Rebuild;
                MapSession.Instance.OnMapResized -= Rebuild;
                MapSession.Instance.OnMapRebuilt -= Rebuild;
            }
        }

        void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("ObjectLayer");
            go.transform.SetParent(transform, false);
            _root = go.transform;
        }

        public void Rebuild(MapData map)
        {
            foreach (var sr in _renderers.Values)
                if (sr != null) Destroy(sr.gameObject);
            _renderers.Clear();
            if (map?.GameLayer?.objects == null) return;
            EnsureRoot();
            foreach (var inst in map.GameLayer.objects)
                Apply(inst, map);
        }

        /// <summary>建立（若無）並套用某物件實例的外觀／變換。</summary>
        public void Apply(ObjectInstance inst, MapData map)
        {
            if (inst == null || map == null) return;
            EnsureRoot();

            if (!_renderers.TryGetValue(inst, out var sr) || sr == null)
            {
                var go = new GameObject("Obj");
                go.transform.SetParent(_root, false);
                sr = go.AddComponent<SpriteRenderer>();
                _renderers[inst] = sr;
            }

            var item = MapSession.Instance.Catalog.Find(inst.assetId);
            sr.sprite = SpriteCache.GetWholeSprite(item, map.tileSize);

            sr.transform.position = new Vector3(inst.x, inst.y, 0f);
            sr.transform.localScale = new Vector3(
                (inst.flipX ? -1f : 1f) * inst.scaleX,
                (inst.flipY ? -1f : 1f) * inst.scaleY, 1f);
            sr.transform.rotation = Quaternion.Euler(0, 0, inst.rot);
            // 先比 zOrder 層級（每層跨整個 Y-sort 範圍），同層內再依 sortKey 做 Y-sort
            sr.sortingOrder = SortBase + inst.zOrder * BandStep + Mathf.RoundToInt(-inst.sortKey * SortScale);
        }

        public void Remove(ObjectInstance inst)
        {
            if (inst != null && _renderers.TryGetValue(inst, out var sr))
            {
                if (sr != null) Destroy(sr.gameObject);
                _renderers.Remove(inst);
            }
        }

        /// <summary>取得某物件實例目前渲染後的世界包圍盒（含縮放），供點選命中測試。</summary>
        public bool TryGetWorldBounds(ObjectInstance inst, out Bounds bounds)
        {
            bounds = default;
            if (inst != null && _renderers.TryGetValue(inst, out var sr) && sr != null && sr.sprite != null)
            {
                bounds = sr.bounds;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 取得某物件「整張原圖」四角的世界座標（含翻轉/縮放/旋轉）。
        /// 用 sprite.bounds（整張貼圖、含透明邊）→ 選取框即原圖邊界，可看出去背範圍。
        /// 角點順序：左下、右下、右上、左上。
        /// </summary>
        public bool TryGetSpriteCorners(ObjectInstance inst, out Vector3 bl, out Vector3 br, out Vector3 tr, out Vector3 tl)
        {
            bl = br = tr = tl = default;
            if (inst == null || !_renderers.TryGetValue(inst, out var sr) || sr == null || sr.sprite == null)
                return false;
            Bounds b = sr.sprite.bounds;     // 區域性座標（含 pivot 偏移）
            Vector3 c = b.center, e = b.extents;
            var m = sr.transform.localToWorldMatrix;
            bl = m.MultiplyPoint3x4(new Vector3(c.x - e.x, c.y - e.y, 0));
            br = m.MultiplyPoint3x4(new Vector3(c.x + e.x, c.y - e.y, 0));
            tr = m.MultiplyPoint3x4(new Vector3(c.x + e.x, c.y + e.y, 0));
            tl = m.MultiplyPoint3x4(new Vector3(c.x - e.x, c.y + e.y, 0));
            return true;
        }
    }
}
