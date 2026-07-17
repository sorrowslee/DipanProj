using System.IO;
using UnityEngine;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 「顯示底部ui」參考層：把遊戲的底部操控列 HUD（<c>BottomControlPanel_Bg.png</c>）以
    /// **世界空間** sprite 疊在地圖上，讓編輯時就能看到「遊戲中會被底部 UI 遮住哪些格子」。
    ///
    /// 為什麼是世界空間（而非螢幕空間）：遊戲端這張圖雖是螢幕空間 Overlay，但在標準「一個螢幕」
    /// 框景下（主相機 orthographicSize = 5、視高 <see cref="ViewHeightTiles"/> 格）它其實遮住一塊
    /// **固定大小的世界矩形**。把它畫進世界、貼齊地圖底部置中，就會跟著地圖一起平移縮放——
    /// 編輯器裡蓋住的格子 = 遊戲裡被遮住的格子（對 ~18×10 標準房間精準）。
    ///
    /// 幾何來源（見 readme/BOTTOM_HUD.md、DipanProj_Main 的 BottomHudPanel.cs）：
    ///   Canvas 基準 1920×1080；框圖 2172×724，螢幕寬固定 DisplayWidth=1180、底部置中；
    ///   不透明內容底邊（框圖 y=606）對齊螢幕底。
    ///
    /// 圖檔來源：由選單「DipanMapEditor → 同步素材（全部 module）」複製到
    ///   StreamingAssets/EditorUI/BottomControlPanel_Bg.png（無條件覆蓋，見 AssetSyncTool.cs）。
    /// 全程式建構，仿 <see cref="BackgroundView"/>。
    /// </summary>
    public class BottomUiOverlay : MonoBehaviour
    {
        // ── 圖檔位置（AssetSyncTool 同步到此）──
        public const string SubDir = "EditorUI";
        public const string FileName = "BottomControlPanel_Bg.png";
        public static string DefaultPath =>
            Path.Combine(Application.streamingAssetsPath, SubDir, FileName);

        // ── 遊戲端定位常數（與 BottomHudPanel.cs 一致）──
        const float ArtW = 2172f, ArtH = 724f;
        const float ArtBottomOpaque = 606f;   // 不透明內容底邊（對齊螢幕底）
        const float DisplayWidth = 1180f;      // 框圖在螢幕上的寬（參考像素）
        const float RefResY = 1080f;           // Canvas 基準解析度高

        // 遊戲標準「一個螢幕」的視高（格）。等於 MapCameraController.followViewHeightTiles，
        // orthographicSize = 此值 × tileSize ÷ 2。想對齊某張「整張地圖模式」的高房間可改成該房間高度。
        public float ViewHeightTiles = 10f;

        // 半透明，方便一邊看格子一邊避開遮蔽區。
        public float Alpha = 0.55f;

        public bool Visible { get; private set; }

        SpriteRenderer _sr;
        bool _texTried;
        Texture2D _tex;
        Sprite _sprite;

        void Start()
        {
            var go = new GameObject("BottomUi");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
            // 疊在所有地圖 sprite 之上。注意：本專案 sortingOrder 實質是 16-bit（超過會取模繞回，
            // 見 readme/SCENE_EFFECT.md），故用 16-bit 正值上限 32767，而非大基底（大基底會繞回負值被背景蓋住）。
            _sr.sortingOrder = short.MaxValue;   // 32767，穩定在最上層
            _sr.color = new Color(1f, 1f, 1f, Alpha);
            _sr.enabled = false;
        }

        /// <summary>切換顯示。回傳切換後是否可見；找不到圖檔會回 false（呼叫端可提示先同步素材）。</summary>
        public bool Toggle()
        {
            if (Visible) { Hide(); return false; }
            return Show();
        }

        /// <summary>顯示。找不到圖檔回 false。</summary>
        public bool Show()
        {
            if (!EnsureSprite())
            {
                Debug.LogWarning($"[BottomUiOverlay] 找不到底部 UI 圖：{DefaultPath}\n" +
                                 "請先執行選單「DipanMapEditor → 同步素材（全部 module）」。");
                Visible = false;
                if (_sr != null) _sr.enabled = false;
                return false;
            }
            Visible = true;
            return true;
        }

        public void Hide()
        {
            Visible = false;
            if (_sr != null) _sr.enabled = false;
        }

        bool EnsureSprite()
        {
            if (_sprite != null) return true;
            if (_texTried && _tex == null) _texTried = false; // 允許同步後重試
            string path = DefaultPath;
            if (!File.Exists(path)) { _texTried = true; return false; }

            _tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!_tex.LoadImage(File.ReadAllBytes(path))) { _tex = null; _texTried = true; return false; }
            _tex.filterMode = FilterMode.Point;
            _tex.wrapMode = TextureWrapMode.Clamp;
            // PPU 256：sprite 原生世界寬 = tex.width/256；實際世界尺寸靠 localScale 調到目標。
            _sprite = Sprite.Create(_tex, new Rect(0, 0, _tex.width, _tex.height),
                                    new Vector2(0.5f, 0.5f), 256f);
            if (_sr != null) _sr.sprite = _sprite;
            return true;
        }

        void LateUpdate()
        {
            if (_sr == null) return;
            var map = MapSession.Instance?.Map;
            if (!Visible || map == null || _sprite == null) { _sr.enabled = false; return; }

            float ts = map.tileSize;
            // 遊戲標準框景：RefResY 參考像素 ↔ ViewHeightTiles×ts 世界單位。
            float refPxPerWorld = RefResY / Mathf.Max(0.0001f, ViewHeightTiles * ts);
            float worldW = DisplayWidth / refPxPerWorld;      // 框圖世界寬
            float worldH = worldW * (ArtH / ArtW);            // 框圖世界高（含透明裙襬）

            // 依原生 sprite 尺寸換算 localScale。
            Vector3 nat = _sprite.bounds.size;                // 原生世界尺寸（PPU 256）
            if (nat.x > 0f && nat.y > 0f)
                _sr.transform.localScale = new Vector3(worldW / nat.x, worldH / nat.y, 1f);

            // 置中 X＝地圖水平中心；不透明底邊（框圖 y=606）對齊地圖底邊。
            float centerX = map.origin.x + map.width * ts * 0.5f;
            float mapBottomY = map.origin.y - map.height * ts;
            // pivot 在中心：不透明底邊距 sprite 底緣 = (ArtH-ArtBottomOpaque)/ArtH × worldH。
            float opaqueFromBottom = (ArtH - ArtBottomOpaque) / ArtH * worldH;
            float centerY = mapBottomY - opaqueFromBottom + worldH * 0.5f;

            _sr.color = new Color(1f, 1f, 1f, Alpha);
            _sr.transform.position = new Vector3(centerX, centerY, 0f);
            _sr.enabled = true;
        }
    }
}
