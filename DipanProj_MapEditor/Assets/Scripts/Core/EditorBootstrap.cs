using UnityEngine;
using DipanMapEditor.UI;
using DipanMapEditor.Tools;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 一鍵組裝整個編輯器場景：相機、格線、Tilemap、狀態、UI。
    /// 使用方式：空場景放一個空物件、掛上本元件、按 Play（或打包執行）即可。
    /// 全部由程式建立，無需手動接線。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class EditorBootstrap : MonoBehaviour
    {
        // 純黑：沒鋪 tile 的區域＝黑（＝不可玩範圍），與參考遊戲一致
        public Color backgroundColor = Color.black;

        void Awake()
        {
            // 1. 全域狀態（其 Awake 會立即設好 Instance、載入 catalog/triggerTypes）
            if (MapSession.Instance == null)
            {
                var sessionGO = new GameObject("MapSession");
                sessionGO.transform.SetParent(transform, false);
                sessionGO.AddComponent<MapSession>();
            }

            // 2. 相機（沿用 Main Camera，沒有就建一個）
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Editor Camera") { tag = "MainCamera" };
                cam = camGO.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
            var camT = cam.transform;
            camT.position = new Vector3(0, 0, -10);

            if (cam.GetComponent<EditorCamera>() == null) cam.gameObject.AddComponent<EditorCamera>();
            if (cam.GetComponent<GridRenderer>() == null) cam.gameObject.AddComponent<GridRenderer>();
            if (cam.GetComponent<WalkableOverlay>() == null) cam.gameObject.AddComponent<WalkableOverlay>();
            if (cam.GetComponent<TriggerOverlay>() == null) cam.gameObject.AddComponent<TriggerOverlay>();
            if (cam.GetComponent<ObjectSelectionOverlay>() == null) cam.gameObject.AddComponent<ObjectSelectionOverlay>();
            if (cam.GetComponent<TileBrushPreview>() == null) cam.gameObject.AddComponent<TileBrushPreview>();

            // 3. Tilemap 視圖容器
            if (FindObjectOfType<TilemapView>() == null)
            {
                var tvGO = new GameObject("TilemapView");
                tvGO.transform.SetParent(transform, false);
                tvGO.AddComponent<TilemapView>();
            }

            // 3a. 背景圖渲染（最底層）
            if (FindObjectOfType<BackgroundView>() == null)
            {
                var bgGO = new GameObject("BackgroundView");
                bgGO.transform.SetParent(transform, false);
                bgGO.AddComponent<BackgroundView>();
            }

            // 3b. 地上物渲染
            if (FindObjectOfType<ObjectView>() == null)
            {
                var ovGO = new GameObject("ObjectView");
                ovGO.transform.SetParent(transform, false);
                ovGO.AddComponent<ObjectView>();
            }

            // 3c. 地上物放置預覽（幻影）
            if (FindObjectOfType<ObjectGhostPreview>() == null)
            {
                var ghostGO = new GameObject("ObjectGhostPreview");
                ghostGO.transform.SetParent(transform, false);
                ghostGO.AddComponent<ObjectGhostPreview>();
            }

            // 4. IMGUI 介面
            if (FindObjectOfType<EditorUI>() == null)
            {
                var uiGO = new GameObject("EditorUI");
                uiGO.transform.SetParent(transform, false);
                uiGO.AddComponent<EditorUI>();
            }

            // 5. 筆刷輸入控制（地磚）
            if (FindObjectOfType<PaintController>() == null)
            {
                var paintGO = new GameObject("PaintController");
                paintGO.transform.SetParent(transform, false);
                paintGO.AddComponent<PaintController>();
            }

            // 6. 地上物輸入控制
            if (FindObjectOfType<ObjectController>() == null)
            {
                var objGO = new GameObject("ObjectController");
                objGO.transform.SetParent(transform, false);
                objGO.AddComponent<ObjectController>();
            }

            // 7. 可走/不可走筆刷控制
            if (FindObjectOfType<WalkableController>() == null)
            {
                var walkGO = new GameObject("WalkableController");
                walkGO.transform.SetParent(transform, false);
                walkGO.AddComponent<WalkableController>();
            }

            // 8. Trigger 塗刷控制
            if (FindObjectOfType<TriggerController>() == null)
            {
                var trigGO = new GameObject("TriggerController");
                trigGO.transform.SetParent(transform, false);
                trigGO.AddComponent<TriggerController>();
            }

            // 9. Undo 熱鍵（Cmd/Ctrl+Z）
            if (FindObjectOfType<UndoHotkey>() == null)
            {
                var undoGO = new GameObject("UndoHotkey");
                undoGO.transform.SetParent(transform, false);
                undoGO.AddComponent<UndoHotkey>();
            }

            Debug.Log("[EditorBootstrap] 地圖編輯器已組裝完成。按「新建地圖」開始。");
        }
    }
}
