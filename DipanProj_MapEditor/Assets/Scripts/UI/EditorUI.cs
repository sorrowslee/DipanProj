using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.Data;
using DipanMapEditor.IO;
using DipanMapEditor.Tools;

namespace DipanMapEditor.UI
{
    /// <summary>
    /// IMGUI 介面（M2）：頂部工具列 + 右側地磚調色盤 + 新建對話框。
    /// 對外提供當前工具、選取 tile、以及「指標是否在 UI 上」（供筆刷避開面板）。
    /// </summary>
    public class EditorUI : MonoBehaviour
    {
        const int TileNativePx = 256;
        // ── 版面尺寸（B 案：左側工具列 + 中央場景 + 右側屬性面板 + 底部狀態列）──
        // 場景由 EditorViewport 依 ViewportRect 縮 Camera.rect，所以面板是「排在旁邊」而不是「蓋在上面」。
        const float TopBarH = 34f;     // 頂部列：檔案與檢視操作
        const float RailW = 76f;       // 左側垂直工具列（垂直空間充足，之後再加工具也不會擠爆）
        const float StatusH = 26f;     // 底部狀態列：地圖資訊與狀態訊息
        const float PaletteW = 240f;   // 右側屬性面板
        const float Thumb = 48f;
        const float InspectorW = 360f;
        const float InspectorH = 470f;

        public EditTool CurrentTool { get; private set; } = EditTool.Object;
        public string SelectedObjectAssetId { get; private set; }

        /// <summary>可走工具的當前筆刷狀態字元（'0' 可走 / '1' 牆 / '2' 水）。</summary>
        public char WalkBrushState { get; private set; } = WalkableOps.Walk;

        /// <summary>可走工具的筆刷邊長（以子格計）：1~128。一筆塗 N×N 個子格。</summary>
        public int WalkBrushSize { get; private set; } = 1;
        static readonly int[] WalkBrushSizes = { 1, 2, 4, 8, 16, 32, 64, 128 };

        public void ClearObjectBrush() => SelectedObjectAssetId = null;

        // 座標/血量/FPS 輸入框暫存（依焦點決定要不要從物件同步回來）
        string _objXBuf = "", _objYBuf = "", _objHpBuf = "", _objFpsBuf = "";
        string _objAppearDelayBuf = "";
        string _objLightBuf = "";   // 發光半徑輸入暫存
        // 照明細項的輸入暫存（亮度／搖晃強度／搖晃速度／邊緣柔和度／光色）。與發光半徑同一套「聚焦中才不覆寫」的寫法。
        string _objLightIntBuf = "", _objLightFlkBuf = "", _objLightSpdBuf = "", _objLightSofBuf = "", _objLightColBuf = "";
        Vector2 _objInspScroll;
        DipanMapEditor.Core.ObjectView _objView;
        string _objAppearBuf = "";   // 出現條件「完成 N 關」輸入暫存

        // Trigger
        public TriggerRegion CurrentRegion { get; private set; }

        // 傳送點「外型位置」點放模式：面板開啟後，TriggerController 下一次點畫布就把 markerX/markerY 設成點擊處。
        public bool MarkerPlaceActive { get; private set; }
        public void EndMarkerPlace() => MarkerPlaceActive = false;
        public bool TriggerAddCells { get; private set; } = true;     // true=加格、false=減格
        public bool TriggerPaintMode { get; private set; } = true;    // true=筆刷、false=檢視（點區域檢查參數）
        // true=選了類型、每畫一筆建一個「新」區域；false=正在編輯某個既有區域（加進它）
        public bool TriggerNewRegionPerStroke { get; private set; } = true;
        string _triggerType;
        Vector2 _trigScroll;

        /// <summary>ESC 進檢視模式：停止筆刷、清掉選取，改成點畫布上的區域來檢查。</summary>
        public void EnterTriggerInspect()
        {
            TriggerPaintMode = false;
            CurrentRegion = null;
            _statusMsg = "Trigger 檢視模式：點區域檢查參數；點右側類型回到筆刷";
        }

        /// <summary>檢視模式下由 TriggerController 呼叫，選取點到的區域（null = 點空白取消）。</summary>
        public void SelectRegion(TriggerRegion r) => CurrentRegion = r;

        /// <summary>新一筆畫的開始：依當前類型建一個新區域並設為 current（TriggerController 呼叫）。</summary>
        public TriggerRegion BeginNewRegion()
        {
            var map = MapSession.Instance?.Map;
            var types = MapSession.Instance?.TriggerTypes;
            if (map == null || types == null || string.IsNullOrEmpty(_triggerType)) return null;
            CreateRegion(_triggerType, map.TriggerLayer.regions, types);
            return CurrentRegion;
        }

        // 存/讀檔/背景
        bool _showSave, _showLoad, _showBg;
        string _saveName = "";
        Vector2 _loadScroll, _bgScroll;
        string _statusMsg = "";

        // 旗標登記表（全域；觸發點的旗標欄從這裡選）
        static FlagRegistry _flagReg = new FlagRegistry();
        bool _showFlags;
        Vector2 _flagScroll;
        string _flagMsg = "";
        string _newFlagName = "";
        // 旗標欄「輸入 id → 按確認」的暫存輸入（key＝region id + "/" + 欄位 key；按確認成功後清掉）
        static readonly Dictionary<string, string> _flagIdBuf = new Dictionary<string, string>();

        // 「螢幕特效表」參照彈窗（playScreenFx 的 effectId 欄旁的按鈕開）：列出可填的一次性全螢幕過場特效 id。
        // ⚠️ 這些欄位在 static 的 DrawParamField 裡被寫入，所以**必須 static**（同 _flagIdBuf/_flagReg），否則編譯不過。
        static bool _showScreenFx;
        static Vector2 _screenFxScroll;
        static TriggerRegion _screenFxRegion;   // 開表時記住是哪顆 trigger 的哪個欄，點「填入」就寫回去
        static string _screenFxKey;
        // ★ 螢幕特效清單（維護點之一）：id 必須對齊主遊戲 Resources/ScreenFxTable.csv。
        //   新增一種：這裡加一列 + 遊戲端寫 shader/控制器 + ScreenFxPlayer.Play 加 case + ScreenFxTable.csv 加列。
        //   這份 id 同時給「劇情 screenFx 步驟」與「MapsTable 的 EnterEffect 欄」共用（同 id 同效果）。
        static readonly (int id, string name, string desc)[] ScreenFxCatalog =
        {
            (1, "睜眼醒來", "眼皮閉合→睜開的進場醒來。填在 MapsTable 的 EnterEffect 欄時會連動玩家趴地→起身。"),
            (2, "破幻術", "幻境崩碎回歸現實：玻璃裂紋→碎塊崩落色散→白光收尾。紅嫁衣沒殺家人分支傳去榕樹妖前播。"),
            (3, "馬賽克清晰", "像素馬賽克格由粗到細慢慢收斂成清晰畫面。適合放在劇情亮起後、進場觸發對話前。"),
        };

        const string MapsDirPrefKey = "MapEditor.MapsDir";
        static string DefaultMapsDir => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Maps");
        string _mapsDir;   // 當前存讀檔資料夾（可自選，PlayerPrefs 記住）

        EditorCamera _cam;
        BottomUiOverlay _bottomUi;   // 「顯示底部ui」參考層
        Tools.ObjectController _objCtl;
        Tools.SceneFxController _sfxCtl;
        Tools.LightController _lightCtl;
        EditTool _toolBeforePreview = EditTool.Object;   // 進特效預覽器前停在哪個工具，離開時退回這個
        LightPreview _lightPrev;     // 照明預覽（壓暗＋燈照回來）
        Preview.TeleportMarkerPreview _tpPrev;   // 傳送點對位預覽（畫出真的傳送點特效、可直接拖曳）
        // 照明面板：清單捲動位置＋選取中那盞的數字輸入暫存（切換選取時重新同步）
        Vector2 _lightScroll;
        Data.LightInstance _lightBufFor;
        string _liRadBuf = "", _liIntBuf = "", _liFlkBuf = "", _liSpdBuf = "", _liSofBuf = "", _liColBuf = "", _liNameBuf = "";
        EffectPreviewUI _preview;   // 特效預覽器（懶建立）
        Vector2 _sfxScroll;
        // 場景特效數字欄的文字緩衝（讓使用者可清空/自由編輯；空或無效 = 套預設）。切換選取時重新同步。
        SceneFxInstance _sfxBufFor;
        string _bufFxId, _bufBulge, _bufW, _bufH, _bufInterval;

        // 劇情工具
        Tools.CutsceneController _csCtl;
        Preview.CutscenePreview _previewCtl;
        Vector2 _csScroll;

        // NPC 工具
        Tools.NpcController _npcCtl;
        Vector2 _npcScroll;
        Data.NpcInstance _npcBufFor;   // 選取切換時重灌輸入暫存
        string _npNameBuf = "", _npDramaBuf = "", _npSpeedBuf = "", _npDwellBuf = "", _npPanelBuf = "", _npArgBuf = "", _npNextBuf = "";
        bool _npcPickRole, _npcPickNext;
        CutsceneActor _csActorBufFor;
        CutsceneStep _csStepBufFor;
        string _csBufDrama, _csBufLang, _csBufSeconds, _csBufZoom, _csBufScale, _csBufFps, _csBufSpeed;

        // 物件調色盤
        List<PlaceableObject> _objects;
        string _objectsModule;
        Vector2 _objScroll;
        CatalogItem _hoverObj;     // 滑鼠懸停的物件（用於放大預覽）

        // 新建對話框
        bool _showNew;
        string _name = "RedBridalGown_01";
        string _module = "";
        string _bgId = "";
        string _tileSize = "1";
        string _width = "18";
        string _height = "10";

        void Start()
        {
            _cam = FindObjectOfType<EditorCamera>();
            _bottomUi = FindObjectOfType<BottomUiOverlay>();
            _objView = FindObjectOfType<DipanMapEditor.Core.ObjectView>();
            _objCtl = FindObjectOfType<Tools.ObjectController>();
            _flagReg = FlagRegistryStore.Load();
            _mapsDir = PlayerPrefs.GetString(MapsDirPrefKey, DefaultMapsDir);
            if (MapSession.Instance != null && MapSession.Instance.Map == null)
                _showNew = true;
        }

        // 存讀檔資料夾：可編輯路徑 + （Editor 內）原生選資料夾
        void DrawFolderRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("資料夾", GUILayout.Width(48));
            if (GUILayout.Button("選資料夾…", GUILayout.Width(90))) PickFolder();
            GUILayout.EndHorizontal();
            // 路徑另起一行，避免長路徑把按鈕擠出畫面
            _mapsDir = GUILayout.TextField(_mapsDir ?? "");
        }

        void PickFolder()
        {
#if UNITY_EDITOR
            string start = Directory.Exists(_mapsDir) ? _mapsDir : DefaultMapsDir;
            string picked = UnityEditor.EditorUtility.OpenFolderPanel("選擇存讀檔資料夾", start, "");
            if (!string.IsNullOrEmpty(picked)) RememberMapsDir(picked);
#else
            _statusMsg = "打包版請直接在欄位輸入/貼上資料夾路徑";
#endif
        }

        void RememberMapsDir(string dir)
        {
            _mapsDir = dir;
            PlayerPrefs.SetString(MapsDirPrefKey, dir);
            PlayerPrefs.Save();
        }

        Tools.ObjectController ObjCtl()
        {
            if (_objCtl == null) _objCtl = FindObjectOfType<Tools.ObjectController>();
            return _objCtl;
        }

        Tools.SceneFxController SfxCtl()
        {
            if (_sfxCtl == null) _sfxCtl = FindObjectOfType<Tools.SceneFxController>();
            return _sfxCtl;
        }

        Tools.LightController LightCtl()
        {
            if (_lightCtl == null) _lightCtl = FindObjectOfType<Tools.LightController>();
            return _lightCtl;
        }

        LightPreview LightPrev()
        {
            if (_lightPrev == null) _lightPrev = FindObjectOfType<LightPreview>();
            return _lightPrev;
        }

        Preview.TeleportMarkerPreview TpPrev()
        {
            if (_tpPrev == null) _tpPrev = FindObjectOfType<Preview.TeleportMarkerPreview>();
            return _tpPrev;
        }

        /// <summary>傳送點對位模式是否開著（TriggerController 據此決定要不要吃掉拖曳）。</summary>
        public bool TeleportAlignActive => TpPrev() != null && TpPrev().Enabled;

        Tools.CutsceneController CsCtl()
        {
            if (_csCtl == null) _csCtl = FindObjectOfType<Tools.CutsceneController>();
            return _csCtl;
        }

        Preview.CutscenePreview PreviewCtl()
        {
            if (_previewCtl == null) _previewCtl = FindObjectOfType<Preview.CutscenePreview>();
            return _previewCtl;
        }

        Tools.NpcController NpcCtl()
        {
            if (_npcCtl == null) _npcCtl = FindObjectOfType<Tools.NpcController>();
            return _npcCtl;
        }

        // ---- 版面計算（面板位置與場景可視區的唯一真相）----

        /// <summary>右側屬性面板的位置。所有工具的面板共用，夾在頂部列與底部狀態列之間。</summary>
        static Rect PanelRect =>
            new Rect(Screen.width - PaletteW, TopBarH, PaletteW,
                     Mathf.Max(1f, Screen.height - TopBarH - StatusH));

        /// <summary>
        /// 中央可視區（GUI 座標，左上原點）＝場景實際該畫出來的範圍。
        /// <see cref="Core.EditorViewport"/> 會把它換算成 Camera.rect，
        /// 這樣場景不會被面板蓋住，聚焦/縮放也自動以這塊為準（Camera.aspect 會跟著變）。
        /// </summary>
        public static Rect ViewportRect =>
            new Rect(RailW, TopBarH,
                     Mathf.Max(1f, Screen.width - RailW - PaletteW),
                     Mathf.Max(1f, Screen.height - TopBarH - StatusH));

        // ---- 供各工具查詢：指標是否壓在 UI 面板上 ----
        public bool IsPointerOverUI(Vector3 mousePos)
        {
            float ty = Screen.height - mousePos.y;          // 轉成左上原點 Y
            if (ty <= TopBarH) return true;                 // 頂部列
            if (mousePos.x <= RailW) return true;           // 左側工具列
            if (ty >= Screen.height - StatusH) return true; // 底部狀態列
            if (mousePos.x >= Screen.width - PaletteW) return true; // 右側屬性面板
            if (_showNew && CenteredRect(420, 380).Contains(new Vector2(mousePos.x, ty))) return true;
            if (_showSave && CenteredRect(460, 210).Contains(new Vector2(mousePos.x, ty))) return true;
            if (_showLoad && CenteredRect(460, 340).Contains(new Vector2(mousePos.x, ty))) return true;
            if (_showBg && CenteredRect(420, 280).Contains(new Vector2(mousePos.x, ty))) return true;
            if (_showFlags && CenteredRect(480, 420).Contains(new Vector2(mousePos.x, ty))) return true;
            if (_showScreenFx && CenteredRect(560, 360).Contains(new Vector2(mousePos.x, ty))) return true;
            if (CurrentTool == EditTool.EffectPreview) return true; // 預覽器佔滿畫面，不編輯地圖
            if (CurrentTool == EditTool.Object && ObjCtl()?.Selected != null
                && mousePos.x <= RailW + InspectorW && ty >= Screen.height - StatusH - InspectorH)
                return true;                                // 物件選取面板（已往右讓開工具列）
            return false;
        }

