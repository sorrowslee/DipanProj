using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using Dipan.Data;
using Dipan.Inventory;

/// <summary>
/// **武器工坊**（Editor-only，放 Assets/Editor，不進 build）：在 Unity 裡「選外型 → 選模式 → 填效果 → 立刻射出去看」的一站式武器編輯器。
///
/// 選單 <b>Project Tools/武器工坊</b>。左邊視窗、右邊 Game 畫面；Play 中改任何值，下一發就是新的。
///
/// <para><b>它編輯的就是 CSV 本身</b>：把 <c>Assets/Data/WeaponTable.csv</c>／<c>RecipeTable.csv</c> 讀成「欄名 → 值」字典，
/// 依 <see cref="WeaponModeSpec"/> 只顯示對目前 Mode 有效的欄（無效的根本不出現，互斥從結構上消失）、
/// 每欄依 <see cref="FieldSpec"/> 的型別長出對的控制項（列舉→下拉、數值→帶範圍提示、布林→勾選），
/// 「儲存」再用 <see cref="CsvWriter"/> 把整張表寫回（表頭與分組註解由 spec 產，保證跟程式一致）。</para>
///
/// <para><b>Play 模式的即時模擬</b>：<see cref="RecipeManager.CreateTransient"/> ＋ <see cref="WeaponManager.CreateTransient"/>
/// 做出一把不登記進表的臨時武器，塞進 <see cref="WeaponManager.SimulationOverride"/>——PlayerController 的所有發射路徑
/// （離散／雷射／佛光／集氣）都會拿它去打；它也照樣過玩家的鑲嵌解析，所以真的去鍛造鑲珠子也有效。
/// 模擬跟「儲存」是兩件事：存了檔，執行中的遊戲仍用舊表（Awake 載一次），下次 Play 才生效。</para>
///
/// <para><b>刻意不做的</b>：不產 ItemTable 列（只提示）、不做勾選式的假鑲嵌（作者要真鑲）、不刪武器／配方（改用 git）。
/// 見 readme/WEAPON_WORKBENCH.md。</para>
/// </summary>
public class WeaponWorkbench : EditorWindow
{
    const string RecipePath = "Assets/Data/RecipeTable.csv";
    const string WeaponPath = "Assets/Data/WeaponTable.csv";
    const string VfxPath = "Assets/Data/VfxTable.csv";
    const string GroundPath = "Assets/Data/GroundEffectTable.csv";
    const string ItemPath = "Assets/Data/ItemTable.csv";
    const string WeaponArtRoot = "Assets/Resources/Weapon";
    const string ResourcesPrefix = "Assets/Resources/";

    [MenuItem("Project Tools/武器工坊 (Weapon Workbench)", false, 30)]
    static void Open()
    {
        var w = GetWindow<WeaponWorkbench>("武器工坊");
        w.minSize = new Vector2(520, 600);
    }

    // ───────────────────────── 資料 ─────────────────────────

    // 與 CSV 一對一的「欄名 → 值」字典（空白 = 預設）。編輯的就是這個，存檔就是把它們寫回去。
    readonly List<Dictionary<string, string>> _weapons = new List<Dictionary<string, string>>();
    readonly List<Dictionary<string, string>> _recipes = new List<Dictionary<string, string>>();
    readonly List<string> _loadErrors = new List<string>();

    // 下拉用的參考表（只讀名字）
    readonly List<int> _vfxIds = new List<int>();       readonly List<string> _vfxLabels = new List<string>();
    readonly List<int> _groundIds = new List<int>();    readonly List<string> _groundLabels = new List<string>();
    readonly Dictionary<int, string> _groundDuration = new Dictionary<int, string>();
    readonly HashSet<int> _itemWeaponIds = new HashSet<int>();   // ItemTable 裡 WeaponID 有指到的武器（能裝備的）

    // 外型（掃 Resources/Weapon）
    class LookOption { public string Label; public string SpritePath = ""; public string AniPath = ""; public int Frames; public string PreviewAsset; }
    readonly List<LookOption> _looks = new List<LookOption>();

    int _sel = -1;              // 目前選的武器（_weapons 的索引）
    bool _dirty;
    // 「儲存」不直接存：先把鍵盤焦點拿掉、等下一次 OnGUI 讓 DelayedTextField 把還沒提交的文字寫進字典，再存。
    // 否則「改了名字直接點儲存」會存到舊名字（Delayed 欄位要 Enter／失焦才提交，而點按鈕不會搶走焦點）。
    bool _saveRequested;
    Vector2 _scroll;

    // Play 模擬
    bool _autoApply = true;
    string _simStatus = "";
    bool _simError;
    bool _applyRequested;
    double _applyAt;
    bool _simActive;

    static readonly Color WarnColor = new Color(1f, 0.55f, 0.35f);
    static readonly Color ErrColor = new Color(1f, 0.35f, 0.35f);
    static readonly Color DimColor = new Color(0.6f, 0.6f, 0.6f);

    // ───────────────────────── 生命週期 ─────────────────────────

