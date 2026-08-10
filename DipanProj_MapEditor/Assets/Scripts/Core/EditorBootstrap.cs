using UnityEngine;
using DipanMapEditor.UI;
using DipanMapEditor.Tools;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 一鍵組裝整個編輯器場景：相機、格線、背景、狀態、UI。
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
            // 場景讓位：把 Camera.rect 縮到「扣掉工具列/面板/狀態列」的中央區域，面板才不會蓋住地圖
            if (cam.GetComponent<EditorViewport>() == null) cam.gameObject.AddComponent<EditorViewport>();
            if (cam.GetComponent<GridRenderer>() == null) cam.gameObject.AddComponent<GridRenderer>();
            if (cam.GetComponent<WalkableOverlay>() == null) cam.gameObject.AddComponent<WalkableOverlay>();
            if (cam.GetComponent<TriggerOverlay>() == null) cam.gameObject.AddComponent<TriggerOverlay>();
            if (cam.GetComponent<ObjectSelectionOverlay>() == null) cam.gameObject.AddComponent<ObjectSelectionOverlay>();
            // 照明光圈參考線（畫每盞燈的照射範圍與把手；編輯器不跑後處理，沒這圈線等於盲填半徑）
            if (cam.GetComponent<LightOverlay>() == null) cam.gameObject.AddComponent<LightOverlay>();
            // 照明預覽（把場景壓暗、讓燈照回來，看接近遊戲的實際效果；按「照明預覽」開關）
            if (cam.GetComponent<LightPreview>() == null) cam.gameObject.AddComponent<LightPreview>();
            if (cam.GetComponent<SceneFxOverlay>() == null) cam.gameObject.AddComponent<SceneFxOverlay>();
            if (cam.GetComponent<CutsceneOverlay>() == null) cam.gameObject.AddComponent<CutsceneOverlay>();

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

            // 3d. 底部 UI 參考層（「顯示底部ui」，世界空間疊在地圖底部；預設隱藏）
            if (FindObjectOfType<BottomUiOverlay>() == null)
            {
                var buGO = new GameObject("BottomUiOverlay");
                buGO.transform.SetParent(transform, false);
                buGO.AddComponent<BottomUiOverlay>();
            }

            // 4. IMGUI 介面
            if (FindObjectOfType<EditorUI>() == null)
            {
                var uiGO = new GameObject("EditorUI");
                uiGO.transform.SetParent(transform, false);
                uiGO.AddComponent<EditorUI>();
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

            // 8b. 場景特效控制
            if (FindObjectOfType<SceneFxController>() == null)
            {
                var sfxGO = new GameObject("SceneFxController");
                sfxGO.transform.SetParent(transform, false);
                sfxGO.AddComponent<SceneFxController>();
            }

            // 8b-2. 照明控制（不綁地上物的獨立光源）
            if (FindObjectOfType<LightController>() == null)
            {
                var lightGO = new GameObject("LightController");
                lightGO.transform.SetParent(transform, false);
                lightGO.AddComponent<LightController>();
            }

            // 8c. 劇情演出控制
            if (FindObjectOfType<CutsceneController>() == null)
            {
                var csGO = new GameObject("CutsceneController");
                csGO.transform.SetParent(transform, false);
                csGO.AddComponent<CutsceneController>();
            }

            // 8d. 劇情預覽器
            if (FindObjectOfType<DipanMapEditor.Preview.CutscenePreview>() == null)
            {
                var pvGO = new GameObject("CutscenePreview");
                pvGO.transform.SetParent(transform, false);
                pvGO.AddComponent<DipanMapEditor.Preview.CutscenePreview>();
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
