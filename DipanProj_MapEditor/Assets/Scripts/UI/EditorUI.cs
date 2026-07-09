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
        const float TopBarH = 30f;
        const float PaletteW = 240f;
        const float Thumb = 48f;
        const float InspectorW = 300f;
        const float InspectorH = 238f;

        public EditTool CurrentTool { get; private set; } = EditTool.TilePaint;
        public string SelectedObjectAssetId { get; private set; }

        /// <summary>可走工具的當前筆刷狀態字元（'0' 可走 / '1' 牆 / '2' 水）。</summary>
        public char WalkBrushState { get; private set; } = WalkableOps.Walk;

        /// <summary>可走工具的筆刷邊長（以子格計）：1~128。一筆塗 N×N 個子格。</summary>
        public int WalkBrushSize { get; private set; } = 1;
        static readonly int[] WalkBrushSizes = { 1, 2, 4, 8, 16, 32, 64, 128 };

        public void ClearObjectBrush() => SelectedObjectAssetId = null;

        // 座標/血量/FPS 輸入框暫存（依焦點決定要不要從物件同步回來）
        string _objXBuf = "", _objYBuf = "", _objHpBuf = "", _objFpsBuf = "";

        // Trigger
        public TriggerRegion CurrentRegion { get; private set; }
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
        // ★ 螢幕特效清單（維護點之一）：新增一種螢幕特效時，這裡加一列，並在遊戲端 ScreenFxPlayer.Play 加對應 case。
        static readonly (int id, string name, string desc)[] ScreenFxCatalog =
        {
            (1, "破幻術", "幻境崩碎回歸現實：玻璃裂紋→碎塊崩落色散→白光收尾。紅嫁衣沒殺家人分支傳去榕樹妖前播。"),
        };

        const string MapsDirPrefKey = "MapEditor.MapsDir";
        static string DefaultMapsDir => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Maps");
        string _mapsDir;   // 當前存讀檔資料夾（可自選，PlayerPrefs 記住）

        EditorCamera _cam;
        Tools.ObjectController _objCtl;
        Tools.SceneFxController _sfxCtl;
        EffectPreviewUI _preview;   // 特效預覽器（懶建立）
        Vector2 _sfxScroll;
        // 場景特效數字欄的文字緩衝（讓使用者可清空/自由編輯；空或無效 = 套預設）。切換選取時重新同步。
        SceneFxInstance _sfxBufFor;
        string _bufFxId, _bufBulge, _bufW, _bufH, _bufInterval;

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

        // 地磚多選（block stamp）：選取範圍以 tileset 的格座標表示
        string _blockId;       // 來源 tileset catalog id
        int _blockCols;        // 該 tileset 的欄數
        int _blockC0, _blockR0, _blockC1, _blockR1;
        bool _blockDragging;
        bool _tileBrushCleared;   // ESC 清掉地磚筆刷後，不再自動選回第一塊

        /// <summary>ESC 退出地磚筆刷：清掉選取、且不自動選回（直到使用者再點一塊）。</summary>
        public void ClearTileBrush() { _blockId = null; _tileBrushCleared = true; }

        void Start()
        {
            _cam = FindObjectOfType<EditorCamera>();
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

        // ---- 供 PaintController 查詢：指標是否壓在 UI 面板上 ----
        public bool IsPointerOverUI(Vector3 mousePos)
        {
            float ty = Screen.height - mousePos.y;          // 轉成左上原點 Y
            if (ty <= TopBarH) return true;                 // 頂部列
            if (mousePos.x >= Screen.width - PaletteW) return true; // 右側調色盤
            if (_showNew && CenteredRect(420, 380).Contains(new Vector2(mousePos.x, ty))) return true;
            if (_showSave && CenteredRect(460, 210).Contains(new Vector2(mousePos.x, ty))) return true;
            if (_showLoad && CenteredRect(460, 340).Contains(new Vector2(mousePos.x, ty))) return true;
            if (_showBg && CenteredRect(420, 280).Contains(new Vector2(mousePos.x, ty))) return true;
            if (_showFlags && CenteredRect(480, 420).Contains(new Vector2(mousePos.x, ty))) return true;
            if (_showScreenFx && CenteredRect(560, 360).Contains(new Vector2(mousePos.x, ty))) return true;
            if (CurrentTool == EditTool.EffectPreview) return true; // 預覽器佔滿畫面，不編輯地圖
            if (CurrentTool == EditTool.Object && ObjCtl()?.Selected != null
                && mousePos.x <= InspectorW && ty >= Screen.height - InspectorH)
                return true;                                // 物件選取面板
            return false;
        }

        void OnGUI()
        {
            DrawTopBar();
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
            else if (CurrentTool == EditTool.SceneFx)
            {
                DrawSceneFxPanel();
            }
            else if (CurrentTool == EditTool.EffectPreview)
            {
                if (_preview == null) _preview = new EffectPreviewUI();
                _preview.Draw(new Rect(0, TopBarH, Screen.width, Screen.height - TopBarH));
            }
            else
            {
                DrawPalette();
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
                _objects = null;
            }
            if (GUILayout.Button("背景", GUILayout.Width(50)) && MapSession.Instance?.Map != null) OpenDialog(bgDlg: true);

            GUILayout.Space(12);
            // 工具切換
            GUI.color = CurrentTool == EditTool.TilePaint ? Color.cyan : Color.white;
            if (GUILayout.Button("畫", GUILayout.Width(40))) CurrentTool = EditTool.TilePaint;
            GUI.color = CurrentTool == EditTool.Erase ? Color.cyan : Color.white;
            if (GUILayout.Button("擦", GUILayout.Width(40))) CurrentTool = EditTool.Erase;
            GUI.color = CurrentTool == EditTool.Object ? Color.cyan : Color.white;
            if (GUILayout.Button("物件", GUILayout.Width(50))) CurrentTool = EditTool.Object;
            GUI.color = CurrentTool == EditTool.Walkable ? Color.cyan : Color.white;
            if (GUILayout.Button("可走", GUILayout.Width(50))) CurrentTool = EditTool.Walkable;
            GUI.color = CurrentTool == EditTool.Trigger ? Color.cyan : Color.white;
            if (GUILayout.Button("Trigger", GUILayout.Width(70))) { CurrentTool = EditTool.Trigger; TriggerPaintMode = true; }
            GUI.color = Color.white;   // 旗標不是工具，永遠白（否則會沿用上一顆 Trigger 的 cyan 而看起來被選取）
            if (GUILayout.Button("旗標", GUILayout.Width(50))) { if (CurrentTool == EditTool.EffectPreview) CurrentTool = EditTool.TilePaint; _showFlags = true; _flagMsg = ""; }
            GUI.color = CurrentTool == EditTool.SceneFx ? Color.cyan : Color.white;
            if (GUILayout.Button("場景特效", GUILayout.Width(80))) CurrentTool = EditTool.SceneFx;
            GUI.color = CurrentTool == EditTool.EffectPreview ? Color.cyan : Color.white;
            if (GUILayout.Button("特效預覽器", GUILayout.Width(90)))
            {
                CurrentTool = EditTool.EffectPreview;
                _showNew = _showSave = _showLoad = _showBg = _showFlags = _showScreenFx = false; // 開預覽器就收起所有彈窗（含新建地圖）
            }
            GUI.color = Color.white;

            GUILayout.Space(12);
            var map = MapSession.Instance?.Map;
            if (map != null)
                GUILayout.Label($"地圖：{map.name}　|　module：{map.module}　|　{map.width}×{map.height} 格　|　tile {map.tileSize}");
            else
                GUILayout.Label("尚無地圖，請按「新建地圖」");

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_statusMsg)) GUILayout.Label(_statusMsg);
            GUILayout.Label("左鍵畫/擦　中鍵·右鍵平移　滾輪縮放");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ---- 存檔 / 讀檔 ----

        /// <summary>對話框互斥：開其中一個就關掉其他。</summary>
        void OpenDialog(bool newDlg = false, bool saveDlg = false, bool loadDlg = false, bool bgDlg = false)
        {
            if (CurrentTool == EditTool.EffectPreview) CurrentTool = EditTool.TilePaint; // 從預覽器點「新建/存/讀/背景」→ 先離開預覽器
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
                _blockId = null; _objects = null;
                _statusMsg = $"已讀入：{Path.GetFileName(path)}";
            }
            else _statusMsg = $"讀檔失敗：{Path.GetFileName(path)}";
            OpenDialog();   // 關掉所有對話框（含開機殘留的新建對話框）
        }

        // ---- 地磚調色盤（依拼接圖原始格狀排列，可拖曳框選整塊） ----

        public bool HasTileBrush => !string.IsNullOrEmpty(_blockId) && _blockCols > 0;
        public int TileBrushW => Mathf.Abs(_blockC1 - _blockC0) + 1;
        public int TileBrushH => Mathf.Abs(_blockR1 - _blockR0) + 1;
        public string TileBrushAt(int dx, int dy)
        {
            int cmin = Mathf.Min(_blockC0, _blockC1), rmin = Mathf.Min(_blockR0, _blockR1);
            int idx = (rmin + dy) * _blockCols + (cmin + dx);
            return $"{_blockId}#{idx}";
        }

        List<CatalogItem> TilesetItems()
        {
            var list = new List<CatalogItem>();
            var cat = MapSession.Instance?.Catalog;
            string module = MapSession.Instance?.Map?.module ?? "";
            if (cat == null) return list;
            foreach (var it in cat.items)
                if (TilesetService.IsTilesetCategory(it.category) && (it.module == "Main" || it.module == module))
                    list.Add(it);
            return list;
        }

        bool InTileSelection(int c, int r)
        {
            int cmin = Mathf.Min(_blockC0, _blockC1), cmax = Mathf.Max(_blockC0, _blockC1);
            int rmin = Mathf.Min(_blockR0, _blockR1), rmax = Mathf.Max(_blockR0, _blockR1);
            return c >= cmin && c <= cmax && r >= rmin && r <= rmax;
        }

        void DrawPalette()
        {
            var rect = new Rect(Screen.width - PaletteW, TopBarH, PaletteW, Screen.height - TopBarH);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("地磚調色盤");
            GUILayout.Label(HasTileBrush ? $"選取：{Short(_blockId)} {TileBrushW}×{TileBrushH}" : "未選取");
            GUILayout.Label("在地磚上左鍵拖曳框選多格，\n再到地圖上點/拖一次貼整塊。");
            GUILayout.Space(4);

            var tilesets = TilesetItems();
            if (tilesets.Count == 0)
            {
                GUILayout.Label("沒有可畫的地磚。\n請把地磚 texture 放進關卡的\nTiles 資料夾，再同步素材。");
                GUILayout.EndArea();
                return;
            }

            // 預設選取（或選取的 tileset 不在當前清單時重設）；ESC 清過就不自動選回
            if (!_tileBrushCleared && (string.IsNullOrEmpty(_blockId) || tilesets.FindIndex(it => it.id == _blockId) < 0))
                SelectTileBlockDefault(tilesets[0]);

            var e = Event.current;

            foreach (var item in tilesets)
            {
                var tex = SpriteCache.GetTexture(item);
                if (tex == null) continue;
                int cols = Mathf.Max(1, tex.width / TileNativePx);
                int rows = Mathf.Max(1, tex.height / TileNativePx);
                float cell = Thumb;

                GUILayout.Label(Short(item.id));
                Rect area = GUILayoutUtility.GetRect(cols * cell, rows * cell,
                    GUILayout.Width(cols * cell), GUILayout.Height(rows * cell));

                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        Rect cr = new Rect(area.x + c * cell, area.y + r * cell, cell - 1, cell - 1);
                        var tc = new Rect((float)c / cols, 1f - (float)(r + 1) / rows, 1f / cols, 1f / rows);
                        if (e.type == EventType.Repaint) GUI.DrawTextureWithTexCoords(cr, tex, tc);
                        if (item.id == _blockId && InTileSelection(c, r)) DrawBorder(cr, Color.cyan);
                    }

                // 拖曳框選（限本 tileset 範圍）
                if (e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                    && area.Contains(e.mousePosition))
                {
                    int c = Mathf.Clamp((int)((e.mousePosition.x - area.x) / cell), 0, cols - 1);
                    int r = Mathf.Clamp((int)((e.mousePosition.y - area.y) / cell), 0, rows - 1);
                    if (e.type == EventType.MouseDown)
                    {
                        _blockId = item.id; _blockCols = cols;
                        _blockC0 = _blockC1 = c; _blockR0 = _blockR1 = r;
                        _blockDragging = true; _tileBrushCleared = false; e.Use();
                    }
                    else if (_blockDragging && _blockId == item.id)
                    {
                        _blockC1 = c; _blockR1 = r; e.Use();
                    }
                }
            }

            if (e.type == EventType.MouseUp && e.button == 0) _blockDragging = false;

            GUILayout.EndArea();
        }

        void SelectTileBlockDefault(CatalogItem item)
        {
            var tex = SpriteCache.GetTexture(item);
            _blockId = item.id;
            _blockCols = tex != null ? Mathf.Max(1, tex.width / TileNativePx) : 1;
            _blockC0 = _blockR0 = _blockC1 = _blockR1 = 0;
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

            var rect = new Rect(Screen.width - PaletteW, TopBarH, PaletteW, Screen.height - TopBarH);
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
            var sel = ctl?.Selected;
            if (sel == null) return;   // 沒選取就不畫面板（避免擋住點擊）

            var rect = new Rect(0, Screen.height - InspectorH, InspectorW, InspectorH);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label($"選取：{Short(sel.assetId)}　縮放 {sel.scaleX:0.00}　角度 {sel.rot:0}°　層 {sel.zOrder}");

            // 座標：未編輯時顯示物件當前座標；改數值或按 ± 就移動（每次 ±0.1）
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
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("上移層")) { UndoManager.Push(); ctl.RaiseZ(); }
            if (GUILayout.Button("下移層")) { UndoManager.Push(); ctl.LowerZ(); }
            GUILayout.EndHorizontal();

            // 可走（勾選＝這個地上物不擋路、不設碰撞，走不走由地圖該格可走層決定；例：木板/地毯可踩上去）。
            bool nextWalk = GUILayout.Toggle(sel.walkable, " 可走（不擋路，走地圖判定）");
            if (nextWalk != sel.walkable) { UndoManager.Push(); sel.walkable = nextWalk; }

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
            if (!sel.walkable && sel.hp != -1)
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
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("取消選取")) ctl.Deselect();
            if (GUILayout.Button("刪除")) { UndoManager.Push(); ctl.DeleteSelected(); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ---- 可走/不可走筆刷面板 ----

        void DrawWalkablePanel()
        {
            var map = MapSession.Instance?.Map;
            var rect = new Rect(Screen.width - PaletteW, TopBarH, PaletteW, Screen.height - TopBarH);
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

            var rect = new Rect(Screen.width - PaletteW, TopBarH, PaletteW, Screen.height - TopBarH);
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
                if (GUILayout.Button($"{r.name}（{r.cells.Count}格）", GUILayout.Width(160))) { CurrentRegion = r; TriggerPaintMode = true; TriggerNewRegionPerStroke = false; }
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
                // 循環按鈕：在固定選項間切換，避免打錯字。留空＝顯示第一個選項(＝預設)。
                string cur = (r.Params.TryGetValue(p.key, out var v) && v != null) ? v.ToString() : "";
                int idx = System.Array.IndexOf(p.options, cur);
                string shown = idx >= 0 ? cur : p.options[0];
                if (GUILayout.Button(shown))
                    r.Params[p.key] = p.options[idx < 0 ? 0 : (idx + 1) % p.options.Length];
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

            var rect = new Rect(Screen.width - PaletteW, TopBarH, PaletteW, Screen.height - TopBarH);
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

        static string Short(string id)
        {
            int slash = id.LastIndexOf('/');
            return slash >= 0 ? id.Substring(slash + 1) : id;
        }

        static Rect CenteredRect(int w, int h)
            => new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
    }
}