    void OnEnable()
    {
        LoadAll();
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    void OnDisable()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    void OnPlayModeChanged(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingPlayMode || s == PlayModeStateChange.EnteredEditMode)
        {
            _simActive = false; _simStatus = ""; _simError = false; _applyRequested = false;
            Repaint();
        }
    }

    void Tick()
    {
        if (_applyRequested && EditorApplication.timeSinceStartup >= _applyAt)
        {
            _applyRequested = false;
            ApplySimulation();
            Repaint();
        }
    }

    // ───────────────────────── 載入 ─────────────────────────

    void LoadAll()
    {
        _loadErrors.Clear();
        LoadTable(RecipePath, "RecipeTable", FieldTable.Recipe, _recipes);
        LoadTable(WeaponPath, "WeaponTable", FieldTable.Weapon, _weapons);

        _vfxIds.Clear(); _vfxLabels.Clear();
        _vfxIds.Add(0); _vfxLabels.Add("（無）");
        foreach (var r in ParseFile(VfxPath, "VfxTable").Rows)
        {
            int id = r.GetInt("ID", 0); if (id <= 0) continue;
            _vfxIds.Add(id); _vfxLabels.Add($"{id} {Safe(r.Get("Name"))}");
        }
        _groundIds.Clear(); _groundLabels.Clear(); _groundDuration.Clear();
        _groundIds.Add(0); _groundLabels.Add("（無）");
        foreach (var r in ParseFile(GroundPath, "GroundEffectTable").Rows)
        {
            int id = r.GetInt("ID", 0); if (id <= 0) continue;
            _groundIds.Add(id); _groundLabels.Add($"{id} {Safe(r.Get("Name"))}（半徑 {r.Get("Radius")}，持續 {r.Get("Duration")}）");
            _groundDuration[id] = r.Get("Duration");
        }
        _itemWeaponIds.Clear();
        foreach (var r in ParseFile(ItemPath, "ItemTable").Rows)
        {
            int wid = r.GetInt("WeaponID", 0); if (wid > 0) _itemWeaponIds.Add(wid);
        }
        ScanLooks();

        if (_sel >= _weapons.Count) _sel = _weapons.Count - 1;
        if (_sel < 0 && _weapons.Count > 0) _sel = 0;
        _dirty = false;
    }

    CsvTable ParseFile(string path, string name)
    {
        if (!File.Exists(path)) { _loadErrors.Add($"找不到 {path}"); return CsvTable.Parse("", name); }
        var t = CsvTable.Parse(File.ReadAllText(path, Encoding.UTF8), name);
        foreach (var e in t.Errors) _loadErrors.Add(e);
        return t;
    }

