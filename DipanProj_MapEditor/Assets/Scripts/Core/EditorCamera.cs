using UnityEngine;
using DipanMapEditor.Data;
using DipanMapEditor.UI;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 編輯器相機：滑鼠中鍵/右鍵拖曳平移、滾輪縮放、聚焦地圖。
    /// 畫布大於視窗時靠平移捲動編輯。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class EditorCamera : MonoBehaviour
    {
        public float minOrthoSize = 1f;
        public float maxOrthoSize = 60f;
        public float zoomSpeed = 0.1f;     // 每格滾輪的縮放比例
        public float framePadding = 1.2f;  // 聚焦時留白倍率

        Camera _cam;
        Vector3 _dragOrigin;
        bool _dragging;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
        }

        void OnEnable()
        {
            if (MapSession.Instance != null)
            {
                MapSession.Instance.OnMapChanged += FrameMap;
                MapSession.Instance.OnMapResized += FrameMap;
            }
        }

        void OnDisable()
        {
            if (MapSession.Instance != null)
            {
                MapSession.Instance.OnMapChanged -= FrameMap;
                MapSession.Instance.OnMapResized -= FrameMap;
            }
        }

        EditorUI _ui;

        void Update()
        {
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            bool overUI = _ui != null && _ui.IsPointerOverUI(Input.mousePosition);

            if (!overUI) HandleZoom();   // 滑鼠在面板上時不縮放（避免捲動調色盤連帶縮放場景）
            HandlePan(overUI);
        }

        void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Approximately(scroll, 0f)) return;

            // 以滑鼠所在世界點為錨點縮放，手感較自然
            Vector3 mouseWorldBefore = _cam.ScreenToWorldPoint(Input.mousePosition);
            float factor = 1f - scroll / zoomSpeed * 0.1f;
            _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize * factor, minOrthoSize, maxOrthoSize);
            Vector3 mouseWorldAfter = _cam.ScreenToWorldPoint(Input.mousePosition);
            transform.position += mouseWorldBefore - mouseWorldAfter;
        }

        void HandlePan(bool overUI)
        {
            // 中鍵或右鍵拖曳平移（在面板上時不「開始」平移；已在拖曳中則繼續）
            if ((Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1)) && !overUI)
            {
                _dragOrigin = _cam.ScreenToWorldPoint(Input.mousePosition);
                _dragging = true;
            }
            if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(1))
                _dragging = false;

            if (_dragging && (Input.GetMouseButton(2) || Input.GetMouseButton(1)))
            {
                Vector3 current = _cam.ScreenToWorldPoint(Input.mousePosition);
                Vector3 delta = _dragOrigin - current;
                delta.z = 0;
                transform.position += delta;
            }
        }

        /// <summary>把相機對準整張地圖（聚焦＋縮放到看得到全部）。</summary>
        public void FrameMap(MapData map)
        {
            if (map == null) return;
            Rect b = MapCoords.WorldBounds(map);
            Vector3 center = new Vector3(b.center.x, b.center.y, transform.position.z);
            transform.position = center;

            float halfH = b.height * 0.5f * framePadding;
            float halfW = b.width * 0.5f * framePadding / Mathf.Max(0.0001f, _cam.aspect);
            _cam.orthographicSize = Mathf.Clamp(Mathf.Max(halfH, halfW), minOrthoSize, maxOrthoSize);
        }
    }
}
