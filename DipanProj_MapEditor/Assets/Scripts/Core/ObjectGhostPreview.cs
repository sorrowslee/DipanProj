using UnityEngine;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 地上物工具下，選了素材後在滑鼠位置顯示半透明「幻影」預覽
    /// （原尺寸、未翻轉、未旋轉 = 與放置出來的初始狀態一致），讓使用者下手前
    /// 就看到會放在哪、長怎樣。用一個 runtime SpriteRenderer 跟著游標。
    /// </summary>
    public class ObjectGhostPreview : MonoBehaviour
    {
        public float alpha = 0.5f;

        Camera _cam;
        EditorUI _ui;
        SpriteRenderer _sr;

        void Start()
        {
            _cam = Camera.main;
            _ui = FindObjectOfType<EditorUI>();
            var go = new GameObject("ObjectGhost");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 32000;   // 永遠畫在最上層
            _sr.enabled = false;
        }

        void Update()
        {
            var session = MapSession.Instance;
            if (_cam == null) _cam = Camera.main;
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();

            if (session == null || session.Map == null || _ui == null || _sr == null
                || _ui.CurrentTool != EditTool.Object
                || string.IsNullOrEmpty(_ui.SelectedObjectAssetId)
                || _ui.IsPointerOverUI(Input.mousePosition))
            {
                if (_sr != null) _sr.enabled = false;
                return;
            }

            var item = session.Catalog.Find(_ui.SelectedObjectAssetId);
            var sprite = SpriteCache.GetWholeSprite(item, session.Map.tileSize);
            if (sprite == null) { _sr.enabled = false; return; }

            _sr.sprite = sprite;
            _sr.color = new Color(1f, 1f, 1f, alpha);
            Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition); w.z = 0;
            _sr.transform.position = w;
            _sr.transform.localScale = Vector3.one;
            _sr.transform.rotation = Quaternion.identity;
            _sr.enabled = true;
        }
    }
}