    void LoadTable(string path, string name, FieldTable table, List<Dictionary<string, string>> into)
    {
        into.Clear();
        var t = ParseFile(path, name);
        var unknown = t.UnknownColumns(WeaponModeSpec.ColumnNames(table));
        if (unknown.Count > 0) _loadErrors.Add($"{name} 表頭有程式不認得的欄位，存檔時會被丟掉：{string.Join(", ", unknown)}");
        foreach (var r in t.Rows)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in r.ToDictionary())
                if (WeaponModeSpec.GetField(kv.Key) != null) d[kv.Key] = kv.Value;   // 只留 spec 認得的欄
            into.Add(d);
        }
    }

    /// <summary>掃 Resources/Weapon 底下所有圖：檔名結尾 _NN 的歸成序列圖（依前綴分組），其餘是單張。</summary>
    void ScanLooks()
    {
        _looks.Clear();
        _looks.Add(new LookOption { Label = "（無圖：隱形子彈，只靠 TrailEffectID 沿路種特效）" });
        var seq = new Dictionary<string, List<string>>();   // 前綴 → 幀檔案路徑
        var singles = new List<string>();
        var rx = new Regex(@"^(.*)_(\d{2,})$");
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { WeaponArtRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith(ResourcesPrefix)) continue;
            string rel = path.Substring(ResourcesPrefix.Length);
            rel = rel.Substring(0, rel.LastIndexOf('.'));
            var m = rx.Match(rel);
            if (m.Success)
            {
                string prefix = m.Groups[1].Value;
                if (!seq.TryGetValue(prefix, out var list)) seq[prefix] = list = new List<string>();
                list.Add(path);
            }
            else singles.Add(rel);
        }
        singles.Sort();
        foreach (var s in singles)
            _looks.Add(new LookOption { Label = "單張  " + Nice(s), SpritePath = s, PreviewAsset = ResourcesPrefix + s + ".png" });
        foreach (var kv in seq.OrderBy(k => k.Key))
        {
            kv.Value.Sort();
            _looks.Add(new LookOption { Label = $"序列  {Nice(kv.Key)}（{kv.Value.Count} 幀）", AniPath = kv.Key, Frames = kv.Value.Count, PreviewAsset = kv.Value[0] });
        }
    }

    // Popup 會把 "/" 當子選單分隔，顯示用另一個符號
    static string Nice(string s) => (s ?? "").Replace("/", " › ");
    static string Safe(string s) => Nice(s);

    // ───────────────────────── 存檔 ─────────────────────────

    void SaveAll()
    {
        try
        {
            WriteTable(RecipePath, FieldTable.Recipe, _recipes);
            WriteTable(WeaponPath, FieldTable.Weapon, _weapons);
            AssetDatabase.ImportAsset(RecipePath);
            AssetDatabase.ImportAsset(WeaponPath);
            _dirty = false;
            string note = EditorApplication.isPlaying
                ? "已寫回 CSV。⚠ 執行中的遊戲仍用舊表（Awake 只載一次），下次 Play 才生效；目前的模擬武器不受影響。"
                : "已寫回 RecipeTable.csv / WeaponTable.csv。";
            ShowNotification(new GUIContent("已儲存"));
            Debug.Log("[武器工坊] " + note);
        }
        catch (Exception e)
        {
            Debug.LogError("[武器工坊] 存檔失敗：" + e.Message);
            EditorUtility.DisplayDialog("武器工坊", "存檔失敗：" + e.Message, "好");
        }
    }

    static void WriteTable(string path, FieldTable table, List<Dictionary<string, string>> rows)
    {
        var cols = WeaponModeSpec.ColumnNames(table);
        var ordered = rows.OrderBy(r => CsvFieldParse.Int(Get(r, "ID"), int.MaxValue)).Cast<IReadOnlyDictionary<string, string>>();
        string text = CsvWriter.Build(WeaponModeSpec.HeaderCells(table), cols, WeaponModeSpec.GroupCommentLines(table), ordered);
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    // ───────────────────────── 小工具 ─────────────────────────

    static string Get(Dictionary<string, string> d, string key) => (d != null && d.TryGetValue(key, out var v) && v != null) ? v.Trim() : "";
    static void Set(Dictionary<string, string> d, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) d.Remove(key); else d[key] = value.Trim();
    }
    static int IdOf(Dictionary<string, string> d) => CsvFieldParse.Int(Get(d, "ID"), 0);
    int NextId(List<Dictionary<string, string>> rows) => rows.Count == 0 ? 1 : rows.Max(IdOf) + 1;

    Dictionary<string, string> RecipeOf(Dictionary<string, string> weapon)
    {
        int rid = CsvFieldParse.Int(Get(weapon, "RecipeID"), 0);
        return _recipes.FirstOrDefault(r => IdOf(r) == rid);
    }

    static WeaponMode ModeOf(Dictionary<string, string> recipe)
    {
        WeaponModeSpec.TryParseMode(Get(recipe, "Mode"), out var m);
        return m;
    }

    string WeaponLabel(Dictionary<string, string> w)
    {
        var r = RecipeOf(w);
        string mode = r != null ? WeaponModeSpec.ModeLabel(ModeOf(r)) : "配方缺失";
        return $"{IdOf(w)}  {Safe(Get(w, "Name"))}  [{mode}]";
    }
    string RecipeLabel(Dictionary<string, string> r) => $"{IdOf(r)}  {Safe(Get(r, "Name"))}  [{WeaponModeSpec.ModeLabel(ModeOf(r))}]";

    // ───────────────────────── 新增 / 複製 ─────────────────────────

    void AddWeapon(Dictionary<string, string> template, Dictionary<string, string> recipeTemplate)
    {
        int wid = NextId(_weapons), rid = NextId(_recipes);
        var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (recipeTemplate != null) foreach (var kv in recipeTemplate) r[kv.Key] = kv.Value;
        r["ID"] = rid.ToString();
        r["Name"] = recipeTemplate != null ? Get(recipeTemplate, "Name") + " 複本" : "新配方 " + rid;
        _recipes.Add(r);

        var w = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (template != null) foreach (var kv in template) w[kv.Key] = kv.Value;
        w["ID"] = wid.ToString();
        w["Name"] = template != null ? Get(template, "Name") + " 複本" : "新武器 " + wid;
        w["RecipeID"] = rid.ToString();
        if (template == null) w["Damage"] = "1";
        _weapons.Add(w);
        _sel = _weapons.Count - 1;
        _dirty = true;
    }

    // ───────────────────────── GUI ─────────────────────────

    void OnGUI()
    {
        EditorGUIUtility.labelWidth = 175f;
        DrawToolbar();
        foreach (var e in _loadErrors) EditorGUILayout.HelpBox(e, MessageType.Error);
        DrawPlayBar();

        if (_sel < 0 || _sel >= _weapons.Count)
        {
            EditorGUILayout.HelpBox("沒有武器。按「＋新增」建一把。", MessageType.Info);
            FlushPendingSave();
            return;
        }

        var weapon = _weapons[_sel];
        var recipe = RecipeOf(weapon);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUI.BeginChangeCheck();

        DrawWeaponSection(weapon, recipe);
        EditorGUILayout.Space(8);
        if (recipe != null) DrawRecipeSection(weapon, recipe);
        EditorGUILayout.Space(8);
        DrawValidation(weapon, recipe);

        bool changed = EditorGUI.EndChangeCheck();
        EditorGUILayout.EndScrollView();

        if (changed)
        {
            _dirty = true;
            if (EditorApplication.isPlaying && _autoApply) RequestApply();
        }
        FlushPendingSave();
    }

    void RequestSave()
    {
        GUI.FocusControl(null);
        EditorGUIUtility.editingTextField = false;
        _saveRequested = true;
        Repaint();
    }

    /// <summary>OnGUI 尾端：焦點已拿掉、欄位都畫過一輪（Delayed 欄位此時已提交）→ 真的存。挑 Repaint 事件存，保證 Layout 那一輪先跑過。</summary>
    void FlushPendingSave()
    {
        if (!_saveRequested || Event.current.type != EventType.Repaint) return;
        _saveRequested = false;
        SaveAll();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        var labels = _weapons.Select(WeaponLabel).ToArray();
        int newSel = EditorGUILayout.Popup(_sel, labels, EditorStyles.toolbarPopup, GUILayout.MinWidth(220));
        if (newSel != _sel) { _sel = newSel; if (EditorApplication.isPlaying && _autoApply) RequestApply(); }

        if (GUILayout.Button("＋新增", EditorStyles.toolbarButton, GUILayout.Width(52))) AddWeapon(null, null);
        using (new EditorGUI.DisabledScope(_sel < 0))
            if (GUILayout.Button("複製", EditorStyles.toolbarButton, GUILayout.Width(44)))
                AddWeapon(_weapons[_sel], RecipeOf(_weapons[_sel]));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("重新載入", EditorStyles.toolbarButton, GUILayout.Width(64)))
        {
            if (!_dirty || EditorUtility.DisplayDialog("武器工坊", "有未儲存的修改，重新載入會丟掉。確定？", "丟掉並重載", "取消"))
                LoadAll();
        }
        var saveStyle = new GUIStyle(EditorStyles.toolbarButton);
        if (_dirty) saveStyle.normal.textColor = WarnColor;
        if (GUILayout.Button(_dirty ? "儲存 ●" : "儲存", saveStyle, GUILayout.Width(64))) RequestSave();
        EditorGUILayout.EndHorizontal();
    }

    void DrawPlayBar()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("進 Play 後這裡會出現「模擬」：改任何值，玩家射出去的就是這把（不用裝備、不用存檔）。建議用 Project Tools/測試/直接進關卡/競技場 進場。", MessageType.Info);
            return;
        }
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        _autoApply = GUILayout.Toggle(_autoApply, "改了就自動套用", GUILayout.Width(130));
        if (GUILayout.Button("套用到玩家", GUILayout.Width(90))) ApplySimulation();
        using (new EditorGUI.DisabledScope(!_simActive))
            if (GUILayout.Button("解除模擬", GUILayout.Width(80))) ClearSimulation();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("給每種珠子 Lv3", GUILayout.Width(110))) GiveGems();
        if (GUILayout.Button("裝備中武器開 6 孔", GUILayout.Width(130))) RerollSockets(6);
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(_simStatus))
        {
            var st = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            st.normal.textColor = _simError ? ErrColor : new Color(0.55f, 0.9f, 0.55f);
            EditorGUILayout.LabelField(_simStatus, st);
        }
        EditorGUILayout.LabelField("珠子要真鑲：給珠子＋開孔後，遊戲裡按 Y 開鍛造，把珠子拖進裝備中武器（或護身符／戒指）的孔——模擬武器會吃到。", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    // ── 武器區 ──
    void DrawWeaponSection(Dictionary<string, string> weapon, Dictionary<string, string> recipe)
    {
        WeaponMode mode = recipe != null ? ModeOf(recipe) : WeaponMode.Normal;
        EditorGUILayout.LabelField("武器（WeaponTable）", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField("ID", Get(weapon, "ID"));
        // ⚠ 名稱一定要用 DelayedTextField：即時版每一幀把 Trim 過的值塞回欄位，中文輸入法組字到一半的字串會被打斷，
        //   結果怎麼打都留不住（作者回報「新增武器後改不了名」）。Delayed 版按 Enter／離開欄位才提交。
        Set(weapon, "Name", EditorGUILayout.DelayedTextField(new GUIContent("名稱", "只給人看；打完按 Enter"), Get(weapon, "Name")));

        // 配方選擇
        EditorGUILayout.BeginHorizontal();
        var rlabels = _recipes.Select(RecipeLabel).ToArray();
        int rid = CsvFieldParse.Int(Get(weapon, "RecipeID"), 0);
        int ridx = _recipes.FindIndex(r => IdOf(r) == rid);
        int nidx = EditorGUILayout.Popup(new GUIContent("配方", "這把武器的行為（RecipeTable）"), ridx, rlabels);
        if (nidx != ridx && nidx >= 0) Set(weapon, "RecipeID", IdOf(_recipes[nidx]).ToString());
        if (GUILayout.Button("＋新配方", GUILayout.Width(70)))
        {
            int nr = NextId(_recipes);
            _recipes.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "ID", nr.ToString() }, { "Name", Get(weapon, "Name") } });
            Set(weapon, "RecipeID", nr.ToString());
            GUI.changed = true;
        }
        EditorGUILayout.EndHorizontal();
        if (recipe == null) EditorGUILayout.HelpBox($"RecipeID={rid} 在 RecipeTable 找不到，請選一個配方或按「＋新配方」。", MessageType.Error);
        else
        {
            int shared = _weapons.Count(w => CsvFieldParse.Int(Get(w, "RecipeID"), 0) == rid);
            if (shared > 1) EditorGUILayout.HelpBox($"這個配方被 {shared} 把武器共用，改它會一起改到。想獨立就按「＋新配方」。", MessageType.Warning);
        }

        // 依模式顯示武器欄
        var fields = WeaponModeSpec.EffectiveFields(mode, FieldTable.Weapon);
        bool lookDrawn = false;
        string group = null;
        foreach (var f in fields)
        {
            if (f.Name == "ID" || f.Name == "Name" || f.Name == "RecipeID") continue;
            if (f.Group != group) { group = f.Group; EditorGUILayout.Space(4); EditorGUILayout.LabelField(group, EditorStyles.miniBoldLabel); }
            if (f.Name == "WeaponSpritePath" || f.Name == "WeaponAniPath" || f.Name == "WeaponAniNumber")
            {
                if (!lookDrawn) { DrawLookPicker(weapon); lookDrawn = true; }
                continue;
            }
            DrawField(f, weapon, mode);
        }
        EditorGUILayout.EndVertical();
    }

    /// <summary>外型下拉：單張／序列二選一，選了就同時填 WeaponSpritePath 或 WeaponAniPath＋WeaponAniNumber（另一種清掉）。</summary>
    void DrawLookPicker(Dictionary<string, string> weapon)
    {
        string sp = Get(weapon, "WeaponSpritePath"), ap = Get(weapon, "WeaponAniPath");
        int cur = 0;
        if (!string.IsNullOrEmpty(ap)) cur = _looks.FindIndex(l => l.AniPath == ap);
        else if (!string.IsNullOrEmpty(sp)) cur = _looks.FindIndex(l => l.SpritePath == sp);
        bool unknown = cur < 0;
        if (unknown) cur = 0;

        EditorGUILayout.BeginHorizontal();
        int n = EditorGUILayout.Popup(new GUIContent("外型", "掃 Resources/Weapon：單張圖或序列圖"), cur, _looks.Select(l => l.Label).ToArray());
        if (GUILayout.Button("刷新", GUILayout.Width(40))) ScanLooks();
        EditorGUILayout.EndHorizontal();
        if (unknown) EditorGUILayout.HelpBox($"表上填的圖不在 Resources/Weapon 底下：{(ap.Length > 0 ? ap : sp)}（保留原值，換一個就會覆蓋）", MessageType.Warning);

        if (n != cur || (!unknown && n == 0 && (sp.Length > 0 || ap.Length > 0)))
        {
            var pick = _looks[Mathf.Clamp(n, 0, _looks.Count - 1)];
            Set(weapon, "WeaponSpritePath", pick.SpritePath);
            Set(weapon, "WeaponAniPath", pick.AniPath);
            Set(weapon, "WeaponAniNumber", pick.Frames > 0 ? pick.Frames.ToString() : "");
            if (pick.Frames > 0 && Get(weapon, "AnimFPS").Length == 0) Set(weapon, "AnimFPS", "12");
            GUI.changed = true;
        }

        // 縮圖
        var look = _looks[Mathf.Clamp(cur, 0, _looks.Count - 1)];
        if (!string.IsNullOrEmpty(look.PreviewAsset))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(look.PreviewAsset);
            if (tex != null)
            {
                var rect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
                rect.x += EditorGUIUtility.labelWidth;
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
            }
        }
    }

    // ── 配方區 ──
    void DrawRecipeSection(Dictionary<string, string> weapon, Dictionary<string, string> recipe)
    {
        WeaponMode mode = ModeOf(recipe);
        EditorGUILayout.LabelField($"配方（RecipeTable #{IdOf(recipe)}）", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        Set(recipe, "Name", EditorGUILayout.DelayedTextField(new GUIContent("配方名稱", "只給人看；打完按 Enter"), Get(recipe, "Name")));   // 同上，Delayed

        // Mode 下拉（中文＋英文）
        var modes = Enum.GetValues(typeof(WeaponMode)).Cast<WeaponMode>().ToArray();
        var mlabels = modes.Select(m => $"{WeaponModeSpec.ModeLabel(m)}  ({m})").ToArray();
        int mi = Array.IndexOf(modes, mode);
        int nmi = EditorGUILayout.Popup(new GUIContent("發射模式 Mode", "一列只能一種；換模式後只會顯示對該模式有效的欄"), mi, mlabels);
        if (nmi != mi) { Set(recipe, "Mode", modes[nmi] == WeaponMode.Normal ? "" : modes[nmi].ToString()); mode = modes[nmi]; }
        EditorGUILayout.LabelField(WeaponModeSpec.Get(mode).Help, EditorStyles.wordWrappedMiniLabel);

        // 換了模式後，表上還留著對新模式無效的值：列出來、一鍵清
        var stale = recipe.Keys.Where(k => k != "ID" && k != "Name" && k != "Mode" && !WeaponModeSpec.IsEffective(mode, k) && Get(recipe, k).Length > 0).ToList();
        if (stale.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox($"這些欄對 {WeaponModeSpec.ModeLabel(mode)} 無效、程式不會讀（存檔會留著、載入印 Warning）：{string.Join(", ", stale)}", MessageType.Warning);
            if (GUILayout.Button("清掉", GUILayout.Width(44), GUILayout.Height(38))) { foreach (var k in stale) recipe.Remove(k); GUI.changed = true; }
            EditorGUILayout.EndHorizontal();
        }

        string group = null;
        foreach (var f in WeaponModeSpec.EffectiveFields(mode, FieldTable.Recipe))
        {
            if (f.Name == "ID" || f.Name == "Name" || f.Name == "Mode") continue;
            if (f.Group != group) { group = f.Group; EditorGUILayout.Space(4); EditorGUILayout.LabelField(group, EditorStyles.miniBoldLabel); }
            DrawField(f, recipe, mode);
        }
        EditorGUILayout.EndVertical();
    }

    // ── 單一欄位 ──
    void DrawField(FieldSpec f, Dictionary<string, string> d, WeaponMode mode)
    {
        string label = WeaponModeSpec.Label(mode, f.Name);
        bool required = WeaponModeSpec.IsRequired(mode, f.Name);
        var content = new GUIContent((required ? "★ " : "") + label, f.Help);
        string cur = Get(d, f.Name);

        // 有參考表的欄位 → 下拉
        switch (f.Name)
        {
            case "GroundEffectID": DrawIdPopup(content, d, f.Name, _groundIds, _groundLabels); CheckAuraDuration(d, mode); return;
            case "FireEffectID": case "HitEffectID": case "TrailEffectID": case "SummonEffectID":
                DrawIdPopup(content, d, f.Name, _vfxIds, _vfxLabels); return;
            case "SubRecipeID": DrawRowPopup(content, d, f.Name, _recipes, RecipeLabel, "（無：分裂子彈繼承自身）"); return;
            case "SubWeaponOnHit": DrawRowPopup(content, d, f.Name, _weapons, WeaponLabel, "（無）"); return;
            case "BeamStyle": DrawIndexedPopup(content, d, f.Name, BeamStyleNames, 2); return;
            case "BeamColor": DrawIndexedPopup(content, d, f.Name, BeamColorNames, 9); return;
        }

        switch (f.Kind)
        {
            case FieldKind.Bool:
            {
                bool v = CsvFieldParse.Bool(cur, false);
                bool nv = EditorGUILayout.Toggle(content, v);
                if (nv != v) Set(d, f.Name, nv ? "1" : "");
                break;
            }
            case FieldKind.Enum:
            {
                var opts = new List<string> { $"（預設：{(string.IsNullOrEmpty(f.Default) ? "無" : f.Default)}）" };
                opts.AddRange(f.Options ?? new string[0]);
                int ci = string.IsNullOrEmpty(cur) ? 0 : Array.FindIndex(f.Options ?? new string[0], o => string.Equals(o, cur, StringComparison.OrdinalIgnoreCase)) + 1;
                if (ci < 0) ci = 0;
                int ni = EditorGUILayout.Popup(content, ci, opts.ToArray());
                if (ni != ci) Set(d, f.Name, ni == 0 ? "" : f.Options[ni - 1]);
                break;
            }
            default:
            {
                EditorGUILayout.BeginHorizontal();
                string nv = EditorGUILayout.TextField(content, cur);
                if (nv != cur) Set(d, f.Name, nv);
                string hint = RangeHint(f);
                if (hint.Length > 0)
                {
                    var st = new GUIStyle(EditorStyles.miniLabel); st.normal.textColor = DimColor;
                    EditorGUILayout.LabelField(hint, st, GUILayout.Width(150));
                }
                EditorGUILayout.EndHorizontal();
                string err = ValueProblem(f, Get(d, f.Name));
                if (err.Length > 0) Colored(err, ErrColor);
                break;
            }
        }
    }

    static string RangeHint(FieldSpec f)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(f.Default)) sb.Append("空=").Append(f.Default);
        if (f.Kind == FieldKind.Int || f.Kind == FieldKind.Float || f.Kind == FieldKind.Percent)
        {
            bool hasMin = !float.IsNegativeInfinity(f.Min), hasMax = !float.IsPositiveInfinity(f.Max);
            if (hasMin || hasMax)
            {
                if (sb.Length > 0) sb.Append("  ");
                sb.Append(hasMin ? f.Min.ToString("0.##") : "…").Append("~").Append(hasMax ? f.Max.ToString("0.##") : "…");
            }
        }
        return sb.ToString();
    }

    /// <summary>值格式／範圍檢查（空白永遠合法＝預設）。</summary>
    static string ValueProblem(FieldSpec f, string v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        switch (f.Kind)
        {
            case FieldKind.Int:
            case FieldKind.Float:
            {
                if (!float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)) return "不是數字";
                if (f.Kind == FieldKind.Int && Math.Abs(x - Math.Round(x)) > 0.0001f) return "要整數";
                if (x < f.Min) return $"小於下限 {f.Min:0.##}";
                if (x > f.Max) return $"大於上限 {f.Max:0.##}（超過通常是填錯位）";
                return "";
            }
            case FieldKind.Percent:
            {
                string s = v.EndsWith("%") ? v.Substring(0, v.Length - 1) : v;
                if (!float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)) return "百分比格式：30% 或 -20%";
                if (x < f.Min || x > f.Max) return $"要在 {f.Min:0.##}~{f.Max:0.##} 之間";
                return "";
            }
            case FieldKind.IntList:
                return v.Split('|').All(p => int.TryParse(p.Trim(), out _)) ? "" : "用 | 分隔的整數，例：2|3|5";
            default: return "";
        }
    }

    void DrawIdPopup(GUIContent content, Dictionary<string, string> d, string key, List<int> ids, List<string> labels)
    {
        int cur = CsvFieldParse.Int(Get(d, key), 0);
        int ci = ids.IndexOf(cur);
        var opts = labels.ToArray();
        if (ci < 0) { opts = opts.Concat(new[] { $"{cur}（表裡沒有這個 ID！）" }).ToArray(); ci = opts.Length - 1; }
        int ni = EditorGUILayout.Popup(content, ci, opts);
        if (ni != ci && ni < ids.Count) Set(d, key, ids[ni] == 0 ? "" : ids[ni].ToString());
    }

    void DrawRowPopup(GUIContent content, Dictionary<string, string> d, string key, List<Dictionary<string, string>> rows, Func<Dictionary<string, string>, string> label, string none)
    {
        int cur = CsvFieldParse.Int(Get(d, key), 0);
        var opts = new List<string> { none };
        opts.AddRange(rows.Select(label));
        int ci = cur <= 0 ? 0 : rows.FindIndex(r => IdOf(r) == cur) + 1;
        if (ci < 0 || (cur > 0 && ci == 0)) { opts.Add($"{cur}（表裡沒有這個 ID！）"); ci = opts.Count - 1; }
        int ni = EditorGUILayout.Popup(content, ci, opts.ToArray());
        if (ni != ci && ni <= rows.Count) Set(d, key, ni == 0 ? "" : IdOf(rows[ni - 1]).ToString());
    }

    static void DrawIndexedPopup(GUIContent content, Dictionary<string, string> d, string key, string[] names, int defaultIdx)
    {
        int cur = CsvFieldParse.Int(Get(d, key), 0);
        var opts = new List<string> { $"（預設：{defaultIdx} {names[defaultIdx - 1]}）" };
        for (int i = 0; i < names.Length; i++) opts.Add($"{i + 1} {names[i]}");
        int ci = (cur >= 1 && cur <= names.Length) ? cur : 0;
        int ni = EditorGUILayout.Popup(content, ci, opts.ToArray());
        if (ni != ci) Set(d, key, ni == 0 ? "" : ni.ToString());
    }

    static readonly string[] BeamStyleNames = { "鏡光", "標準", "脈衝", "離子", "電漿", "虛線", "閃電", "針狀", "洪流", "微光" };
    static readonly string[] BeamColorNames = { "紅", "橙", "黃", "綠", "青", "藍", "紫", "洋紅", "白", "琥珀金" };

    void CheckAuraDuration(Dictionary<string, string> recipe, WeaponMode mode)
    {
        if (mode != WeaponMode.Aura) return;
        int id = CsvFieldParse.Int(Get(recipe, "GroundEffectID"), 0);
        if (id > 0 && _groundDuration.TryGetValue(id, out var dur) && dur != "-1")
            Colored($"佛光的地面特效 Duration 必須是 -1（永久，由玩家按住管生死），這一列是 {dur}。", ErrColor);
    }

    static void Colored(string msg, Color c)
    {
        var st = new GUIStyle(EditorStyles.wordWrappedMiniLabel); st.normal.textColor = c;
        EditorGUILayout.LabelField(msg, st);
    }

    // ── 驗證區 ──
    void DrawValidation(Dictionary<string, string> weapon, Dictionary<string, string> recipe)
    {
        var problems = new List<string>();
        if (recipe != null)
        {
            var mode = ModeOf(recipe);
            problems.AddRange(WeaponModeSpec.Validate(mode, recipe, FieldTable.Recipe));
            problems.AddRange(WeaponModeSpec.Validate(mode, weapon, FieldTable.Weapon));
            foreach (var f in WeaponModeSpec.EffectiveFields(mode, FieldTable.Recipe))
            {
                string e = ValueProblem(f, Get(recipe, f.Name)); if (e.Length > 0) problems.Add($"[Error] 配方「{f.Name}」：{e}");
            }
            foreach (var f in WeaponModeSpec.EffectiveFields(mode, FieldTable.Weapon))
            {
                string e = ValueProblem(f, Get(weapon, f.Name)); if (e.Length > 0) problems.Add($"[Error] 武器「{f.Name}」：{e}");
            }
        }
        int wid = IdOf(weapon);
        if (!_itemWeaponIds.Contains(wid))
            problems.Add($"[Info] ItemTable 沒有指到武器 {wid} 的物品列（WeaponID={wid}）——玩家無法裝備它；模擬與存檔都不受影響，要上線時記得在 ItemTable 加一列。");

        if (problems.Count == 0) { EditorGUILayout.HelpBox("✓ 沒有問題。", MessageType.None); return; }
        foreach (var p in problems)
        {
            var type = p.StartsWith("[Error]") ? MessageType.Error : p.StartsWith("[Warning]") ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox(p.Substring(p.IndexOf(']') + 1).Trim(), type);
        }
    }

    // ───────────────────────── Play 模擬 ─────────────────────────

    void RequestApply()
    {
        _applyRequested = true;
        _applyAt = EditorApplication.timeSinceStartup + 0.25;   // 打字中不要每個字都重建一把武器
    }

    void ApplySimulation()
    {
        if (!EditorApplication.isPlaying) return;
        if (_sel < 0 || _sel >= _weapons.Count) return;
        var wm = FindObjectOfType<WeaponManager>();
        var rm = FindObjectOfType<RecipeManager>();
        if (wm == null || rm == null) { SetSim("場景裡找不到 WeaponManager / RecipeManager（還沒進地圖？）", true); return; }

        var weapon = _weapons[_sel];
        var recipeDict = RecipeOf(weapon);
        if (recipeDict == null) { SetSim("這把武器的配方不存在，無法模擬。", true); return; }

        try
        {
            var problems = new List<string>();
            var recipe = rm.CreateTransient(recipeDict, problems);
            var w = wm.CreateTransient(weapon, recipe, problems);
            wm.SimulationOverride = w;
            _simActive = true;
            var errs = problems.Where(p => p.StartsWith("[Error]")).ToList();
            SetSim($"模擬中：{Get(weapon, "Name")}（{WeaponModeSpec.ModeLabel(recipe.Mode)}）" + (errs.Count > 0 ? "  ⚠ " + string.Join("；", errs) : "  ← 按左鍵／空白鍵射出去"), errs.Count > 0);
        }
        catch (Exception e)
        {
            SetSim("套用失敗：" + e.Message, true);
            Debug.LogException(e);
        }
    }

    void ClearSimulation()
    {
        var wm = FindObjectOfType<WeaponManager>();
        if (wm != null) wm.SimulationOverride = null;
        _simActive = false;
        SetSim("已解除模擬，回到背包裝備的武器。", false);
    }

    void SetSim(string msg, bool error) { _simStatus = msg; _simError = error; }

    // ── 珠子：與作弊面板「鑲嵌」分頁同一套（真鑲，作者自己去 Y 鍵鍛造）──
    void GiveGems()
    {
        var inv = InventorySystem.Instance;
        if (inv == null) { SetSim("找不到背包系統。", true); return; }
        int given = 0, full = 0;
        foreach (var kv in inv.Db.Items)
        {
            if (kv.Value == null || !kv.Value.IsGem) continue;
            var st = ItemManager.CreateGem(kv.Key, 3);
            if (st.IsEmpty) continue;
            if (inv.AddStack(st) > 0) full++; else given++;
        }
        SetSim(full > 0 ? $"給了 {given} 顆能力珠（Lv3），{full} 顆因背包已滿放不下。" : $"給了 {given} 顆能力珠（Lv3）。按 Y 開鍛造去鑲。", full > 0);
    }

    void RerollSockets(int n)
    {
        var inv = InventorySystem.Instance;
        if (inv == null) { SetSim("找不到背包系統。", true); return; }
        var st = inv.GetEquippedStack(EquipSlot.Weapon);
        if (st.IsEmpty) { SetSim("武器欄是空的：先用 L 作弊面板「取得所有武器」並裝備一把（模擬武器本身不占武器欄，珠子要鑲在裝備上）。", true); return; }
        n = Mathf.Clamp(n, 0, ItemInstance.SocketMax);
        if (st.Inst != null && st.Inst.HasSockets)
            for (int i = 0; i < st.Inst.sockets.Count; i++)
            {
                var g = st.Inst.TakeGem(i);
                if (g != null) inv.AddStack(ItemManager.FromGemRef(g));
            }
        st.Inst = ItemInstance.FromSocketLayout(Dipan.Rules.RandomRules.LayoutFor(n));
        inv.SetEquippedStack(EquipSlot.Weapon, st);
        var d = inv.GetData(st.ItemId);
        SetSim($"「{(d != null ? d.Name : st.ItemId.ToString())}」已改成 {n} 孔。按 Y 開鍛造去鑲。", false);
    }
}