        void OnGUI()
        {
            DrawTopBar();
            DrawToolRail();
            DrawStatusBar();
            if (CurrentTool == EditTool.Object)
            {
                DrawObjectPalette();
                DrawObjectInspector();
            }
            else if (CurrentTool == EditTool.Walkable)
            {
                DrawWalkablePanel();
            }
            else if (CurrentTool == EditTool.Trigger)
            {
                DrawTriggerPanel();
            }
            else if (CurrentTool == EditTool.Npc)
            {
                DrawNpcPanel();
            }
            else if (CurrentTool == EditTool.SceneFx)
            {
                DrawSceneFxPanel();
            }
            else if (CurrentTool == EditTool.Light)
            {
                DrawLightPanel();
            }
            else if (CurrentTool == EditTool.Cutscene)
            {
                DrawCutscenePanel();
            }
            else if (CurrentTool == EditTool.EffectPreview)
            {
                if (_preview == null) _preview = new EffectPreviewUI();
                var pvRect = new Rect(RailW, TopBarH, Screen.width - RailW, Screen.height - TopBarH - StatusH);
                _preview.Draw(pvRect);
                // 關閉鈕畫在預覽器之後 → 疊在它上面。預覽器佔滿整塊可視區，沒有出口的話只能靠切別的工具離開。
                if (GUI.Button(new Rect(pvRect.xMax - 96f, pvRect.y + 6f, 88f, 24f), "✕ 關閉"))
                    CurrentTool = _toolBeforePreview;
            }
            if (_showNew) DrawNewDialog();
            if (_showSave) DrawSaveDialog();
            if (_showLoad) DrawLoadDialog();
            if (_showBg) DrawBgDialog();
            if (_showFlags) DrawFlagManager();
            if (_showScreenFx) DrawScreenFxTable();
        }

        void DrawTopBar()
        {
            GUILayout.BeginArea(new Rect(0, 0, Screen.width, TopBarH), GUI.skin.box);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("新建地圖", GUILayout.Width(80))) OpenDialog(newDlg: true);
            if (GUILayout.Button("存檔", GUILayout.Width(50)))
            {
                var m = MapSession.Instance?.Map;
                if (m != null)
                {
                    _saveName = string.IsNullOrEmpty(MapSession.Instance.CurrentPath) ? m.name : Path.GetFileNameWithoutExtension(MapSession.Instance.CurrentPath);
                    OpenDialog(saveDlg: true);
                }
            }
            if (GUILayout.Button("讀檔", GUILayout.Width(50))) OpenDialog(loadDlg: true);
            if (GUILayout.Button("聚焦", GUILayout.Width(50)))
                _cam?.FrameMap(MapSession.Instance?.Map);
            if (GUILayout.Button("刷新素材", GUILayout.Width(70)))
            {
                MapSession.Instance?.ReloadCatalog();
                SpriteCache.Clear();
                Preview.TeleportMarkerPreview.ClearCache();   // 傳送點外型改了(VfxTable/PNG)也一起重讀
                _objects = null;
            }
            if (GUILayout.Button("背景", GUILayout.Width(50)) && MapSession.Instance?.Map != null) OpenDialog(bgDlg: true);

            // 底部 UI 參考層：按一下疊上遊戲底部操控列（世界空間、貼齊地圖底部置中、半透明），再按一下隱藏。
            GUI.color = (_bottomUi != null && _bottomUi.Visible) ? Color.cyan : Color.white;
            if (GUILayout.Button("顯示底部ui", GUILayout.Width(90)))
            {
                if (_bottomUi == null) _bottomUi = FindObjectOfType<BottomUiOverlay>();
                if (_bottomUi != null)
                {
                    if (_bottomUi.Visible) { _bottomUi.Hide(); _statusMsg = "已隱藏底部 UI 參考層"; }
                    else if (_bottomUi.Show()) _statusMsg = "已疊上底部 UI 參考層（半透明、貼齊地圖底部）";
                    else _statusMsg = "找不到底部 UI 圖：請先執行 選單 DipanMapEditor→同步素材（全部 module）";
                }
            }

            // 照明預覽：把場景壓暗、讓場上的燈照回來，看接近遊戲的實際效果。放在頂部列而不是照明面板裡，
            // 是因為它是「檢視模式」——擺地上物、畫地磚時也會想開著看氣氛。開關狀態記在 PlayerPrefs。
            var lp = LightPrev();
            GUI.color = (lp != null && lp.Enabled) ? Color.cyan : Color.white;
            if (GUILayout.Button("照明預覽", GUILayout.Width(80)) && lp != null)
            {
                lp.Toggle();
                _statusMsg = lp.Enabled
                    ? $"照明預覽開啟（環境亮度 {lp.EnvBright}）——到「照明」面板可調亮度"
                    : "照明預覽關閉";
            }

            // 傳送點對位：把遊戲真正的傳送點特效畫在畫布上（同尺寸、同幀率），Trigger 工具下可直接拖曳對齊門。
            // 門的美術畫在背景圖裡、資料裡沒有門的位置，所以外型只能人工對 —— 這個模式讓你「看著對」而不是猜座標。
            var tp = TpPrev();
            GUI.color = (tp != null && tp.Enabled) ? Color.cyan : Color.white;
            if (GUILayout.Button("傳送點對位", GUILayout.Width(90)) && tp != null)
            {
                tp.Toggle();
                if (tp.Enabled) CurrentTool = EditTool.Trigger;   // 開了就切到 Trigger 工具，直接可以拖
                _statusMsg = tp.Enabled
                    ? "傳送點對位開啟：直接拖曳畫布上的傳送點外型即可對齊門（半透明＝該點勾掉了「使用傳送點外型」）"
                    : "傳送點對位關閉";
            }
            GUI.color = Color.white;

            GUILayout.Space(12);
            // 旗標不是工具（開的是彈窗），特效預覽器是佔滿畫面的獨立模式（不編輯地圖）——
            // 兩者都不屬於左側工具列，留在頂部列。
            if (GUILayout.Button("旗標", GUILayout.Width(50)))
            {
                if (CurrentTool == EditTool.EffectPreview) CurrentTool = _toolBeforePreview;
                _showFlags = true; _flagMsg = "";
            }
            // 特效預覽器：可切換——已經在預覽器裡就按同一顆退出，回到進來前的工具。
            GUI.color = CurrentTool == EditTool.EffectPreview ? Color.cyan : Color.white;
            if (GUILayout.Button(CurrentTool == EditTool.EffectPreview ? "關閉預覽器" : "特效預覽器", GUILayout.Width(90)))
            {
                if (CurrentTool == EditTool.EffectPreview) CurrentTool = _toolBeforePreview;
                else
                {
                    _toolBeforePreview = CurrentTool;
                    CurrentTool = EditTool.EffectPreview;
                    _showNew = _showSave = _showLoad = _showBg = _showFlags = _showScreenFx = false; // 開預覽器就收起所有彈窗（含新建地圖）
                }
            }
            GUI.color = Color.white;

            // 地圖資訊與狀態訊息移到底部狀態列（原本夾在按鈕中間，長度不固定，是把頂部列擠爆的主因之一）
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ---- 左側垂直工具列 ----
        // 工具從橫排改成直排：垂直空間幾乎用不完，之後再加工具也不會像橫排那樣往螢幕外擠。
        void DrawToolRail()
        {
            GUILayout.BeginArea(new Rect(0, TopBarH, RailW, Screen.height - TopBarH - StatusH), GUI.skin.box);
            GUILayout.Space(4);
            RailButton("物件", EditTool.Object);
            RailButton("可走", EditTool.Walkable);
            RailButton("Trigger", EditTool.Trigger);
            GUILayout.Space(10);
            RailButton("場景特效", EditTool.SceneFx);
            RailButton("照明", EditTool.Light);
            RailButton("劇情", EditTool.Cutscene);
            RailButton("NPC", EditTool.Npc);
            GUILayout.EndArea();
        }

        void RailButton(string label, EditTool tool)
        {
            GUI.color = CurrentTool == tool ? Color.cyan : Color.white;
            if (GUILayout.Button(label, GUILayout.Width(66), GUILayout.Height(32)))
            {
                CurrentTool = tool;
                if (tool == EditTool.Trigger) TriggerPaintMode = true;   // 沿用舊行為：切到 Trigger 就進塗格模式
            }
            GUI.color = Color.white;
        }

