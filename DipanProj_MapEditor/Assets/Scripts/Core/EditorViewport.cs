using UnityEngine;
using DipanMapEditor.UI;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 讓場景只畫在「扣掉左側工具列、右側屬性面板、頂部列、底部狀態列」的中央區域。
    ///
    /// 在此之前，面板是用 <c>GUILayout.BeginArea</c> **蓋在**場景上的，地圖右緣永遠有 240px 看不到，
    /// 擺東西時得一直平移鏡頭。改成縮 <see cref="Camera.rect"/> 之後，面板變成「排在旁邊」而不是「蓋在上面」。
    ///
    /// 連帶好處（都是 Unity 自動處理的，不必改別的程式）：
    ///   • <c>Camera.aspect</c> 會變成可視區的比例 → `EditorCamera.FrameMap`（聚焦）自動以可視區為準。
    ///   • <c>Camera.ScreenToWorldPoint</c> 會考慮 pixelRect → 放置/拖曳/塗格的滑鼠座標自動正確。
    ///   • <c>OnPostRender</c> 的 GL 參考線（光圈、選取框、格線）也一併被限制在可視區內。
    ///
    /// ⚠ **Camera.rect 以外的區域不會被相機清除**，會殘留上一幀的畫面。
    ///    IMGUI 的 box 底圖是半透明的，蓋不住殘影，所以這裡另外生一台「只負責清背景」的相機
    ///    （depth 更低、cullingMask = Nothing、rect 全螢幕），每幀先把整個畫面清成底色。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class EditorViewport : MonoBehaviour
    {
        Camera _cam;
        Camera _clearCam;
        Rect _lastApplied = new Rect(-1, -1, -1, -1);

        void Awake()
        {
            _cam = GetComponent<Camera>();
            EnsureClearCamera();
        }

        void OnDisable()
        {
            // 元件被關掉時把相機還原成全螢幕，免得留下一塊縮小的畫面。
            if (_cam != null) _cam.rect = new Rect(0f, 0f, 1f, 1f);
        }

        void EnsureClearCamera()
        {
            if (_clearCam != null) return;
            var go = new GameObject("EditorClearCamera");
            go.transform.SetParent(transform, false);
            _clearCam = go.AddComponent<Camera>();
            _clearCam.orthographic = true;
            _clearCam.cullingMask = 0;                                  // 什麼都不畫，只負責清
            _clearCam.clearFlags = CameraClearFlags.SolidColor;
            _clearCam.backgroundColor = _cam != null ? _cam.backgroundColor : Color.black;
            _clearCam.rect = new Rect(0f, 0f, 1f, 1f);                  // 全螢幕
            _clearCam.depth = (_cam != null ? _cam.depth : 0f) - 100f;  // 比主相機早畫
            _clearCam.allowHDR = false;
            _clearCam.allowMSAA = false;
        }

        void LateUpdate()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null) return;
            EnsureClearCamera();

            // 背景色以 EditorBootstrap 設在主相機上的那個為準（它在 Start 才設，所以每幀同步一次最保險）。
            if (_clearCam.backgroundColor != _cam.backgroundColor)
                _clearCam.backgroundColor = _cam.backgroundColor;

            // 主相機改成只清深度——整個畫面的底色已由 clear 相機負責，
            // 主相機若還是 SolidColor，只會清自己 viewport 內那塊，意義重複。
            if (_cam.clearFlags != CameraClearFlags.Depth) _cam.clearFlags = CameraClearFlags.Depth;

            // EditorUI.ViewportRect 是 GUI 座標（左上原點、像素）；Camera.rect 是左下原點的 0~1 比例。
            Rect vp = EditorUI.ViewportRect;
            float sw = Mathf.Max(1f, Screen.width);
            float sh = Mathf.Max(1f, Screen.height);
            var r = new Rect(vp.x / sw,
                             (sh - vp.y - vp.height) / sh,
                             vp.width / sw,
                             vp.height / sh);

            // 只在真的變了才寫（每幀寫 Camera.rect 會強制重建投影矩陣）
            if (r != _lastApplied)
            {
                _cam.rect = r;
                _lastApplied = r;
            }
        }
    }
}
