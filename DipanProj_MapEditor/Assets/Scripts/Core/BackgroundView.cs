using UnityEngine;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 把地圖的背景圖（map.backgroundId）渲染在**最底層**，並**拉伸貼齊整個畫布範圍**
    /// （origin 到 width×height），讓可走格、物件座標都與畫面對齊。
    /// 每幀同步，故換背景/改尺寸/Undo 都會即時反映。
    /// </summary>
    public class BackgroundView : MonoBehaviour
    {
        SpriteRenderer _sr;
        string _appliedId;
        float _appliedTileSize = -1f;

        void Start()
        {
            var go = new GameObject("Background");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = -1000;   // 在 Tilemap(0) 與物件之下
            _sr.enabled = false;
        }

        void LateUpdate()
        {
            var session = MapSession.Instance;
            var map = session?.Map;
            if (map == null || string.IsNullOrEmpty(map.backgroundId))
            {
                if (_sr != null) _sr.enabled = false;
                _appliedId = null;
                return;
            }

            if (map.backgroundId != _appliedId || !Mathf.Approximately(map.tileSize, _appliedTileSize))
            {
                var item = session.Catalog.Find(map.backgroundId);
                _sr.sprite = SpriteCache.GetWholeSprite(item, map.tileSize);
                _appliedId = map.backgroundId;
                _appliedTileSize = map.tileSize;
            }

            if (_sr.sprite == null) { _sr.enabled = false; return; }

            // 拉伸貼齊畫布
            Vector3 size = _sr.sprite.bounds.size;
            float w = map.width * map.tileSize;
            float h = map.height * map.tileSize;
            if (size.x > 0f && size.y > 0f)
                _sr.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
            _sr.transform.position = new Vector3(map.origin.x + w / 2f, map.origin.y - h / 2f, 0f);
            _sr.enabled = true;
        }
    }
}