        // ---- 底部狀態列 ----
        void DrawStatusBar()
        {
            GUILayout.BeginArea(new Rect(0, Screen.height - StatusH, Screen.width, StatusH), GUI.skin.box);
            GUILayout.BeginHorizontal();
            var map = MapSession.Instance?.Map;
            if (map != null)
                GUILayout.Label($"地圖：{map.name}　|　module：{map.module}　|　{map.width}×{map.height} 格　|　tile {map.tileSize}");
            else
                GUILayout.Label("尚無地圖，請按「新建地圖」");
            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_statusMsg)) { GUILayout.Label(_statusMsg); GUILayout.Space(16); }
            GUILayout.Label("左鍵畫/擦　中鍵·右鍵平移　滾輪縮放");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ---- 存檔 / 讀檔 ----

        /// <summary>對話框互斥：開其中一個就關掉其他。</summary>
        void OpenDialog(bool newDlg = false, bool saveDlg = false, bool loadDlg = false, bool bgDlg = false)
        {
            if (CurrentTool == EditTool.EffectPreview) CurrentTool = _toolBeforePreview; // 從預覽器點「新建/存/讀/背景」→ 先離開預覽器
            _showNew = newDlg;
            _showSave = saveDlg;
            _showLoad = loadDlg;
            _showBg = bgDlg;
        }

        void DrawBgDialog()
        {
            var map = MapSession.Instance?.Map;
            if (map == null) { _showBg = false; return; }
            const int w = 420, h = 280;
            GUILayout.BeginArea(CenteredRect(w, h), GUI.skin.box);
            GUILayout.Label("選擇背景圖");
            GUILayout.Space(4);

            GUI.color = string.IsNullOrEmpty(map.backgroundId) ? Color.cyan : Color.white;
            if (GUILayout.Button("無（純黑底，用 tile 鋪）"))
            { UndoManager.Push(); map.backgroundId = ""; _statusMsg = "背景：無"; _showBg = false; }
            GUI.color = Color.white;

            var bgs = BackgroundService.BuildBackgrounds(MapSession.Instance.Catalog, map.module);
            _bgScroll = GUILayout.BeginScrollView(_bgScroll, GUILayout.Height(170));
            if (bgs.Count == 0)
                GUILayout.Label("（此 module 沒有 Background 素材。\n請把背景圖放進 Background/ 再「同步素材」。）");
            foreach (var b in bgs)
            {
                GUI.color = (map.backgroundId == b.id) ? Color.cyan : Color.white;
                if (GUILayout.Button(Short(b.id)))
                { UndoManager.Push(); map.backgroundId = b.id; _statusMsg = $"背景：{Short(b.id)}"; _showBg = false; }
            }
            GUI.color = Color.white;
            GUILayout.EndScrollView();

            if (GUILayout.Button("取消")) _showBg = false;
            GUILayout.EndArea();
        }

        /// <summary>把畫布寬設成「高 × 背景長寬比」，減少背景被拉伸變形。</summary>
        void ApplyBgAspect(string id)
        {
            var item = MapSession.Instance?.Catalog?.Find(id);
            var tex = item != null ? SpriteCache.GetTexture(item) : null;
            if (tex == null || tex.height == 0) return;
            if (int.TryParse(_height, out int h) && h > 0)
            {
                float aspect = (float)tex.width / tex.height;
                _width = Mathf.RoundToInt(h * aspect).ToString();
            }
        }

        void DrawSaveDialog()
        {
            const int w = 460, h = 210;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), GUI.skin.box);
            GUILayout.Label("存檔");
            Field("檔名", ref _saveName);
            DrawFolderRow();
            GUILayout.Label($"→ 存成 <檔名>{MapSerializer.Extension}");
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("存檔") && !string.IsNullOrWhiteSpace(_saveName) && !string.IsNullOrWhiteSpace(_mapsDir))
            {
                Directory.CreateDirectory(_mapsDir);
                string path = Path.Combine(_mapsDir, _saveName.Trim() + MapSerializer.Extension);
                MapSession.Instance.SaveMap(path);
                RememberMapsDir(_mapsDir);
                _statusMsg = $"已存檔：{_saveName.Trim()}{MapSerializer.Extension}";
                _showSave = false;
            }
            if (GUILayout.Button("取消")) _showSave = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawLoadDialog()
        {
            const int w = 460, h = 340;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), GUI.skin.box);
            GUILayout.Label("讀檔");
            DrawFolderRow();
            GUILayout.Space(4);

            string toLoad = null;
            if (!string.IsNullOrEmpty(_mapsDir) && Directory.Exists(_mapsDir))
            {
                var files = Directory.GetFiles(_mapsDir, "*" + MapSerializer.Extension);
                _loadScroll = GUILayout.BeginScrollView(_loadScroll, GUILayout.Height(210));
                if (files.Length == 0) GUILayout.Label("（此資料夾沒有 .dipanmap 檔）");
                foreach (var f in files)
                    if (GUILayout.Button(Path.GetFileName(f))) toLoad = f;
                GUILayout.EndScrollView();
            }
            else GUILayout.Label("（資料夾不存在，請改路徑或按「選…」）");

            if (GUILayout.Button("取消")) _showLoad = false;
            GUILayout.EndArea();

            if (toLoad != null) { RememberMapsDir(_mapsDir); LoadMapFile(toLoad); }
        }

        void LoadMapFile(string path)
        {
            if (MapSession.Instance.LoadMap(path))
            {
                // 清掉跟舊地圖綁定的選取與快取
                ObjCtl()?.Deselect();
                CurrentRegion = null;
                _triggerType = null;
                _objects = null;
                _statusMsg = $"已讀入：{Path.GetFileName(path)}";
            }
            else _statusMsg = $"讀檔失敗：{Path.GetFileName(path)}";
            OpenDialog();   // 關掉所有對話框（含開機殘留的新建對話框）
        }

        static void DrawBorder(Rect r, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            float t = 2f;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        // ---- 物件（地上物）調色盤 ----

        void EnsureObjects()
        {
            string module = MapSession.Instance?.Map?.module ?? "";
            if (_objects != null && _objectsModule == module) return;
            _objects = ObjectService.BuildObjects(MapSession.Instance?.Catalog, module);
            _objectsModule = module;
            bool selValid = !string.IsNullOrEmpty(SelectedObjectAssetId)
                            && _objects.FindIndex(o => o.assetId == SelectedObjectAssetId) >= 0;
            if (!selValid)
                SelectedObjectAssetId = _objects.Count > 0 ? _objects[0].assetId : null;
        }

        void DrawObjectPalette()
        {
            EnsureObjects();
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint) _hoverObj = null;

            var rect = PanelRect;
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label($"地上物（{_objects.Count}）");
            GUILayout.Label(string.IsNullOrEmpty(SelectedObjectAssetId) ? "未選取" : $"選取：{Short(SelectedObjectAssetId)}");
            GUILayout.Label("放置：選素材→左鍵點畫布\n選取：左鍵點物件\n移動：Ctrl+左鍵拖曳");
            GUILayout.Space(4);

            if (_objects.Count == 0)
            {
                GUILayout.Label("沒有可放置的地上物。\n請把物件 png 放進關卡的\nEnvironment 資料夾，\n再同步素材。");
                GUILayout.EndArea();
                return;
            }

            _objScroll = GUILayout.BeginScrollView(_objScroll);
            int perRow = Mathf.Max(1, (int)((PaletteW - 24) / (Thumb + 4)));
            int col = 0;
            bool rowOpen = false;
            foreach (var o in _objects)
            {
                if (col == 0) { GUILayout.BeginHorizontal(); rowOpen = true; }
                bool clicked = GUILayout.Button(GUIContent.none, GUILayout.Width(Thumb), GUILayout.Height(Thumb));
                Rect r = GUILayoutUtility.GetLastRect();
                var tex = SpriteCache.GetTexture(o.source);
                if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
                if (o.assetId == SelectedObjectAssetId) DrawBorder(r, Color.cyan);
                // 動畫物件加標記，讓設計師一眼分辨（▶ + 幀數）。
                if (o.source != null && o.source.IsAnimated)
                {
                    var badge = new Rect(r.x + 2, r.y + 2, 24, 16);
                    var oldc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.6f);
                    GUI.DrawTexture(badge, Texture2D.whiteTexture); GUI.color = oldc;
                    GUI.Label(badge, $" ▶{o.source.frameCount}");
                }
                if (clicked) SelectedObjectAssetId = o.assetId;
                if (repaint && r.Contains(Event.current.mousePosition)) _hoverObj = o.source;
                col++;
                if (col >= perRow) { GUILayout.EndHorizontal(); rowOpen = false; col = 0; }
            }
            if (rowOpen) GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // 懸浮放大預覽（畫在面板外、螢幕座標）
            if (_hoverObj != null) DrawHoverPreview(_hoverObj);
        }

        void DrawHoverPreview(CatalogItem item)
        {
            var tex = SpriteCache.GetTexture(item);
            if (tex == null) return;

            const float boxW = 200f, boxH = 232f, pad = 14f;
            Vector2 m = Event.current.mousePosition;       // 此處為螢幕座標
            float x = m.x - boxW - 16f; if (x < 4f) x = m.x + 16f;
            float y = Mathf.Clamp(m.y - boxH / 2f, 4f, Screen.height - boxH - 4f);
            var box = new Rect(x, y, boxW, boxH);

            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.88f);       // 深色底，蓋住後面場景才看得清
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Box(box, GUIContent.none);

            float img = boxW - pad * 2;
            GUI.DrawTexture(new Rect(x + pad, y + pad, img, img), tex, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(x + 8f, y + boxH - 26f, boxW - 16f, 22f), Short(item.id));
        }

        void DrawObjectInspector()
        {
            var ctl = ObjCtl();
            if (ctl == null || ctl.SelectionCount == 0) return;   // 沒選取就不畫面板（避免擋住點擊）
            if (ctl.SelectionCount > 1) { DrawMultiObjectInspector(ctl); return; }   // 多選＝精簡面板
            var sel = ctl.Selected;
            if (sel == null) return;

            float h = Mathf.Min(InspectorH, Screen.height - TopBarH - StatusH - 8f);
            var rect = new Rect(RailW, Screen.height - StatusH - h, InspectorW, h);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label($"選取：{Short(sel.assetId)}　縮放 {sel.scaleX:0.00}　角度 {sel.rot:0}°　層 {sel.zOrder}");

            // 座標：未編輯時顯示物件當前座標；改數值或按 ± 就移動（每次 ±0.1）
            _objInspScroll = GUILayout.BeginScrollView(_objInspScroll);

            GUILayout.BeginHorizontal();
            GUILayout.Label("X", GUILayout.Width(12));
            if (GUILayout.Button("－", GUILayout.Width(22))) { UndoManager.Push(); ctl.SetPosition(sel.x - 0.1f, sel.y); }
            GUI.SetNextControlName("objX");
            string sx = GUILayout.TextField(_objXBuf, GUILayout.Width(42));
            if (GUILayout.Button("＋", GUILayout.Width(22))) { UndoManager.Push(); ctl.SetPosition(sel.x + 0.1f, sel.y); }
            GUILayout.Space(6);
            GUILayout.Label("Y", GUILayout.Width(12));
            if (GUILayout.Button("－", GUILayout.Width(22))) { UndoManager.Push(); ctl.SetPosition(sel.x, sel.y - 0.1f); }
            GUI.SetNextControlName("objY");
            string sy = GUILayout.TextField(_objYBuf, GUILayout.Width(42));
            if (GUILayout.Button("＋", GUILayout.Width(22))) { UndoManager.Push(); ctl.SetPosition(sel.x, sel.y + 0.1f); }
            GUILayout.EndHorizontal();
            bool editingX = GUI.GetNameOfFocusedControl() == "objX";
            bool editingY = GUI.GetNameOfFocusedControl() == "objY";
            if (sx != _objXBuf) { _objXBuf = sx; if (float.TryParse(sx, out var vx)) ctl.SetPosition(vx, sel.y); }
            if (sy != _objYBuf) { _objYBuf = sy; if (float.TryParse(sy, out var vy)) ctl.SetPosition(sel.x, vy); }
            if (!editingX) _objXBuf = sel.x.ToString("0.###");
            if (!editingY) _objYBuf = sel.y.ToString("0.###");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("翻轉H")) { UndoManager.Push(); ctl.FlipH(); }
            if (GUILayout.Button("翻轉V")) { UndoManager.Push(); ctl.FlipV(); }
            if (GUILayout.Button("複製")) { UndoManager.Push(); ctl.DuplicateSelected(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("縮小")) { UndoManager.Push(); ctl.ScaleBy(0.9f); }
            if (GUILayout.Button("放大")) { UndoManager.Push(); ctl.ScaleBy(1.1f); }
            if (GUILayout.Button("旋轉 15°")) { UndoManager.Push(); ctl.Rotate(15f); }
            GUILayout.EndHorizontal();
            // 「層」：+1 的實際語意是「**永遠畫在角色前面、完全不參與 Y 排序**」（排序帶 13000~18999，
            // 玩家永遠在 0 層的 7000~12999），設在大型落地家具上會造成「玩家站在它前面、頭還是被蓋住」。
            // 它只適合「放在別的東西上面、玩家永遠站不到它前面」的小型桌上物（花瓶/香爐/燭台）。
            // 原本只在面板最上面那行小小顯示「層 N」，很容易漏看，所以這裡獨立一行、寫出語意並上色。
            // 場景上另有常駐標示（見 ObjectSelectionOverlay）。詳見 readme/PROBLEMS.md E16。
            var _zPrevCol = GUI.color;
            if (sel.zOrder > 0) GUI.color = new Color(1f, 0.62f, 0.2f);
            else if (sel.zOrder < 0) GUI.color = new Color(0.5f, 0.75f, 1f);
            GUILayout.Label(ZOrderText(sel.zOrder));
            GUI.color = _zPrevCol;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("上移層")) { UndoManager.Push(); ctl.RaiseZ(); }
            if (GUILayout.Button("下移層")) { UndoManager.Push(); ctl.LowerZ(); }
            GUILayout.EndHorizontal();

            // 出現條件：完成 N 關後才出現（0＝一開始就有）。遊戲端進地圖當下依「完成關卡數」判定，未達則不生此物件。
            // 同 hp/fps 的防退回作法：沒在打字時 buffer 先跟著物件值，±按鈕才不會被回寫誤判而回退。
            bool editingAppear = GUI.GetNameOfFocusedControl() == "objAppear";
            if (!editingAppear) _objAppearBuf = sel.appearAfterClears.ToString();
            GUILayout.BeginHorizontal();
            GUILayout.Label("出現:完成", GUILayout.Width(72));
            if (GUILayout.Button("－", GUILayout.Width(24))) { UndoManager.Push(); sel.appearAfterClears = Mathf.Max(0, sel.appearAfterClears - 1); _objAppearBuf = sel.appearAfterClears.ToString(); }
            GUI.SetNextControlName("objAppear");
            string sap = GUILayout.TextField(_objAppearBuf, GUILayout.Width(40));
            if (GUILayout.Button("＋", GUILayout.Width(24))) { UndoManager.Push(); sel.appearAfterClears += 1; _objAppearBuf = sel.appearAfterClears.ToString(); }
            GUILayout.Label("關後", GUILayout.Width(40));
            GUILayout.EndHorizontal();
            if (editingAppear && sap != _objAppearBuf)
            {
                _objAppearBuf = sap;
                if (int.TryParse(sap, out var vap) && vap >= 0) sel.appearAfterClears = vap;
            }
            if (sel.appearAfterClears <= 0)
            {
                GUILayout.Label("（0＝一開始就出現）");
            }
            else
            {
                // 範圍：每周目（本周目完成數、輪迴會再隱藏）／永久（曾完成過就一直在）。
                bool isCycle = sel.appearScope != "lifetime";
                GUILayout.BeginHorizontal();
                GUILayout.Label("範圍", GUILayout.Width(72));
                GUI.color = isCycle ? Color.cyan : Color.white;
                if (GUILayout.Button("每周目")) { if (!isCycle) { UndoManager.Push(); sel.appearScope = "cycle"; } }
                GUI.color = !isCycle ? Color.cyan : Color.white;
                if (GUILayout.Button("永久")) { if (isCycle) { UndoManager.Push(); sel.appearScope = "lifetime"; } }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
                GUILayout.Label(isCycle ? "（每周目完成 N 關才出現，輪迴重置）" : "（曾完成過 N 關就永久出現）");
            }

            // ── 出現條件②：某旗標為 true 才顯示（與上面「完成 N 關」同時設＝兩者都要滿足）。旗標用旗標管理器登記（輸入 id→確認），與觸發點/破壞旗標同一套。
            {
                var objSelF = sel;
                GUILayout.BeginHorizontal();
                GUILayout.Label("出現旗標", GUILayout.Width(64));
                DrawFlagFieldCore(objSelF.appearFlag ?? "", "obj" + objSelF.GetHashCode() + "/appearFlag", false,
                    val => objSelF.appearFlag = val);
                GUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(sel.appearFlag))
                {
                    bool editingDelay = GUI.GetNameOfFocusedControl() == "objAppearDelay";
                    if (!editingDelay) _objAppearDelayBuf = sel.appearDelaySeconds.ToString("0.#");
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("現身延遲", GUILayout.Width(64));
                    if (GUILayout.Button("－", GUILayout.Width(24))) { UndoManager.Push(); sel.appearDelaySeconds = Mathf.Max(0f, sel.appearDelaySeconds - 0.5f); _objAppearDelayBuf = sel.appearDelaySeconds.ToString("0.#"); }
                    GUI.SetNextControlName("objAppearDelay");
                    string sd = GUILayout.TextField(_objAppearDelayBuf, GUILayout.Width(44));
                    if (GUILayout.Button("＋", GUILayout.Width(24))) { UndoManager.Push(); sel.appearDelaySeconds += 0.5f; _objAppearDelayBuf = sel.appearDelaySeconds.ToString("0.#"); }
                    GUILayout.Label("秒後現身", GUILayout.Width(72));
                    GUILayout.EndHorizontal();
                    if (editingDelay && sd != _objAppearDelayBuf)
                    {
                        _objAppearDelayBuf = sd;
                        if (float.TryParse(sd, out var vd) && vd >= 0f) sel.appearDelaySeconds = vd;
                    }
                    bool nextFade = GUILayout.Toggle(sel.appearFade, " 現身淡入");
                    if (nextFade != sel.appearFade) { UndoManager.Push(); sel.appearFade = nextFade; }
                    GUILayout.Label("（延遲/淡入只在「靠旗標中途現身」時生效）");
                }
            }

            // ── 消失旗標：此旗標為 true 時這個地上物消失（與「出現旗標」相反）。進圖時已成立＝根本不生；中途成立＝立即銷毀。
            //    搭配觸發鏈 setFlag 用（例：pickup 撿起佛燈 → setFlag → 佛燈地上物消失）。用旗標管理器登記的旗標。
            {
                var objSelG = sel;
                GUILayout.BeginHorizontal();
                GUILayout.Label("消失旗標", GUILayout.Width(64));
                DrawFlagFieldCore(objSelG.disappearFlag ?? "", "obj" + objSelG.GetHashCode() + "/disappearFlag", false,
                    val => objSelG.disappearFlag = val);
                GUILayout.EndHorizontal();
            }

            // ── 照明（火把/燈籠/香爐/地上的佛燈…）──
            //    發光半徑 >0 才會展開其餘選項。在「暗氛圍」地圖(幽暗/噩夢/深海恐怖)、
            //    或 MapsTable「環境亮度」<100 的地圖上才看得到效果；同框最多 12 盞。
            {
                bool editingLight = GUI.GetNameOfFocusedControl() == "objLight";
                if (!editingLight) _objLightBuf = sel.lightRadius.ToString("0.##");
                GUILayout.BeginHorizontal();
                GUILayout.Label("發光半徑", GUILayout.Width(64));
                if (GUILayout.Button("－", GUILayout.Width(24))) { UndoManager.Push(); sel.lightRadius = Mathf.Max(0f, sel.lightRadius - 0.5f); _objLightBuf = sel.lightRadius.ToString("0.##"); }
                GUI.SetNextControlName("objLight");
                string sl = GUILayout.TextField(_objLightBuf, GUILayout.Width(56));
                if (GUILayout.Button("＋", GUILayout.Width(24))) { UndoManager.Push(); sel.lightRadius += 0.5f; _objLightBuf = sel.lightRadius.ToString("0.##"); }
                GUILayout.Label("格（0=不發光）", GUILayout.Width(96));
                GUILayout.EndHorizontal();
                if (editingLight && sl != _objLightBuf)
                {
                    _objLightBuf = sl;
                    if (float.TryParse(sl, out var vl) && vl >= 0f) sel.lightRadius = vl;
                }

                // 發光半徑 >0 才展開照明細項（不發光的地上物不用被這些欄位洗版）。
                if (sel.lightRadius > 0f)
                {
                    // 一鍵套用常見燈種：先按這個，再微調底下欄位。
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("燈種預設", GUILayout.Width(64));
                    if (GUILayout.Button("火把", GUILayout.Width(46))) { UndoManager.Push(); ApplyLightPreset(sel, "FFC785", 1.0f, 1.0f, 1.0f, 0.46f); }
                    if (GUILayout.Button("燭火", GUILayout.Width(46))) { UndoManager.Push(); ApplyLightPreset(sel, "FFD9A0", 0.7f, 1.4f, 1.6f, 0.30f); }
                    if (GUILayout.Button("燈籠", GUILayout.Width(46))) { UndoManager.Push(); ApplyLightPreset(sel, "FFB05A", 1.0f, 0.5f, 0.6f, 0.55f); }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(64);
                    if (GUILayout.Button("鬼火", GUILayout.Width(46))) { UndoManager.Push(); ApplyLightPreset(sel, "7CFFB0", 0.8f, 1.8f, 0.35f, 0.25f); }
                    if (GUILayout.Button("月光", GUILayout.Width(46))) { UndoManager.Push(); ApplyLightPreset(sel, "CFE4FF", 0.9f, 0.0f, 1.0f, 0.40f); }
                    if (GUILayout.Button("爐火", GUILayout.Width(46))) { UndoManager.Push(); ApplyLightPreset(sel, "FF8A3D", 1.5f, 1.2f, 0.9f, 0.60f); }
                    GUILayout.EndHorizontal();

                    // 亮度：1=標準、<1 微光、>1 刺眼。
                    LightNumField("亮度", "objLightInt", ref _objLightIntBuf,
                                  () => sel.lightIntensity, x => sel.lightIntensity = x, 0.1f, 0f, "倍（1=標準）");
                    // 搖晃強度：0=完全不動（電燈/月光）、1=標準燭火、2=狂亂火焰。
                    LightNumField("搖晃強度", "objLightFlk", ref _objLightFlkBuf,
                                  () => sel.lightFlicker, x => sel.lightFlicker = x, 0.1f, 0f, "（0=不晃）");
                    // 搖晃速度：小=油燈慢晃、大=營火急促跳動。
                    LightNumField("搖晃速度", "objLightSpd", ref _objLightSpdBuf,
                                  () => sel.lightFlickerSpeed, x => sel.lightFlickerSpeed = x, 0.1f, 0.01f, "倍（1=標準）");
                    // 邊緣柔和度：小=瀰漫柔邊、大=範圍內均勻邊緣硬（聚光燈）。
                    LightNumField("邊緣柔和", "objLightSof", ref _objLightSofBuf,
                                  () => sel.lightSoftness, x => sel.lightSoftness = Mathf.Clamp01(x), 0.05f, 0f, "（小=柔 大=硬）");

                    // 光色：6 碼 16 進位 RRGGBB（不含 #）。空＝預設暖橘。右邊畫一塊實際顏色方便確認。
                    bool editingCol = GUI.GetNameOfFocusedControl() == "objLightCol";
                    if (!editingCol) _objLightColBuf = sel.lightColor ?? "";
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("光色 RGB", GUILayout.Width(64));
                    GUI.SetNextControlName("objLightCol");
                    string sc = GUILayout.TextField(_objLightColBuf ?? "", GUILayout.Width(70));
                    var swatch = GUILayoutUtility.GetRect(22f, 16f, GUILayout.Width(22));
                    var prevCol = GUI.color;
                    GUI.color = ParseHexColor(sel.lightColor);
                    GUI.DrawTexture(swatch, Texture2D.whiteTexture);
                    GUI.color = prevCol;
                    GUILayout.Label("空=暖橘", GUILayout.Width(60));
                    GUILayout.EndHorizontal();
                    if (editingCol && sc != _objLightColBuf) { _objLightColBuf = sc; sel.lightColor = sc.Trim().TrimStart('#'); }
                }
            }

            // 可走（勾選＝這個地上物不擋路、不設碰撞、畫在角色腳下；例：木板/地毯可踩上去）。
            bool nextWalk = GUILayout.Toggle(sel.walkable, " 可走");
            if (nextWalk != sel.walkable) { UndoManager.Push(); sel.walkable = nextWalk; if (nextWalk) sel.passThrough = false; }
            // 可穿越（勾選＝無碰撞可穿過，但照常 Y-sort 依 Y 前後遮蔽；給站立的鬼魂/煙/光這種穿透物）。與可走互斥。
            bool nextPass = GUILayout.Toggle(sel.passThrough, " 可穿越");
            if (nextPass != sel.passThrough) { UndoManager.Push(); sel.passThrough = nextPass; if (nextPass) sel.walkable = false; }

            // 不可被摧毀（勾選＝血量 -1）；未勾選才顯示可調的數值血量
            bool indes = sel.hp == -1;
            bool nextIndes = GUILayout.Toggle(indes, " 不可被摧毀（血量 -1）");
            if (nextIndes != indes) { UndoManager.Push(); sel.hp = nextIndes ? -1 : 1; _objHpBuf = sel.hp.ToString(); }

            if (sel.hp != -1)
            {
                // 同 FPS：沒在打字時 buffer 跟著物件值（在文字框之前同步），±按鈕才不會被回寫誤判而回退。
                bool editingHp = GUI.GetNameOfFocusedControl() == "objHp";
                if (!editingHp) _objHpBuf = sel.hp.ToString();

                GUILayout.BeginHorizontal();
                GUILayout.Label("血量", GUILayout.Width(40));
                if (GUILayout.Button("－", GUILayout.Width(24))) { UndoManager.Push(); sel.hp = Mathf.Max(0, sel.hp - 1); _objHpBuf = sel.hp.ToString(); }
                GUI.SetNextControlName("objHp");
                string sh = GUILayout.TextField(_objHpBuf, GUILayout.Width(56));
                if (GUILayout.Button("＋", GUILayout.Width(24))) { UndoManager.Push(); sel.hp += 1; _objHpBuf = sel.hp.ToString(); }
                GUILayout.EndHorizontal();

                if (editingHp && sh != _objHpBuf)
                {
                    _objHpBuf = sh;
                    if (int.TryParse(sh, out var vh) && vh >= 0) sel.hp = vh;
                }
            }

            // 破壞觸發旗標：破壞這個可破壞物件時把指定旗標設為 true（給觸發鏈 requireFlag 用，例：打破供品→改變劇情走向）。
            // 只有「可破壞」（非可走、非不可摧毀）的物件才有效；用旗標管理器登記的旗標（輸入 id→確認），與觸發點同一套。
            if (!sel.walkable && !sel.passThrough && sel.hp != -1)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("破壞旗標", GUILayout.Width(64));
                var objSel = sel;   // 供 lambda 捕捉（各實例自己的暫存鍵，切換選取不殘留輸入）
                DrawFlagFieldCore(objSel.breakFlag ?? "", "obj" + objSel.GetHashCode() + "/breakFlag", false,
                    val => objSel.breakFlag = val);
                GUILayout.EndHorizontal();
            }

            // 動畫地上物：每實例可調播放 FPS（靜態物件不顯示此列）。改了下一幀即生效（ObjectView.Update 讀 animFps）。
            var animItem = MapSession.Instance?.Catalog?.Find(sel.assetId);
            if (animItem != null && animItem.IsAnimated)
            {
                const float MinFps = 0.5f, MaxFps = 60f;
                // 沒在編輯文字框時，buffer 永遠跟著物件值（含按鈕剛改完的值）。在文字框「之前」同步，
                // 兩顆 ±按鈕才不會被後面的「回寫」誤判成打字而回退（這是先前 ＋ 失效的根因）。
                bool editingFps = GUI.GetNameOfFocusedControl() == "objFps";
                if (!editingFps) _objFpsBuf = sel.animFps.ToString("0.#");

                GUILayout.BeginHorizontal();
                GUILayout.Label($"動畫 {animItem.frameCount} 幀  FPS", GUILayout.Width(96));
                if (GUILayout.Button("－", GUILayout.Width(24))) { UndoManager.Push(); sel.animFps = Mathf.Clamp(sel.animFps - 1f, MinFps, MaxFps); _objFpsBuf = sel.animFps.ToString("0.#"); }
                GUI.SetNextControlName("objFps");
                string sf = GUILayout.TextField(_objFpsBuf, GUILayout.Width(48));
                if (GUILayout.Button("＋", GUILayout.Width(24))) { UndoManager.Push(); sel.animFps = Mathf.Clamp(sel.animFps + 1f, MinFps, MaxFps); _objFpsBuf = sel.animFps.ToString("0.#"); }
                GUILayout.EndHorizontal();

                // 只有在「真的聚焦文字框打字」時才把輸入回寫到物件，避免把按鈕的改動誤判成打字而回退。
                if (editingFps && sf != _objFpsBuf)
                {
                    _objFpsBuf = sf;
                    if (float.TryParse(sf, out var vf) && vf > 0f) sel.animFps = Mathf.Clamp(vf, MinFps, MaxFps);
                }

                // 播放模式：循環 / 乒乓。乒乓 = 來回播，AI 產的圖首尾接不順時用它讓接縫消失（適合佛像等氛圍動畫；
                // 有方向性的動畫會變「正放再倒放」）。改了預覽即時反映。
                GUILayout.BeginHorizontal();
                GUILayout.Label("播放", GUILayout.Width(96));
                GUI.color = !sel.pingPong ? Color.cyan : Color.white;
                if (GUILayout.Button("循環")) { if (sel.pingPong) { UndoManager.Push(); sel.pingPong = false; } }
                GUI.color = sel.pingPong ? Color.cyan : Color.white;
                if (GUILayout.Button("乒乓(來回)")) { if (!sel.pingPong) { UndoManager.Push(); sel.pingPong = true; } }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();

                // 播一次：勾了＝播到最後一幀就停住（不循環，例：跪拜停在跪姿）。勾選時上面的循環/乒乓不生效。
                bool nextOnce = GUILayout.Toggle(sel.playOnce, " 播一次（停在最後一幀）");
                if (nextOnce != sel.playOnce) { UndoManager.Push(); sel.playOnce = nextOnce; if (_objView != null) _objView.ReplayAnim(sel); }
                if (GUILayout.Button("重播預覽") && _objView != null) _objView.ReplayAnim(sel);
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("取消選取")) ctl.Deselect();
            if (GUILayout.Button("刪除")) { UndoManager.Push(); ctl.DeleteSelected(); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // 多選時的精簡面板：只給整組操作（複製全部／刪除全部／取消）。單物件的座標/翻轉/縮放等仍需回到單選才顯示。
        void DrawMultiObjectInspector(DipanMapEditor.Tools.ObjectController ctl)
        {
            var rect = new Rect(RailW, Screen.height - StatusH - 132, InspectorW, 132);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label($"已選 {ctl.SelectionCount} 個地上物");
            GUILayout.Label("Cmd＋點＝加選／取消　Ctrl＋拖＝一起移動");
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("複製")) { UndoManager.Push(); ctl.DuplicateSelected(); }
            if (GUILayout.Button("刪除")) { UndoManager.Push(); ctl.DeleteSelected(); }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("取消選取")) ctl.Deselect();
            GUILayout.EndArea();
        }

        // ================= 照明欄位的小工具（地上物面板用） =================

        /// <summary>套用一種燈種預設（光色／亮度／搖晃強度／搖晃速度／邊緣柔和度）。發光半徑不動，由使用者自己定範圍。</summary>
        static void ApplyLightPreset(ObjectInstance sel, string hex, float intensity, float flicker, float speed, float softness)
        {
            sel.lightColor = hex;
            sel.lightIntensity = intensity;
            sel.lightFlicker = flicker;
            sel.lightFlickerSpeed = speed;
            sel.lightSoftness = softness;
        }

        /// <summary>套用燈種預設到「獨立光源」（欄位名與地上物的照明欄不同，所以另開一個多載）。發光半徑不動。</summary>
        static void ApplyLightPreset(LightInstance sel, string hex, float intensity, float flicker, float speed, float softness)
        {
            sel.color = hex;
            sel.intensity = intensity;
            sel.flicker = flicker;
            sel.flickerSpeed = speed;
            sel.softness = softness;
        }

        /// <summary>
        /// 照明用的「－ 數字 ＋」一列。與發光半徑同一套寫法：聚焦中才吃使用者輸入，沒聚焦時把現值灌回暫存，
        /// 這樣可以自由清空重打，也不會被舊值蓋回去。
        /// </summary>
        void LightNumField(string label, string ctrlName, ref string buf,
                           System.Func<float> get, System.Action<float> set, float step, float min, string suffix)
        {
            bool editing = GUI.GetNameOfFocusedControl() == ctrlName;
            if (!editing) buf = get().ToString("0.##");
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(64));
            if (GUILayout.Button("－", GUILayout.Width(24))) { UndoManager.Push(); set(Mathf.Max(min, get() - step)); buf = get().ToString("0.##"); }
            GUI.SetNextControlName(ctrlName);
            string s = GUILayout.TextField(buf ?? "", GUILayout.Width(56));
            if (GUILayout.Button("＋", GUILayout.Width(24))) { UndoManager.Push(); set(get() + step); buf = get().ToString("0.##"); }
            GUILayout.Label(suffix, GUILayout.Width(96));
            GUILayout.EndHorizontal();
            if (editing && s != buf)
            {
                buf = s;
                if (float.TryParse(s, out var v) && v >= min) set(v);
            }
        }

        /// <summary>6 碼 16 進位 RRGGBB（可含 #）→ Color；空/無效回預設暖橘（與遊戲端 LightSource.DefaultWarm 一致）。</summary>
        static Color ParseHexColor(string hex)
        {
            var warm = new Color(1.00f, 0.78f, 0.52f, 1f);
            if (string.IsNullOrWhiteSpace(hex)) return warm;
            string s = hex.Trim().TrimStart('#');
            return ColorUtility.TryParseHtmlString("#" + s, out var c) ? c : warm;
        }

        // ---- 可走/不可走筆刷面板 ----

        void DrawWalkablePanel()
        {
            var map = MapSession.Instance?.Map;
            var rect = PanelRect;
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("可走 / 牆 / 水");
            if (map != null)
                GUILayout.Label($"細分 {map.Subdiv}×（每格切 {map.Subdiv}×{map.Subdiv} 子格）");
            GUILayout.Space(4);
            GUILayout.Label("筆刷");

            GUI.color = WalkBrushState == WalkableOps.Walk ? Color.cyan : Color.white;
            if (GUILayout.Button("可走（綠）")) WalkBrushState = WalkableOps.Walk;
            GUI.color = WalkBrushState == WalkableOps.Wall ? Color.cyan : Color.white;
            if (GUILayout.Button("牆（紅）擋＋反彈子彈")) WalkBrushState = WalkableOps.Wall;
            GUI.color = WalkBrushState == WalkableOps.Water ? Color.cyan : Color.white;
            if (GUILayout.Button("水/坑（藍）擋腳·子彈穿過")) WalkBrushState = WalkableOps.Water;
            GUI.color = Color.white;

            GUILayout.Space(8);
            GUILayout.Label("筆刷大小（子格）");
            // 8 種尺寸太多、單列會擠爆窄面板，改成每列 4 顆。
            for (int i = 0; i < WalkBrushSizes.Length; i++)
            {
                if (i % 4 == 0) GUILayout.BeginHorizontal();
                int s = WalkBrushSizes[i];
                GUI.color = WalkBrushSize == s ? Color.cyan : Color.white;
                if (GUILayout.Button($"{s}×{s}")) WalkBrushSize = s;
                if (i % 4 == 3 || i == WalkBrushSizes.Length - 1) { GUI.color = Color.white; GUILayout.EndHorizontal(); }
            }
            GUI.color = Color.white;

            GUILayout.Space(10);
            GUILayout.Label("整張地圖");
            if (map != null)
            {
                if (GUILayout.Button("全部改可走（綠）"))
                {
                    UndoManager.Push();
                    int n = WalkableOps.FillAll(map, WalkableOps.Walk);
                    _statusMsg = $"已將整張地圖改為可走（{n} 子格）";
                }
                if (GUILayout.Button("全部改牆（紅）"))
                {
                    UndoManager.Push();
                    int n = WalkableOps.FillAll(map, WalkableOps.Wall);
                    _statusMsg = $"已將整張地圖改為牆（{n} 子格）";
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("左鍵拖曳塗子格。\n綠 = 可走\n紅 = 牆（擋玩家＋反彈子彈）\n藍 = 水/坑（擋玩家、子彈穿過）\n新地圖初始全部為牆。\n只有此工具下才顯示疊加色。");
            GUILayout.EndArea();
        }

        // ---- Trigger 圖層面板 ----

        void DrawTriggerPanel()
        {
            var map = MapSession.Instance?.Map;
            var types = MapSession.Instance?.TriggerTypes;
            if (map == null || types == null) return;
            var regions = map.TriggerLayer.regions;

            // 當前區域若已被刪除/換地圖則清空
            if (CurrentRegion != null && !regions.Contains(CurrentRegion)) CurrentRegion = null;
            if (string.IsNullOrEmpty(_triggerType) && types.types.Count > 0) _triggerType = types.types[0].typeId;

            var rect = PanelRect;
            GUILayout.BeginArea(rect, GUI.skin.box);
            _trigScroll = GUILayout.BeginScrollView(_trigScroll);

            // 模式提示
            GUILayout.Label(TriggerPaintMode ? "模式：筆刷（ESC 切檢視）" : "模式：檢視（點區域檢查；點下方類型回筆刷）");
            GUILayout.Space(2);

            // 新區域的類型選擇（點任一類型 = 回到筆刷模式、準備畫新區域）
            GUILayout.Label("新區域類型");
            foreach (var t in types.types)
            {
                GUI.color = (TriggerPaintMode && TriggerNewRegionPerStroke && t.typeId == _triggerType) ? Color.cyan : Color.white;
                if (GUILayout.Button(t.displayName)) { _triggerType = t.typeId; CurrentRegion = null; TriggerPaintMode = true; TriggerNewRegionPerStroke = true; }
            }
            GUI.color = Color.white;
            GUILayout.Label("選類型→畫布左鍵拖曳。\n每畫一筆 = 一個獨立區域\n（各自參數）。ESC 進檢視模式\n點畫布上的區域檢查/改參數。");
            if (GUILayout.Button("＋ 手動新增空區域")) { UndoManager.Push(); CreateRegion(_triggerType, regions, types); TriggerPaintMode = true; TriggerNewRegionPerStroke = false; }

            GUILayout.Space(6);
            GUILayout.Label($"區域清單（{regions.Count}）");
            TriggerRegion toDelete = null;
            foreach (var r in regions)
            {
                GUILayout.BeginHorizontal();
                GUI.color = (r == CurrentRegion) ? Color.cyan : Color.white;
                // 點模式的傳送點不靠格子觸發，顯示「點」而不是格數（顯示 0 格會讓人以為壞了）。
                string sizeTag = (r.typeId == "teleport" && Preview.TeleportMarkerPreview.HasAnchor(r))
                    ? "點" : $"{r.cells.Count}格";
                if (GUILayout.Button($"{r.name}（{sizeTag}）", GUILayout.Width(160))) { CurrentRegion = r; TriggerPaintMode = true; TriggerNewRegionPerStroke = false; }
                GUI.color = Color.white;
                if (GUILayout.Button("刪", GUILayout.Width(36))) toDelete = r;
                GUILayout.EndHorizontal();
            }
            if (toDelete != null)
            {
                UndoManager.Push();
                regions.Remove(toDelete);
                if (CurrentRegion == toDelete) CurrentRegion = null;
            }

            // 當前區域編輯
            if (CurrentRegion != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("── 編輯區域 ──");
                GUILayout.BeginHorizontal();
                GUILayout.Label("名稱", GUILayout.Width(60));
                CurrentRegion.name = GUILayout.TextField(CurrentRegion.name);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUI.color = (TriggerPaintMode && TriggerAddCells) ? Color.cyan : Color.white;
                if (GUILayout.Button("加格")) { TriggerAddCells = true; TriggerPaintMode = true; TriggerNewRegionPerStroke = false; }
                GUI.color = (TriggerPaintMode && !TriggerAddCells) ? Color.cyan : Color.white;
                if (GUILayout.Button("減格")) { TriggerAddCells = false; TriggerPaintMode = true; TriggerNewRegionPerStroke = false; }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();

                var def = types.Find(CurrentRegion.typeId);
                if (def != null && def.paramSchema.Count > 0)
                {
                    GUILayout.Label("參數");
                    foreach (var p in def.paramSchema) DrawParamField(CurrentRegion, p);
                }

                // 傳送點：外型特效(marker)的精準位置——可獨立於功能格子點放，並在畫布即時預覽(黃十字)。
                if (CurrentRegion.typeId == "teleport") DrawTeleportMarkerAnchor();
                else MarkerPlaceActive = false;

                // 觸發鏈通用欄位（每種類型都有；遊戲端 TriggerChain 解讀）。依 group 分小節顯示。
                GUILayout.Space(4);
                GUILayout.Label("── 觸發鏈/條件（通用）──");
                string lastGroup = null;
                foreach (var p in TriggerTypeSet.ChainParams)
                {
                    if (!string.IsNullOrEmpty(p.group) && p.group != lastGroup)
                    {
                        GUILayout.Space(2);
                        GUILayout.Label($"◆ {p.group}");
                        lastGroup = p.group;
                    }
                    DrawParamField(CurrentRegion, p);
                }
                GUILayout.Label("接續觸發＝填另一個區域的名稱；\n動作型(給物品/直接傳送)被接續到\n＝立即執行，位置型＝解鎖啟用。");

                GUILayout.Label("左鍵拖曳加/減格。");
            }
            else
            {
                GUILayout.Space(8);
                GUILayout.Label("選一個區域，或按上方\n「新增此類型區域」開始塗。");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // 傳送點「錨點」小面板：錨點＝外型位置＝踩踏區中心＝落點（三者同一個點）。
        // 沒設錨點的舊傳送點仍走格子模式（見主專案 TeleportAnchor）。
        void DrawTeleportMarkerAnchor()
        {
            var r = CurrentRegion;
            GUILayout.Space(4);
            GUILayout.Label("── 錨點（外型＋踩踏＋落點）──");
            bool has = Preview.TeleportMarkerPreview.HasAnchor(r);
            if (has)
            {
                float.TryParse(r.Params["markerX"].ToString(), out float mx);
                float.TryParse(r.Params["markerY"].ToString(), out float my);
                GUILayout.Label($"點模式 ({mx:F2}, {my:F2})");
            }
            else GUILayout.Label("<b>格子模式</b>（舊）：踩格子觸發", new GUIStyle(GUI.skin.label) { richText = true });

            // 踩踏矩形（玩家腳底進到這裡就傳送）。可在畫布拖右下角把手改，也可在這裡打數字。
            var size = Preview.TeleportMarkerPreview.TouchSize(r);
            GUILayout.BeginHorizontal();
            GUILayout.Label("踩踏 寬", GUILayout.Width(46));
            string wBuf = GUILayout.TextField(size.x.ToString("F2"), GUILayout.Width(46));
            GUILayout.Label("高", GUILayout.Width(18));
            string hBuf = GUILayout.TextField(size.y.ToString("F2"), GUILayout.Width(46));
            GUILayout.EndHorizontal();
            if (float.TryParse(wBuf, out float nw) && float.TryParse(hBuf, out float nh)
                && (!Mathf.Approximately(nw, size.x) || !Mathf.Approximately(nh, size.y)))
                Preview.TeleportMarkerPreview.SetSize(r, nw, nh);
            GUILayout.BeginHorizontal();
            GUI.color = MarkerPlaceActive ? Color.cyan : Color.white;
            if (GUILayout.Button(MarkerPlaceActive ? "點門正中央…(Esc取消)" : "設定外型位置")) MarkerPlaceActive = !MarkerPlaceActive;
            GUI.color = Color.white;
            if (GUILayout.Button("回到中心") && r.Params != null)
            {
                UndoManager.Push();
                Preview.TeleportMarkerPreview.ResetMarker(r);
                MarkerPlaceActive = false;
            }
            GUILayout.EndHorizontal();

            // 對位模式：把真的傳送點特效畫出來直接拖 —— 比「設定外型位置→點一下」快，也不用猜。
            var tpp = TpPrev();
            GUI.color = (tpp != null && tpp.Enabled) ? Color.cyan : Color.white;
            if (GUILayout.Button(tpp != null && tpp.Enabled ? "對位模式：開（可直接拖）" : "開啟對位模式（推薦）") && tpp != null)
            {
                tpp.Toggle();
                MarkerPlaceActive = false;
            }
            GUI.color = Color.white;
            GUILayout.Label("開「對位模式」→ 拖光盤搬位置、拖\n右下角綠把手改踩踏大小。\n綠框＝玩家<b>腳底</b>進去就傳送。",
                            new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
        }

        /// <summary>供 TriggerController 在開始畫時呼叫：沒有當前區域就依選取的類型自動建一個。</summary>
        public TriggerRegion EnsureCurrentRegion()
        {
            if (CurrentRegion != null) return CurrentRegion;
            var map = MapSession.Instance?.Map;
            var types = MapSession.Instance?.TriggerTypes;
            if (map == null || types == null || string.IsNullOrEmpty(_triggerType)) return null;
            CreateRegion(_triggerType, map.TriggerLayer.regions, types);
            return CurrentRegion;
        }

        void CreateRegion(string typeId, List<TriggerRegion> regions, TriggerTypeSet types)
        {
            if (string.IsNullOrEmpty(typeId)) return;
            var def = types.Find(typeId);
            int count = regions.FindAll(r => r.typeId == typeId).Count + 1;
            var region = new TriggerRegion
            {
                id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                typeId = typeId,
                name = (def != null ? def.displayName : typeId) + count,
            };
            if (def != null)
                foreach (var p in def.paramSchema)
                    region.Params[p.key] = (p.type == ParamType.Bool) ? (object)p.boolDefault : "";
            regions.Add(region);
            CurrentRegion = region;
            TriggerAddCells = true;
        }

        static void DrawParamField(TriggerRegion r, TriggerParam p)
        {
            if (p.isPortalList) { DrawPortalListField(r, p); return; }   // 可多筆清單：自己畫多欄＋＋/−，不走單行版型

            GUILayout.BeginHorizontal();
            GUILayout.Label(string.IsNullOrEmpty(p.label) ? p.key : p.label, GUILayout.Width(90));
            if (p.isScreenEffectRef)
            {
                // 螢幕特效 id：直接輸入 id，旁邊一顆「螢幕特效表」開參照清單（列出可填的 id，可點填入）。
                string cur = (r.Params.TryGetValue(p.key, out var sv) && sv != null) ? sv.ToString() : "";
                string next = GUILayout.TextField(cur, GUILayout.Width(46));
                if (next != cur) r.Params[p.key] = next;
                if (GUILayout.Button("螢幕特效表", GUILayout.Width(90)))
                {
                    _showScreenFx = true;
                    _screenFxRegion = r;
                    _screenFxKey = p.key;
                }
            }
            else if (p.isFlagRef)
            {
                DrawFlagField(r, p);
            }
            else if (p.options != null && p.options.Length > 0)
            {
                // 循環按鈕：在固定選項間切換，避免打錯字。
                //
                // ⚠ 值是空的時候一定要顯示「（未設定）」，**不可以拿第一個選項來充數**。
                //   舊版就是那樣寫的，造成兩個症狀：
                //     ① 畫面說謊——作者看到欄位已經是想要的值就不會去點它，實際存檔卻是空字串。
                //        祭壇的「面板」欄整組失效就是這樣來的：看起來是 gacha，存檔裡是 ""。
                //     ② 第一次點擊看起來沒反應——因為第一下寫入的正好是畫面上已經顯示的那個選項，
                //        要點第二下才看得到變化。
                //   之所以拖到現在才發現，是因為在此之前所有 options 欄位的「空值語意」剛好都等於
                //   第一個選項（repeat 空＝關卡單次、dim 空＝中央留洞…），行為上看不出差別。
                //   改成顯示「（未設定）」之後：第一次點＝寫入第一個選項且畫面同步變化，所見即所存。
                //   註：多數欄位「未設定」在遊戲端的行為仍等同第一個選項，看到它只代表「這格我還沒碰過」。
                string cur = (r.Params.TryGetValue(p.key, out var v) && v != null) ? v.ToString() : "";
                int idx = System.Array.IndexOf(p.options, cur);
                bool unset = idx < 0;
                if (GUILayout.Button(unset ? "（未設定）" : cur))
                    r.Params[p.key] = p.options[unset ? 0 : (idx + 1) % p.options.Length];
            }
            else if (p.type == ParamType.Bool)
            {
                bool cur = r.Params.TryGetValue(p.key, out var v) && v is bool b && b;
                bool next = GUILayout.Toggle(cur, cur ? "true" : "false");
                if (next != cur) r.Params[p.key] = next;
            }
            else
            {
                string cur = (r.Params.TryGetValue(p.key, out var v) && v != null) ? v.ToString() : "";
                string next = GUILayout.TextField(cur);
                if (next != cur) r.Params[p.key] = next;
            }
            GUILayout.EndHorizontal();
        }

        // 可多筆清單欄（如 togglePortal 的傳送點名稱）：一欄一個值，最後一欄旁有「＋」加欄、每欄有「−」刪欄；
        // 存進 r.Params 的是「逗號分隔的一個字串」。⚠️ **刻意不過濾空欄**——否則按「＋」加的空欄會當幀被濾掉、
        // 看起來沒反應（此為先前的 bug）；空欄對遊戲無害，TriggerChain.ExecuteTogglePortal 讀時會 Trim 後跳過空的。
        static void DrawPortalListField(TriggerRegion r, TriggerParam p)
        {
            // 目前值 → 拆清單（保留空欄；空字串 Split 也會得到一個空欄，至少一欄可輸入）。
            string cur = (r.Params.TryGetValue(p.key, out var v) && v != null) ? v.ToString() : "";
            var names = new List<string>(cur.Split(','));
            if (names.Count == 0) names.Add("");

            bool changed = false;
            int removeAt = -1;
            for (int i = 0; i < names.Count; i++)
            {
                GUILayout.BeginHorizontal();
                // 第一欄顯示欄位標籤，其餘欄留空對齊。
                GUILayout.Label(i == 0 ? (string.IsNullOrEmpty(p.label) ? p.key : p.label) : "", GUILayout.Width(90));
                string next = GUILayout.TextField(names[i] ?? "");
                if (next != names[i]) { names[i] = next; changed = true; }
                if (GUILayout.Button("−", GUILayout.Width(24))) removeAt = i;                 // 刪這一欄
                if (i == names.Count - 1 && GUILayout.Button("＋", GUILayout.Width(24)))       // 最後一欄旁：加一空欄
                    { names.Add(""); changed = true; }
                GUILayout.EndHorizontal();
            }
            if (removeAt >= 0) { names.RemoveAt(removeAt); changed = true; }
            if (names.Count == 0) { names.Add(""); changed = true; }   // 全刪光 → 至少留一欄

            if (changed)
                r.Params[p.key] = string.Join(",", names);   // 保留空欄（含尾逗號）；遊戲端讀時自動略過空的
        }

        // 旗標欄：輸入「旗標 id」→ 按「確認」→ 查登記表把名稱填上並鎖定 → 出現「刪除」清空回可輸入。
        // 存進地圖的只有「裸名字」（＋條件旗標的否定 !）；生命週期由登記表決定，遊戲端查表（方案乙、單一來源）。
        static void DrawFlagField(TriggerRegion r, TriggerParam p)
            => DrawFlagFieldCore(
                (r.Params.TryGetValue(p.key, out var v) && v != null) ? v.ToString().Trim() : "",
                BufKey(r, p), p.flagNegatable,
                val => r.Params[p.key] = val);

        // 旗標欄核心（trigger 參數與地上物「破壞旗標」共用）：輸入 id→確認→鎖成名字；已配置時顯示名稱＋刪除。
        //   cur=目前值（可含否定 !）、bufKey=輸入暫存的鍵、negatable=可否加「有/沒有」、setValue=寫回值。
        static void DrawFlagFieldCore(string cur, string bufKey, bool negatable, System.Action<string> setValue)
        {
            cur = cur?.Trim() ?? "";
            bool neg = cur.StartsWith("!");
            string bare = neg ? cur.Substring(1).Trim() : cur;

            if (!string.IsNullOrEmpty(bare))
            {
                // 已配置 → 鎖定顯示名稱（＋生命週期）；可否定的多一顆「有/沒有」切換；一顆「刪除」清空。
                bool known = _flagReg != null && _flagReg.Contains(bare);
                if (negatable && known)
                    if (GUILayout.Button(neg ? "沒有" : "有", GUILayout.Width(44)))
                        setValue((neg ? "" : "!") + bare);
                GUILayout.Label(known ? DisplayFlag(bare) : bare + "（未登記）");
                if (GUILayout.Button("刪除", GUILayout.Width(50)))
                {
                    setValue("");
                    _flagIdBuf.Remove(bufKey);
                }
                return;
            }

            // 未配置 → 輸入 id + 確認。沒按確認、或查無 id，就不會有名稱出現＝未配置成功。
            string buf = _flagIdBuf.TryGetValue(bufKey, out var b) ? b : "";
            GUILayout.Label("id", GUILayout.Width(16));
            _flagIdBuf[bufKey] = GUILayout.TextField(buf, GUILayout.Width(46));
            if (GUILayout.Button("確認", GUILayout.Width(50)))
            {
                if (int.TryParse(_flagIdBuf[bufKey], out int id) && _flagReg != null && _flagReg.FindById(id) is FlagDef f && f != null)
                {
                    setValue(f.name);   // 查到 → 填名稱（鎖定）。條件旗標預設「有」；要「沒有」再按切換。
                    _flagIdBuf.Remove(bufKey);
                }
                // 查無 → 保留輸入、不填名稱（作者看不到名稱＝知道沒配成功）。
            }
        }

        static string BufKey(TriggerRegion r, TriggerParam p) => (r?.id ?? "") + "/" + p.key;

        // 顯示旗標名＋生命週期（讓作者一眼看到這旗標是周目還是永久）。
        static string DisplayFlag(string name)
        {
            if (string.IsNullOrEmpty(name)) return "（未設）";
            var f = _flagReg?.Find(name);
            return f != null ? $"{name}（{f.ScopeLabel}）" : name;
        }

        // 旗標管理器：集中管理所有具名旗標的名稱與生命週期（周目/永久）。觸發點的旗標欄一律從這裡選。
        void DrawFlagManager()
        {
            const int w = 480, h = 420;
            GUILayout.BeginArea(CenteredRect(w, h), GUI.skin.box);
            GUILayout.Label("旗標管理器（名稱＋生命週期）");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("自動匯入地圖旗標", GUILayout.Width(140)))
            {
                var added = FlagRegistryStore.ImportUsedFlags(_flagReg);
                _flagMsg = added.Count > 0 ? $"匯入 {added.Count} 個：{string.Join("、", added)}" : "沒有新的旗標可匯入。";
            }
            if (GUILayout.Button("儲存", GUILayout.Width(60)))
            {
                FlagRegistryStore.Save(_flagReg);
                _flagMsg = "已儲存 flags.json（記得跑「刷新素材」或同步腳本帶進遊戲）。";
            }
            if (GUILayout.Button("關閉", GUILayout.Width(60))) _showFlags = false;
            GUILayout.EndHorizontal();

            // 新增一列
            GUILayout.BeginHorizontal();
            GUILayout.Label("新增", GUILayout.Width(36));
            _newFlagName = GUILayout.TextField(_newFlagName ?? "");
            if (GUILayout.Button("＋", GUILayout.Width(30)))
            {
                if (_flagReg.Add(_newFlagName)) { _flagMsg = $"已新增「{_newFlagName.Trim()}」（預設周目）"; _newFlagName = ""; }
                else _flagMsg = "名稱空白或已存在。";
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("id　名稱　　　　　　　生命週期　操作（觸發點填 id 選旗標）");
            _flagScroll = GUILayout.BeginScrollView(_flagScroll, GUILayout.Height(250));
            int removeAt = -1;
            for (int i = 0; i < _flagReg.flags.Count; i++)
            {
                var f = _flagReg.flags[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(f.id.ToString(), GUILayout.Width(28));
                f.name = GUILayout.TextField(f.name ?? "", GUILayout.Width(200));
                if (GUILayout.Button(f.ScopeLabel, GUILayout.Width(72)))    // 周目 → 永久 → 關卡單次
                    f.CycleScope();
                if (GUILayout.Button("刪除", GUILayout.Width(50))) removeAt = i;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (removeAt >= 0) _flagReg.flags.RemoveAt(removeAt);

            GUILayout.Label("改名不會自動更新已放好的觸發點；\n刪除/改名後記得到相關觸發點重選。");
            if (!string.IsNullOrEmpty(_flagMsg)) GUILayout.Label(_flagMsg);
            GUILayout.EndArea();
        }

        // 螢幕特效表：列出「播放螢幕特效」trigger 的 effectId 欄可填的一次性全螢幕過場特效，可點「填入」寫回該欄。
        // 清單來源＝ScreenFxCatalog（上面的維護點）。純參照，不改遊戲行為。
        void DrawScreenFxTable()
        {
            const int w = 560, h = 360;
            GUILayout.BeginArea(CenteredRect(w, h), GUI.skin.box);
            GUILayout.Label("螢幕特效表（一次性全螢幕過場特效；填進「播放螢幕特效」trigger 的『螢幕特效id』欄）");
            GUILayout.Label("id　名稱　　說明（點「填入」把 id 寫回欄位）");

            _screenFxScroll = GUILayout.BeginScrollView(_screenFxScroll, GUILayout.Height(270));
            foreach (var e in ScreenFxCatalog)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(e.id.ToString(), GUILayout.Width(28));
                GUILayout.Label(e.name, GUILayout.Width(90));
                GUILayout.Label(e.desc);
                if (_screenFxRegion != null && !string.IsNullOrEmpty(_screenFxKey)
                    && GUILayout.Button("填入", GUILayout.Width(50)))
                {
                    _screenFxRegion.Params[_screenFxKey] = e.id.ToString();
                    _showScreenFx = false;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button("關閉", GUILayout.Width(60))) _showScreenFx = false;
            GUILayout.EndArea();
        }

        void DrawSceneFxPanel()
        {
            var map = MapSession.Instance?.Map;
            var ctl = SfxCtl();
            if (map == null || ctl == null) return;

            var rect = PanelRect;
            GUILayout.BeginArea(rect, GUI.skin.box);
            _sfxScroll = GUILayout.BeginScrollView(_sfxScroll);

            GUILayout.Label("場景特效（粒子）");
            GUILayout.Label("外觀由 fxId 對應 SceneFxTable.csv\n（1煙 2火 3冰 4毒…）。");
            if (GUILayout.Button("＋ 新增特效")) ctl.NewFx();

            GUILayout.Space(6);
            GUILayout.Label($"特效清單（{map.sceneFx.Count}）");
            SceneFxInstance toDelete = null;
            foreach (var fx in map.sceneFx)
            {
                GUILayout.BeginHorizontal();
                GUI.color = (fx == ctl.Selected) ? Color.cyan : Color.white;
                if (GUILayout.Button($"fx{fx.fxId} {(fx.hasEnd ? "起→終" : "起點")}", GUILayout.Width(112))) ctl.Select(fx);
                // 即時預覽切換：顯示＝跟遊戲內一樣跑這個特效；再按一次隱藏。
                bool prev = ctl.IsPreviewing(fx);
                GUI.color = prev ? Color.green : Color.white;
                if (GUILayout.Button(prev ? "隱藏" : "顯示", GUILayout.Width(50))) ctl.TogglePreview(fx);
                GUI.color = Color.white;
                if (GUILayout.Button("刪", GUILayout.Width(32))) toDelete = fx;
                GUILayout.EndHorizontal();
            }
            if (toDelete != null) { ctl.Select(toDelete); ctl.DeleteSelected(); }

            var sel = ctl.Selected;
            if (sel != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("── 編輯特效 ──");
                // id：給 trigger 的 linkedFx 欄位參照用（傳送點解鎖時顯示綠幕，見主專案 readme/TRIGGER_CHAIN.md）。
                GUILayout.BeginHorizontal();
                GUILayout.Label("id(linkedFx用)", GUILayout.Width(90));
                GUILayout.TextField(sel.id ?? "");   // 可選取複製；不寫回（id 由系統生成）
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUI.color = ctl.Mode == Tools.SceneFxController.PlaceMode.Start ? Color.green : Color.white;
                if (GUILayout.Button("放置起點(綠)")) ctl.BeginPlaceStart();
                GUI.color = ctl.Mode == Tools.SceneFxController.PlaceMode.End ? new Color(1f, 0.5f, 0.5f) : Color.white;
                if (GUILayout.Button("放置終點(紅)")) ctl.BeginPlaceEnd();
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
                GUILayout.Label(ctl.Mode == Tools.SceneFxController.PlaceMode.None
                    ? "（點按鈕後到畫布上點一下放置）" : "→ 現在到畫布上點一下放置");

                // 切換到不同特效時，把數字緩衝重新灌成該特效的現值。
                if (_sfxBufFor != sel)
                {
                    _sfxBufFor = sel;
                    _bufFxId = sel.fxId.ToString();
                    _bufBulge = sel.bulge.ToString("0.###");
                    _bufW = sel.w.ToString("0.###");
                    _bufH = sel.h.ToString("0.###");
                    _bufInterval = sel.interval.ToString("0.###");
                }

                sel.hasEnd = GUILayout.Toggle(sel.hasEnd, "有終點（沿弧線流動）");
                _bufFxId = SfxField("特效編號 fxId", _bufFxId); sel.fxId = ParseIntOr(_bufFxId, 1);
                _bufBulge = SfxField("弧線外鼓 bulge", _bufBulge); sel.bulge = ParseFloatOr(_bufBulge, 0f);
                _bufW = SfxField("大小X w", _bufW); sel.w = ParseFloatOr(_bufW, 1f);
                _bufH = SfxField("大小Y h", _bufH); sel.h = ParseFloatOr(_bufH, 1f);
                sel.loop = GUILayout.Toggle(sel.loop, "循環播放 loop");
                sel.intermittent = GUILayout.Toggle(sel.intermittent, "間歇播放 intermittent");
                _bufInterval = SfxField("間歇間隔秒 interval", _bufInterval); sel.interval = ParseFloatOr(_bufInterval, 2f);

                GUILayout.Space(4);
                GUILayout.Label($"起點 ({sel.startX:0.0}, {sel.startY:0.0})"
                    + (sel.hasEnd ? $"\n終點 ({sel.endX:0.0}, {sel.endY:0.0})" : ""));
            }
            else
            {
                GUILayout.Space(8);
                GUILayout.Label("按「＋ 新增特效」建立，\n或點畫布上的標記選取。");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // 只畫「標籤 + 文字框」、回傳目前文字（不 parse）——讓緩衝可自由編輯/清空，不會被舊值蓋回去。
        static string SfxField(string label, string buf)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(130));
            string s = GUILayout.TextField(buf ?? "", GUILayout.Width(60));
            GUILayout.EndHorizontal();
            return s;
        }

        // 空字串/無效 → 回傳預設值（存進資料時就是預設，符合「沒填就給預設」）。
        static int ParseIntOr(string s, int def) => int.TryParse(s, out int v) ? v : def;
        static float ParseFloatOr(string s, float def) => float.TryParse(s, out float v) ? v : def;

        // ================= NPC 面板 =================

        void DrawNpcPanel()
        {
            var map = MapSession.Instance?.Map;
            var ctl = NpcCtl();
            if (map == null || ctl == null) return;
            if (map.npcs == null) map.npcs = new List<Data.NpcInstance>();

            var rect = PanelRect;
            GUILayout.BeginArea(rect, GUI.skin.box);
            _npcScroll = GUILayout.BeginScrollView(_npcScroll);

            GUILayout.Label("NPC");
            GUILayout.Label("放角色→設行為與對話。\n畫布上直接拖曳 NPC／路徑點。");

            var rows = Preview.NpcTableEditor.Rows;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重讀 NPC 表", GUILayout.Width(88))) Preview.NpcTableEditor.Reload();
            GUILayout.Label($"共 {rows.Count} 種");
            GUILayout.EndHorizontal();
            if (rows.Count == 0)
            {
                GUILayout.Label("⚠ 主專案 Assets/Data/NpcTable.csv\n沒有資料（或不存在）。\n先在表裡加 NPC 種類再來擺。");
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(4);
            if (GUILayout.Button("＋ 新增 NPC")) ctl.NewNpc(rows[0].ID);

            GUILayout.Space(6);
            GUILayout.Label($"NPC 清單（{map.npcs.Count}）");
            Data.NpcInstance toDelete = null;
            for (int i = 0; i < map.npcs.Count; i++)
            {
                var n = map.npcs[i];
                if (n == null) continue;
                GUILayout.BeginHorizontal();
                GUI.color = (n == ctl.Selected) ? Color.cyan : Color.white;
                var r0 = Preview.NpcTableEditor.Get(n.npcId);
                string label = !string.IsNullOrWhiteSpace(n.name) ? n.name : (r0 != null ? r0.ShownName : $"NPC {i + 1}");
                if (GUILayout.Button(label, GUILayout.Width(150))) ctl.Select(n);
                if (GUILayout.Button("刪", GUILayout.Width(32))) toDelete = n;
                GUILayout.EndHorizontal();
            }
            GUI.color = Color.white;
            if (toDelete != null) { ctl.Select(toDelete); ctl.DeleteSelected(); }

            var sel = ctl.Selected;
            if (sel == null)
            {
                GUILayout.Space(8);
                GUILayout.Label("按「＋ 新增 NPC」建立，\n或點畫布上的 NPC 選取。");
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(8);
            GUILayout.Label("── 編輯 NPC ──");

            if (_npcBufFor != sel)   // 切換選取 → 重灌輸入暫存
            {
                _npcBufFor = sel;
                _npNameBuf = sel.name ?? "";
                _npDramaBuf = sel.dramaId > 0 ? sel.dramaId.ToString() : "";
                _npPanelBuf = sel.panelId ?? "";
                _npArgBuf = sel.panelArg ?? "";
                _npNextBuf = sel.next ?? "";
                _npcPickRole = false; _npcPickNext = false;
            }

            GUILayout.BeginHorizontal();
            GUI.color = ctl.Placing ? Color.green : Color.white;
            if (GUILayout.Button("放置站位")) ctl.BeginPlace();
            GUI.color = Color.white;
            if (GUILayout.Button("複製一個")) ctl.DuplicateSelected();
            GUILayout.EndHorizontal();
            GUILayout.Label(ctl.Placing ? "→ 到畫布點一下放置" : $"站位 ({sel.x:0.0}, {sel.y:0.0})");

            // 名稱（清單顯示用）
            GUILayout.BeginHorizontal();
            GUILayout.Label("名稱", GUILayout.Width(44));
            bool editingName = GUI.GetNameOfFocusedControl() == "npName";
            if (!editingName) _npNameBuf = sel.name ?? "";
            GUI.SetNextControlName("npName");
            string sn = GUILayout.TextField(_npNameBuf ?? "", GUILayout.Width(140));
            GUILayout.EndHorizontal();
            if (editingName && sn != _npNameBuf) { _npNameBuf = sn; sel.name = sn; }

            // 角色（NpcTable 下拉）
            var curRow = Preview.NpcTableEditor.Get(sel.npcId);
            GUILayout.BeginHorizontal();
            GUILayout.Label("角色", GUILayout.Width(44));
            if (GUILayout.Button(curRow != null ? $"{curRow.ID}｜{curRow.ShownName}" : $"{sel.npcId}（表裡沒有）"))
                _npcPickRole = !_npcPickRole;
            GUILayout.EndHorizontal();
            if (_npcPickRole)
            {
                foreach (var r in rows)
                    if (GUILayout.Button($"　{r.ID}｜{r.ShownName}"))
                    {
                        UndoManager.Push();
                        sel.npcId = r.ID;
                        _npcPickRole = false;
                    }
            }

            // 行為
            GUILayout.BeginHorizontal();
            GUILayout.Label("行為", GUILayout.Width(44));
            GUI.color = sel.behavior != Data.NpcInstance.BehaviorPatrol ? Color.cyan : Color.white;
            if (GUILayout.Button("原地", GUILayout.Width(60))) { UndoManager.Push(); sel.behavior = Data.NpcInstance.BehaviorIdle; }
            GUI.color = sel.behavior == Data.NpcInstance.BehaviorPatrol ? Color.cyan : Color.white;
            if (GUILayout.Button("來回走動", GUILayout.Width(80))) { UndoManager.Push(); sel.behavior = Data.NpcInstance.BehaviorPatrol; }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            if (sel.behavior == Data.NpcInstance.BehaviorPatrol)
            {
                GUILayout.Space(2);
                GUI.color = ctl.AddingWaypoints ? Color.green : Color.white;
                if (GUILayout.Button(ctl.AddingWaypoints ? "加路徑點中…（ESC 結束）" : "＋ 加路徑點（連續點畫布）"))
                    ctl.ToggleAddWaypoints();
                GUI.color = Color.white;

                if (sel.waypoints != null && sel.waypoints.Count > 0)
                {
                    int del = -1;
                    for (int i = 0; i < sel.waypoints.Count; i++)
                    {
                        var w = sel.waypoints[i];
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"點{i + 1} ({w.x:0.0}, {w.y:0.0})", GUILayout.Width(140));
                        if (GUILayout.Button("刪", GUILayout.Width(32))) del = i;
                        GUILayout.EndHorizontal();
                    }
                    if (del >= 0) ctl.RemoveWaypoint(del);
                    GUILayout.Label("走法：站位→各點 乒乓來回；\n點可在畫布上直接拖。");
                }
                else GUILayout.Label("（還沒有路徑點：至少加一點\n才會走動）");

                LightNumField("速度", "npSpeed", ref _npSpeedBuf,
                              () => sel.speed, v => sel.speed = v, 0.2f, 0f, "0=用表");
                LightNumField("停留秒", "npDwell", ref _npDwellBuf,
                              () => sel.dwellSeconds, v => sel.dwellSeconds = v, 0.2f, 0f, "每點停留");
            }

            GUILayout.Space(6);
            GUILayout.Label("── 對話與介面 ──");

            // 對話 id（DramaTable；Type 1 大圖或 Type 2 頭像對話都可）
            bool editingDrama = GUI.GetNameOfFocusedControl() == "npDrama";
            if (!editingDrama) _npDramaBuf = sel.dramaId > 0 ? sel.dramaId.ToString() : "";
            GUILayout.BeginHorizontal();
            GUILayout.Label("對話id", GUILayout.Width(44));
            GUI.SetNextControlName("npDrama");
            string sd = GUILayout.TextField(_npDramaBuf ?? "", GUILayout.Width(56));
            GUILayout.Label("DramaTable", GUILayout.Width(80));
            GUILayout.EndHorizontal();
            if (editingDrama && sd != _npDramaBuf)
            {
                _npDramaBuf = sd;
                sel.dramaId = int.TryParse(sd, out var dv) && dv > 0 ? dv : 0;
            }
            GUILayout.Label("靠近按 F 交談（可反覆聊）。\n空＝不對話。");

            // 介面（對話結束後開；沒填對話＝按 F 直接開）
            bool editingPanel = GUI.GetNameOfFocusedControl() == "npPanel";
            if (!editingPanel) _npPanelBuf = sel.panelId ?? "";
            GUILayout.BeginHorizontal();
            GUILayout.Label("介面", GUILayout.Width(44));
            GUI.SetNextControlName("npPanel");
            string sp = GUILayout.TextField(_npPanelBuf ?? "", GUILayout.Width(80));
            GUILayout.EndHorizontal();
            if (editingPanel && sp != _npPanelBuf) { _npPanelBuf = sp; sel.panelId = sp.Trim(); }

            bool editingArg = GUI.GetNameOfFocusedControl() == "npArg";
            if (!editingArg) _npArgBuf = sel.panelArg ?? "";
            GUILayout.BeginHorizontal();
            GUILayout.Label("參數", GUILayout.Width(44));
            GUI.SetNextControlName("npArg");
            string sa = GUILayout.TextField(_npArgBuf ?? "", GUILayout.Width(80));
            GUILayout.EndHorizontal();
            if (editingArg && sa != _npArgBuf) { _npArgBuf = sa; sel.panelArg = sa.Trim(); }
            GUILayout.Label("對話結束後開此介面；沒填對話\n＝按 F 直接開。目前可填：gacha\n（參數=抽選池代號）；買賣/兌換\n介面做好後在此填新代號。空＝不開。");

            GUILayout.Space(6);
            GUILayout.Label("── 對話結束後接鏈 ──");

            // 接續觸發（next）：文字輸入＋從本圖 trigger 名單挑
            bool editingNext = GUI.GetNameOfFocusedControl() == "npNext";
            if (!editingNext) _npNextBuf = sel.next ?? "";
            GUILayout.BeginHorizontal();
            GUILayout.Label("接續", GUILayout.Width(44));
            GUI.SetNextControlName("npNext");
            string sx = GUILayout.TextField(_npNextBuf ?? "", GUILayout.Width(110));
            if (GUILayout.Button("選", GUILayout.Width(32))) _npcPickNext = !_npcPickNext;
            GUILayout.EndHorizontal();
            if (editingNext && sx != _npNextBuf) { _npNextBuf = sx; sel.next = sx.Trim(); }
            if (_npcPickNext && map.TriggerLayer?.regions != null)
            {
                foreach (var tr in map.TriggerLayer.regions)
                {
                    if (tr == null || string.IsNullOrWhiteSpace(tr.name)) continue;
                    if (GUILayout.Button("　" + tr.name))
                    {
                        UndoManager.Push();
                        sel.next = tr.name.Trim();
                        _npcPickNext = false;
                    }
                }
                if (GUILayout.Button("　（清空）")) { UndoManager.Push(); sel.next = ""; _npcPickNext = false; }
            }

            // 完成寫旗標（與觸發點同一套旗標登記表）
            GUILayout.BeginHorizontal();
            GUILayout.Label("寫旗標", GUILayout.Width(44));
            DrawFlagFieldCore(sel.setFlag ?? "", "npc/" + sel.id + "/setFlag", false,
                              v => { UndoManager.Push(); sel.setFlag = v; });
            GUILayout.EndHorizontal();
            GUILayout.Label("對話結束＝完成：寫旗標＋啟動\n接續觸發（**每次進圖只跑第一\n次**；對話本身可反覆聊）。");

            GUILayout.Space(4);
            if (GUILayout.Button("刪除這個 NPC")) ctl.DeleteSelected();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ================= 照明面板（獨立光源，不綁地上物） =================

        void DrawLightPanel()
        {
            var map = MapSession.Instance?.Map;
            var ctl = LightCtl();
            if (map == null || ctl == null) return;
            if (map.lights == null) map.lights = new List<Data.LightInstance>();

            var rect = PanelRect;
            GUILayout.BeginArea(rect, GUI.skin.box);
            _lightScroll = GUILayout.BeginScrollView(_lightScroll);

            GUILayout.Label("照明（獨立光源）");
            GUILayout.Label("給已經畫在背景圖裡的火炬/燈籠：\n把光源點放到火焰中心就會發光，\n不必把圖拆成地上物。");
            GUILayout.Label("放好後直接用滑鼠拖曳就能移動。");

            // ── 預覽（把場景壓暗、讓燈照回來）──
            var lp = LightPrev();
            if (lp != null)
            {
                GUILayout.Space(4);
                GUILayout.Label("── 預覽 ──");
                GUI.color = lp.Enabled ? Color.cyan : Color.white;
                if (GUILayout.Button(lp.Enabled ? "照明預覽：開" : "照明預覽：關")) lp.Toggle();
                GUI.color = Color.white;

                GUILayout.BeginHorizontal();
                GUILayout.Label("環境亮度", GUILayout.Width(64));
                int nb = Mathf.RoundToInt(GUILayout.HorizontalSlider(lp.EnvBright, 0f, 100f, GUILayout.Width(110)));
                GUILayout.Label(lp.EnvBright.ToString(), GUILayout.Width(30));
                GUILayout.EndHorizontal();
                if (nb != lp.EnvBright) lp.SetEnvBright(nb);
                GUILayout.Label("調到滿意後，把這個數字填進主專案\nAssets/Data/MapsTable.csv 這張圖那列的\n『EnvBright』欄，遊戲裡才會一樣暗。\n（100=不壓暗；45≈昏暗室內）");

                // 遊戲同框上限是 12 盞，預覽最多畫 32——畫面內超過 12 時遊戲會丟掉最遠的，先在這裡示警。
                if (lp.Enabled && lp.OnScreenCount > LightPreview.GameMaxLights)
                {
                    GUI.color = new Color(1f, 0.75f, 0.3f);
                    GUILayout.Label($"⚠ 畫面內有 {lp.OnScreenCount} 盞，遊戲同框\n上限 {LightPreview.GameMaxLights} 盞，離玩家最遠的會被\n丟掉。預覽比遊戲亮屬正常。");
                    GUI.color = Color.white;
                }
                GUILayout.Label("※ 預覽只模擬「壓暗＋照亮＋光色」。\n遊戲的幽暗/噩夢氛圍還會去飽和、\n加冷色調，實際更陰沉。");
            }

            GUILayout.Space(6);
            if (GUILayout.Button("＋ 新增光源")) ctl.NewLight();

            GUILayout.Space(6);
            GUILayout.Label($"光源清單（{map.lights.Count}）");
            Data.LightInstance toDelete = null;
            for (int i = 0; i < map.lights.Count; i++)
            {
                var l = map.lights[i];
                if (l == null) continue;
                GUILayout.BeginHorizontal();
                GUI.color = (l == ctl.Selected) ? Color.cyan : Color.white;
                string label = string.IsNullOrWhiteSpace(l.name) ? ("燈 " + (i + 1)) : l.name;
                if (GUILayout.Button(label, GUILayout.Width(120))) ctl.Select(l);
                // 小色塊：一眼看出這盞是暖橘還是鬼火綠
                var sw = GUILayoutUtility.GetRect(18f, 16f, GUILayout.Width(18));
                var pc = GUI.color;
                GUI.color = ParseHexColor(l.color);
                GUI.DrawTexture(sw, Texture2D.whiteTexture);
                GUI.color = pc;
                if (GUILayout.Button("刪", GUILayout.Width(32))) toDelete = l;
                GUILayout.EndHorizontal();
            }
            GUI.color = Color.white;
            if (toDelete != null) { ctl.Select(toDelete); ctl.DeleteSelected(); }

            var sel = ctl.Selected;
            if (sel != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("── 編輯光源 ──");

                GUILayout.BeginHorizontal();
                GUI.color = ctl.Placing ? Color.green : Color.white;
                if (GUILayout.Button("放置位置")) ctl.BeginPlace();
                GUI.color = Color.white;
                if (GUILayout.Button("複製一盞")) ctl.DuplicateSelected();
                GUILayout.EndHorizontal();
                GUILayout.Label(ctl.Placing
                    ? "→ 現在到畫布上點一下放置"
                    : "（直接拖曳畫布上的燈就能移動；\n「放置位置」是要精準重放時用）");
                GUILayout.Label($"位置 ({sel.x:0.0}, {sel.y:0.0})");

                // 切換到不同光源時，把輸入暫存重新灌成該盞的現值
                if (_lightBufFor != sel)
                {
                    _lightBufFor = sel;
                    _liRadBuf = sel.radius.ToString("0.##");
                    _liIntBuf = sel.intensity.ToString("0.##");
                    _liFlkBuf = sel.flicker.ToString("0.##");
                    _liSpdBuf = sel.flickerSpeed.ToString("0.##");
                    _liSofBuf = sel.softness.ToString("0.##");
                    _liColBuf = sel.color ?? "";
                    _liNameBuf = sel.name ?? "";
                }

                // 名字：只給編輯器清單看，方便在一堆燈裡認出「大廳左火炬」
                GUILayout.BeginHorizontal();
                GUILayout.Label("名稱", GUILayout.Width(64));
                bool editingName = GUI.GetNameOfFocusedControl() == "liName";
                if (!editingName) _liNameBuf = sel.name ?? "";
                GUI.SetNextControlName("liName");
                string sn = GUILayout.TextField(_liNameBuf ?? "", GUILayout.Width(140));
                GUILayout.EndHorizontal();
                if (editingName && sn != _liNameBuf) { _liNameBuf = sn; sel.name = sn; }

                GUILayout.Space(4);
                GUILayout.Label("燈種預設（不動發光半徑）");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("火把", GUILayout.Width(46))) { UndoManager.Push(); ApplyLightPreset(sel, "FFC785", 1.0f, 1.0f, 1.0f, 0.46f); _lightBufFor = null; }
                if (GUILayout.Button("燭火", GUILayout.Width(46))) { UndoManager.Push(); ApplyLightPreset(sel, "FFD9A0", 0.7f, 1.4f, 1.6f, 0.30f); _lightBufFor = null; }
                if (GUILayout.Button("燈籠", GUILayout.Width(46))) { UndoManager.Push(); ApplyLightPreset(sel, "FFB05A", 1.0f, 0.5f, 0.6f, 0.55f); _lightBufFor = null; }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("鬼火", GUILayout.Width(46))) { UndoManager.Push(); ApplyLightPreset(sel, "7CFFB0", 0.8f, 1.8f, 0.35f, 0.25f); _lightBufFor = null; }
                if (GUILayout.Button("月光", GUILayout.Width(46))) { UndoManager.Push(); ApplyLightPreset(sel, "CFE4FF", 0.9f, 0.0f, 1.0f, 0.40f); _lightBufFor = null; }
                if (GUILayout.Button("爐火", GUILayout.Width(46))) { UndoManager.Push(); ApplyLightPreset(sel, "FF8A3D", 1.5f, 1.2f, 0.9f, 0.60f); _lightBufFor = null; }
                GUILayout.EndHorizontal();

                GUILayout.Space(4);
                LightNumField("發光半徑", "liRad", ref _liRadBuf,
                              () => sel.radius, x => sel.radius = x, 0.5f, 0.1f, "格");
                LightNumField("亮度", "liInt", ref _liIntBuf,
                              () => sel.intensity, x => sel.intensity = x, 0.1f, 0f, "倍（1=標準）");
                LightNumField("搖晃強度", "liFlk", ref _liFlkBuf,
                              () => sel.flicker, x => sel.flicker = x, 0.1f, 0f, "（0=不晃）");
                LightNumField("搖晃速度", "liSpd", ref _liSpdBuf,
                              () => sel.flickerSpeed, x => sel.flickerSpeed = x, 0.1f, 0.01f, "倍（1=標準）");
                LightNumField("邊緣柔和", "liSof", ref _liSofBuf,
                              () => sel.softness, x => sel.softness = Mathf.Clamp01(x), 0.05f, 0f, "（小=柔 大=硬）");

                // 光色：6 碼 16 進位 RRGGBB（不含 #）。空＝預設暖橘。
                bool editingCol = GUI.GetNameOfFocusedControl() == "liCol";
                if (!editingCol) _liColBuf = sel.color ?? "";
                GUILayout.BeginHorizontal();
                GUILayout.Label("光色 RGB", GUILayout.Width(64));
                GUI.SetNextControlName("liCol");
                string sc = GUILayout.TextField(_liColBuf ?? "", GUILayout.Width(70));
                var swatch = GUILayoutUtility.GetRect(22f, 16f, GUILayout.Width(22));
                var prevCol = GUI.color;
                GUI.color = ParseHexColor(sel.color);
                GUI.DrawTexture(swatch, Texture2D.whiteTexture);
                GUI.color = prevCol;
                GUILayout.Label("空=暖橘", GUILayout.Width(60));
                GUILayout.EndHorizontal();
                if (editingCol && sc != _liColBuf) { _liColBuf = sc; sel.color = sc.Trim().TrimStart('#'); }
            }
            else
            {
                GUILayout.Space(8);
                GUILayout.Label("按「＋ 新增光源」建立，\n或直接點畫布上的燈來選取。");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawNewDialog()
        {
            const int w = 420, h = 380;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("新建地圖");
            GUILayout.Space(6);
            Field("名稱", ref _name);

            // Module 選擇（= 此地圖可用的資源範圍 Main + 此 module）
            var modules = MapSession.Instance?.Catalog?.EditableModules() ?? new List<string>();
            if (modules.Count > 0 && (string.IsNullOrEmpty(_module) || !modules.Contains(_module)))
                _module = modules[0];

            GUILayout.Label("Module（資源目錄）");
            if (modules.Count == 0)
            {
                GUILayout.Label("　找不到任何 module。請先同步素材。");
            }
            else
            {
                GUILayout.BeginHorizontal();
                foreach (var m in modules)
                {
                    GUI.color = (m == _module) ? Color.cyan : Color.white;
                    if (GUILayout.Button(m)) _module = m;
                }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
            }

            // 背景圖選擇（選用）
            var bgs = BackgroundService.BuildBackgrounds(MapSession.Instance?.Catalog, _module);
            if (!string.IsNullOrEmpty(_bgId) && bgs.FindIndex(b => b.id == _bgId) < 0) _bgId = "";
            GUILayout.Label("背景圖（選用，留「無」＝純黑底用 tile）");
            GUILayout.BeginHorizontal();
            GUI.color = string.IsNullOrEmpty(_bgId) ? Color.cyan : Color.white;
            if (GUILayout.Button("無")) _bgId = "";
            foreach (var b in bgs)
            {
                GUI.color = (_bgId == b.id) ? Color.cyan : Color.white;
                if (GUILayout.Button(Short(b.id))) _bgId = b.id;
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_bgId) && GUILayout.Button("套用背景長寬比到畫布（依高算寬）"))
                ApplyBgAspect(_bgId);

            Field("Tile 尺寸（世界單位）", ref _tileSize);
            Field("寬（格）", ref _width);
            Field("高（格）", ref _height);

            if (TryParse(out float ts, out int cw, out int ch))
                GUILayout.Label($"≈ {Mathf.RoundToInt(cw * ts * TileNativePx)} × {Mathf.RoundToInt(ch * ts * TileNativePx)} px　（一個螢幕約 18×10 格）");
            else
                GUILayout.Label("<請輸入有效數值>");

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            bool canCreate = modules.Count > 0 && !string.IsNullOrEmpty(_module);
            GUI.enabled = canCreate;
            if (GUILayout.Button("建立") && canCreate && TryParse(out float t, out int width, out int height))
            {
                MapSession.Instance.NewMap(_name, _module, t, width, height, _bgId);
                _showNew = false;
            }
            GUI.enabled = true;
            if (GUILayout.Button("取消") && MapSession.Instance?.Map != null)
                _showNew = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static void Field(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(170));
            value = GUILayout.TextField(value);
            GUILayout.EndHorizontal();
        }

        bool TryParse(out float tileSize, out int width, out int height)
        {
            tileSize = 1; width = 0; height = 0;
            return float.TryParse(_tileSize, out tileSize) && tileSize > 0
                && int.TryParse(_width, out width) && width > 0
                && int.TryParse(_height, out height) && height > 0;
        }
        /// <summary>
        /// 「層」的顯示文字。**寫出語意而不只是數字**——「層 +1」不是「稍微前面一點」，
        /// 而是「整層跳到玩家之上、完全退出 Y 排序」，只看數字不會知道這件事。
        /// 見 readme/PROBLEMS.md E16。
        /// </summary>
        static string ZOrderText(int z)
        {
            if (z > 0) return "層 +" + z + "　⚠ 永遠蓋住角色（只適合桌上的小東西）";
            if (z < 0) return "層 " + z + "　永遠被其他地上物蓋住";
            return "層 0　正常 Y 排序（走到前面就蓋住它）";
        }

        static string Short(string id)
        {
            int slash = id.LastIndexOf('/');
            return slash >= 0 ? id.Substring(slash + 1) : id;
        }

        // ================= 劇情演出（Cutscene）面板 =================
        static readonly string[] StepTypes =
            { "move", "face", "dialogue", "bubble", "wait", "camera", "cameraFollow", "comic", "fade", "spawn", "despawn", "screenFx", "setFlag", "end" };

        void DrawCutscenePanel()
        {
            var map = MapSession.Instance?.Map;
            var ctl = CsCtl();
            if (map == null || ctl == null) return;

            var rect = PanelRect;
            GUILayout.BeginArea(rect, GUI.skin.box);
            _csScroll = GUILayout.BeginScrollView(_csScroll);

            GUILayout.Label("劇情演出（半演出半漫畫）");
            var cs = map.MainCutscene;
            if (cs == null)
            {
                GUILayout.Label("此圖尚無演出。");
                if (GUILayout.Button("＋ 建立演出")) ctl.EnsureCutscene();
                GUILayout.EndScrollView(); GUILayout.EndArea(); return;
            }

            cs.autoStartOnEnter = GUILayout.Toggle(cs.autoStartOnEnter, "一進圖自動播 autoStart");
            if (!cs.autoStartOnEnter)
                GUILayout.Label("　↳ 關掉自動播＝改由 trigger「播放劇情(鏈動作)」啟動");

            // 條件旗標／完成寫旗標：與觸發點、地上物出現/消失旗標**完全同一套**——
            // 輸入旗標 id → 按「確認」→ 從全域 flags.json 撈出名稱並鎖定顯示（含生命週期）。
            // 只播一次的標準做法：條件＝某旗標「沒有」＋ 完成寫旗標＝同一個旗標。
            GUILayout.BeginHorizontal();
            GUILayout.Label("條件旗標", GUILayout.Width(64));
            DrawFlagFieldCore(cs.requireFlag ?? "", "cutscene/requireFlag", true, v => cs.requireFlag = v);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("完成寫旗標", GUILayout.Width(64));
            DrawFlagFieldCore(cs.setFlag ?? "", "cutscene/setFlag", false, v => cs.setFlag = v);
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(cs.requireFlag) && !string.IsNullOrEmpty(cs.setFlag)
                && cs.requireFlag.TrimStart('!') == cs.setFlag)
                GUILayout.Label(cs.requireFlag.StartsWith("!")
                    ? "　✔ 只播一次（生命週期由該旗標決定）"
                    : "　⚠ 條件與寫入是同一個旗標、但條件是「有」——第一次永遠不會播");

            cs.skippable = GUILayout.Toggle(cs.skippable, "可略過（右上角出現 Skip；ESC 同效）");
            if (cs.skippable)
                GUILayout.Label("　↳ ⚠ 略過＝快轉到結局：仍會執行 end 交棒與完成寫旗標");
            cs.lockInput = GUILayout.Toggle(cs.lockInput, "演出期間鎖操作 lockInput");
            cs.memoryFx = GUILayout.Toggle(cs.memoryFx, "回憶特效（泛黃＋上下黑邊＋解除場景壓暗）");
            cs.hidePlayer = GUILayout.Toggle(cs.hidePlayer, "演出期間隱藏主角（演完回原位）");
            cs.hideHud = GUILayout.Toggle(cs.hideHud, "演出期間關閉血量HUD（演完恢復）");
            cs.id = LabeledText("演出id(選填)", cs.id);
            GUILayout.Space(4);
            var pv = PreviewCtl();
            if (pv != null && pv.IsPlaying)
            {
                GUI.color = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("■ 停止預覽")) pv.Stop();
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button("▶ 預覽演出（Esc 略過）") && pv != null) pv.Play(cs, map);
                GUI.color = Color.white;
            }
            GUILayout.Space(4);
            if (GUILayout.Button("刪除整段演出"))
            {
                ctl.RemoveCutscene();
                GUILayout.EndScrollView(); GUILayout.EndArea(); return;
            }

            GUILayout.Space(6);
            GUILayout.Label($"── 演員（{cs.actors.Count}）──");
            if (GUILayout.Button("＋ 新增演員")) ctl.NewActor();
            CutsceneActor delA = null;
            foreach (var a in cs.actors)
            {
                GUILayout.BeginHorizontal();
                GUI.color = (a == ctl.SelectedActor) ? Color.cyan : Color.white;
                if (GUILayout.Button($"{a.id} [{a.kind}]", GUILayout.Width(150))) ctl.SelectActor(a);
                GUI.color = Color.white;
                if (GUILayout.Button("刪", GUILayout.Width(32))) delA = a;
                GUILayout.EndHorizontal();
            }
            if (delA != null) ctl.DeleteActor(delA);

            var sa = ctl.SelectedActor;
            if (sa != null)
            {
                GUILayout.Space(4);
                GUILayout.Label("── 編輯演員 ──");
                sa.id = LabeledText("id", sa.id);
                GUILayout.BeginHorizontal();
                GUILayout.Label("種類", GUILayout.Width(60));
                GUI.color = sa.kind == "npc" ? Color.cyan : Color.white;
                if (GUILayout.Button("npc", GUILayout.Width(50))) sa.kind = "npc";
                GUI.color = sa.kind == "player" ? Color.cyan : Color.white;
                if (GUILayout.Button("主角傀儡", GUILayout.Width(80))) sa.kind = "player";
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
                if (sa.kind == "npc") sa.spriteFolder = LabeledText("序列圖資料夾", sa.spriteFolder);
                FacingRow("起始朝向", ref sa.facing);
                sa.spawnAtStart = GUILayout.Toggle(sa.spawnAtStart, "開場就在場上（否＝等 spawn）");
                sa.flying = GUILayout.Toggle(sa.flying, "飛行：直線移動、不受可走層（蝴蝶/鬼魂）");
                if (_csActorBufFor != sa)
                {
                    _csActorBufFor = sa;
                    _csBufScale = sa.scale.ToString("0.###");
                    _csBufFps = sa.animFps.ToString("0.###");
                }
                _csBufScale = LabeledText("縮放 scale", _csBufScale); sa.scale = ParseFloatOr(_csBufScale, 1f);
                _csBufFps = LabeledText("動畫 fps", _csBufFps); sa.animFps = ParseFloatOr(_csBufFps, 8f);
                GUI.color = ctl.Mode == Tools.CutsceneController.PlaceMode.ActorPos ? Color.green : Color.white;
                if (GUILayout.Button("放置起點（點畫布）")) ctl.BeginPlaceActor();
                GUI.color = Color.white;
                GUILayout.Label($"起點 ({sa.x:0.0}, {sa.y:0.0})　朝{(sa.facing == "right" ? "右" : "左")}");
            }

            GUILayout.Space(8);
            GUILayout.Label($"── 步驟（{cs.steps.Count}）依序執行 ──");
            if (GUILayout.Button("＋ 新增步驟")) ctl.NewStep();
            CutsceneStep delS = null;
            for (int i = 0; i < cs.steps.Count; i++)
            {
                var s2 = cs.steps[i];
                GUILayout.BeginHorizontal();
                GUI.color = (s2 == ctl.SelectedStep) ? Color.cyan : Color.white;
                if (GUILayout.Button($"{i + 1}. {StepSummary(s2)}", GUILayout.Width(150))) ctl.SelectStep(s2);
                GUI.color = Color.white;
                if (GUILayout.Button("↑", GUILayout.Width(24))) ctl.MoveStep(s2, -1);
                if (GUILayout.Button("↓", GUILayout.Width(24))) ctl.MoveStep(s2, +1);
                if (GUILayout.Button("刪", GUILayout.Width(28))) delS = s2;
                GUILayout.EndHorizontal();
            }
            if (delS != null) ctl.DeleteStep(delS);

            var ss = ctl.SelectedStep;
            if (ss != null) DrawStepEditor(cs, ss, ctl);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawStepEditor(Cutscene cs, CutsceneStep s, Tools.CutsceneController ctl)
        {
            GUILayout.Space(4);
            GUILayout.Label("── 編輯步驟 ──");
            GUILayout.Label("型別");
            int per = 0;
            GUILayout.BeginHorizontal();
            foreach (var t in StepTypes)
            {
                GUI.color = s.type == t ? Color.cyan : Color.white;
                if (GUILayout.Button(t, GUILayout.Width(78))) s.type = t;
                GUI.color = Color.white;
                if (++per % 3 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();

            if (_csStepBufFor != s)
            {
                _csStepBufFor = s;
                _csBufDrama = s.dramaId.ToString();
                _csBufLang = s.langId.ToString();
                _csBufSeconds = s.seconds.ToString("0.###");
                _csBufZoom = s.zoom.ToString("0.###");
                _csBufSpeed = s.speed.ToString("0.###");
            }

            switch (s.type)
            {
                case "move":
                    ActorPicker(cs, s, false);
                    PlaceStepPosRow(ctl, s, "放置目標位置（點畫布）");
                    _csBufSpeed = LabeledText("速度(格/秒,0=預設2)", _csBufSpeed); s.speed = ParseFloatOr(_csBufSpeed, 0f);
                    FacingRow("抵達後朝向", ref s.facing);
                    s.parallelNext = GUILayout.Toggle(s.parallelNext, "與下一步同時（走位＋運鏡）");
                    s.background = GUILayout.Toggle(s.background, "背景執行（不擋後續：邊走，主線照跑到下一步）");
                    break;
                case "face":
                    ActorPicker(cs, s, false);
                    FacingRow("朝向", ref s.facing);
                    break;
                case "dialogue":
                    _csBufDrama = LabeledText("dramaId(DramaTable)", _csBufDrama); s.dramaId = ParseIntOr(_csBufDrama, 0);
                    GUILayout.Label("（沿用劇情系統，播完才繼續）");
                    break;
                case "bubble":
                    ActorPicker(cs, s, false);
                    _csBufLang = LabeledText("語言表id(LanguageTable)", _csBufLang); s.langId = ParseIntOr(_csBufLang, 0);
                    _csBufSeconds = LabeledText("顯示秒數(0=2)", _csBufSeconds); s.seconds = ParseFloatOr(_csBufSeconds, 0f);
                    s.background = GUILayout.Toggle(s.background, "背景執行（邊走邊講：主線照跑到下一步）");
                    GUILayout.Label("（頭上冒對話框，不跳對話視窗；文字寫在 LanguageTable.csv）");
                    break;
                case "wait":
                    _csBufSeconds = LabeledText("等待秒數", _csBufSeconds); s.seconds = ParseFloatOr(_csBufSeconds, 1f);
                    break;
                case "camera":
                    PlaceStepPosRow(ctl, s, "放置鏡頭中心（點畫布）");
                    _csBufZoom = LabeledText("縮放 zoom(1=原)", _csBufZoom); s.zoom = ParseFloatOr(_csBufZoom, 1f);
                    _csBufSeconds = LabeledText("過渡秒數", _csBufSeconds); s.seconds = ParseFloatOr(_csBufSeconds, 1f);
                    s.parallelNext = GUILayout.Toggle(s.parallelNext, "與下一步同時");
                    break;
                case "cameraFollow":
                    ActorPicker(cs, s, true);
                    s.parallelNext = GUILayout.Toggle(s.parallelNext, "與下一步同時");
                    break;
                case "comic":
                    s.assetId = LabeledText("圖片路徑id", s.assetId);
                    _csBufSeconds = LabeledText("停留秒數", _csBufSeconds); s.seconds = ParseFloatOr(_csBufSeconds, 3f);
                    GUILayout.Label("（置中顯示，期間演員暫停）");
                    break;
                case "fade":
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("方向", GUILayout.Width(60));
                    GUI.color = s.assetId != "in" ? Color.cyan : Color.white;
                    if (GUILayout.Button("淡出→全黑", GUILayout.Width(90))) s.assetId = "out";
                    GUI.color = s.assetId == "in" ? Color.cyan : Color.white;
                    if (GUILayout.Button("淡入→變回", GUILayout.Width(90))) s.assetId = "in";
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                    _csBufSeconds = LabeledText("秒數", _csBufSeconds); s.seconds = ParseFloatOr(_csBufSeconds, 1f);
                    GUILayout.Label("（淡出後維持全黑；要變回再加一個淡入）");
                    break;
                case "spawn":
                case "despawn":
                    ActorPicker(cs, s, false);
                    break;
                case "screenFx":
                    s.assetId = LabeledText("effectId(1睜眼/2破幻術/3馬賽克)", s.assetId);
                    _csBufSeconds = LabeledText("停留秒數", _csBufSeconds); s.seconds = ParseFloatOr(_csBufSeconds, 0f);
                    break;
                case "setFlag":
                    s.flag = LabeledText("旗標名", s.flag);
                    break;
                case "end":
                    s.assetId = LabeledText("去向(fall/mapId)", s.assetId);
                    GUILayout.Label("（結束演出並交棒）");
                    break;
            }
        }

        void ActorPicker(Cutscene cs, CutsceneStep s, bool allowPlayerEmpty)
        {
            GUILayout.Label("演員");
            GUILayout.BeginHorizontal();
            if (allowPlayerEmpty)
            {
                GUI.color = string.IsNullOrEmpty(s.actorId) ? Color.cyan : Color.white;
                if (GUILayout.Button("玩家", GUILayout.Width(60))) s.actorId = "";
            }
            foreach (var a in cs.actors)
            {
                GUI.color = s.actorId == a.id ? Color.cyan : Color.white;
                if (GUILayout.Button(a.id, GUILayout.Width(90))) s.actorId = a.id;
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        void PlaceStepPosRow(Tools.CutsceneController ctl, CutsceneStep s, string label)
        {
            GUI.color = ctl.Mode == Tools.CutsceneController.PlaceMode.StepPos ? Color.green : Color.white;
            if (GUILayout.Button(label)) ctl.BeginPlaceStepPos();
            GUI.color = Color.white;
            GUILayout.Label(s.hasPos ? $"目標 ({s.x:0.0}, {s.y:0.0})" : "（尚未設定位置）");
        }

        static void FacingRow(string label, ref string facing)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90));
            GUI.color = facing == "left" ? Color.cyan : Color.white;
            if (GUILayout.Button("左", GUILayout.Width(40))) facing = "left";
            GUI.color = facing == "right" ? Color.cyan : Color.white;
            if (GUILayout.Button("右", GUILayout.Width(40))) facing = "right";
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        static string LabeledText(string label, string val)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(130));
            string r = GUILayout.TextField(val ?? "", GUILayout.Width(90));
            GUILayout.EndHorizontal();
            return r;
        }

        static string StepSummary(CutsceneStep s)
        {
            switch (s.type)
            {
                case "move": return $"move {s.actorId}";
                case "face": return $"face {s.actorId} {s.facing}";
                case "dialogue": return $"對話 #{s.dramaId}";
                case "bubble": return $"頭上話 {s.actorId} #{s.langId}";
                case "wait": return $"等 {s.seconds:0.#}s";
                case "camera": return "運鏡";
                case "cameraFollow": return $"鏡頭跟 {(string.IsNullOrEmpty(s.actorId) ? "玩家" : s.actorId)}";
                case "comic": return $"漫畫 {Short(s.assetId ?? "")}";
                case "fade": return s.assetId == "in" ? "淡入" : "淡出全黑";
                case "spawn": return $"出現 {s.actorId}";
                case "despawn": return $"消失 {s.actorId}";
                case "screenFx": return $"螢幕fx {s.assetId}";
                case "setFlag": return $"旗標 {s.flag}";
                case "end": return $"結束→{s.assetId}";
                default: return s.type;
            }
        }

        static Rect CenteredRect(int w, int h)
            => new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
    }
}
