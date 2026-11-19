using System.Collections.Generic;
using UnityEngine;
using Dipan.Vfx;
using DipanMapEditor.Preview;

namespace DipanMapEditor.UI
{
    /// <summary>
    /// 特效預覽器的「自製特效」分頁（IMGUI，2026-11-18，見 readme/DIPAN_VFX.md §6）。
    ///
    /// 左：DipanVfx 的三張表（地刺／火焰／地上燃燒）每一列。中：用遊戲同一份程式與 shader 即時播放（<see cref="CustomVfxPreviewRig"/>）。
    /// 右：選到那一列的每一欄都能直接改，**改了立刻套用重播**；滿意按「存回 CSV」寫回 DipanProj_Main/Assets/Data，
    /// 遊戲那邊 Unity 回到前景就會重新匯入。之後做的 shader 特效都接到這裡（加一種＝在 <see cref="Tables"/> 加一項）。
    /// </summary>
    public class CustomVfxPanel
    {
        const float ListW = 260f;
        const float ParamW = 380f;

        sealed class TableDef
        {
            public string Title, Table;
            public CustomVfxPreviewRig.Kind Kind;
            public System.Action<string> Apply;    // 把整份 CSV 文字餵給套件的 Database
            public EditableCsv Csv;
        }

        static readonly TableDef[] Tables =
        {
            new TableDef { Title = "地刺", Table = "GroundSpikeTable", Kind = CustomVfxPreviewRig.Kind.Spike, Apply = GroundSpikeDatabase.LoadFromText },
            new TableDef { Title = "火焰", Table = FlameDatabase.TableName, Kind = CustomVfxPreviewRig.Kind.Flame, Apply = FlameDatabase.LoadFromText },
            new TableDef { Title = "地上燃燒", Table = GroundFireDatabase.TableName, Kind = CustomVfxPreviewRig.Kind.GroundFire, Apply = GroundFireDatabase.LoadFromText },
            new TableDef { Title = "爆炸", Table = ExplosionDatabase.TableName, Kind = CustomVfxPreviewRig.Kind.Explosion, Apply = ExplosionDatabase.LoadFromText },
            new TableDef { Title = "傳送點", Table = TeleportFxDatabase.TableName, Kind = CustomVfxPreviewRig.Kind.Teleport, Apply = TeleportFxDatabase.LoadFromText },
            new TableDef { Title = "光束", Table = BeamFxDatabase.TableName, Kind = CustomVfxPreviewRig.Kind.Beam, Apply = BeamFxDatabase.LoadFromText },
            new TableDef { Title = "靈獸", Table = SpiritFxDatabase.TableName, Kind = CustomVfxPreviewRig.Kind.Spirit, Apply = SpiritFxDatabase.LoadFromText },
        };

        bool _loaded;
        TableDef _selTable;
        int _selId;
        Vector2 _listScroll, _paramScroll;
        string _status = "";
        readonly Dictionary<string, string> _numBuf = new Dictionary<string, string>();

        GUIStyle _small, _section;

        void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            ReloadAll(clearSprites: false);
        }

        void ReloadAll(bool clearSprites)
        {
            CustomVfxSource.Install();
            if (clearSprites) CustomVfxSource.ClearSpriteCache();
            // 火焰要先載：地上燃燒的火舌指向 FlameTable
            foreach (var t in new[] { Tables[1], Tables[2], Tables[0], Tables[3], Tables[4], Tables[5], Tables[6] })
            {
                t.Csv = EditableCsv.Load(t.Table);
                if (t.Csv.Error == null) t.Apply(t.Csv.BuildText());
            }
            CustomVfxPreviewRig.Instance.Restart();
        }

        public void Draw(Rect area)
        {
            EnsureLoaded();
            if (_small == null)
            {
                _small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
                _small.normal.textColor = new Color(0.7f, 0.7f, 0.72f);
                _section = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                _section.normal.textColor = new Color(0.7f, 0.9f, 1f);
            }
            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();
            DrawList();
            DrawStage();
            DrawParams();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ───────────────────────── 左：清單 ─────────────────────────

        void DrawList()
        {
            GUILayout.BeginVertical(GUILayout.Width(ListW));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重讀表格與圖", GUILayout.Width(110)))
            {
                ReloadAll(clearSprites: true);
                _status = "已重讀三張表與所有圖（未存的修改已放棄）";
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("表格在 DipanProj_Main/Assets/Data，改這裡＝改遊戲", _small);

            _listScroll = GUILayout.BeginScrollView(_listScroll);
            foreach (var t in Tables)
            {
                GUILayout.Space(6);
                GUILayout.Label($"— {t.Title}（{t.Table}）{(t.Csv != null && t.Csv.Dirty ? " *未存" : "")} —", _section);
                if (t.Csv == null || t.Csv.Error != null) { GUILayout.Label(t.Csv?.Error ?? "沒載入", _small); continue; }
                int nameCol = t.Csv.ColumnIndex("Name");
                foreach (var r in t.Csv.Rows)
                {
                    if (r.Id <= 0) continue;
                    bool sel = _selTable == t && _selId == r.Id;
                    GUI.color = sel ? Color.cyan : Color.white;
                    if (GUILayout.Button($"{r.Id}  {t.Csv.Get(r, nameCol)}{(r.Edited ? " *" : "")}"))
                    {
                        _selTable = t;
                        _selId = r.Id;
                        _numBuf.Clear();
                        CustomVfxPreviewRig.Instance.Select(t.Kind, r.Id);
                    }
                    GUI.color = Color.white;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        // ───────────────────────── 中：舞台 ─────────────────────────

        void DrawStage()
        {
            var rig = CustomVfxPreviewRig.Instance;
            GUILayout.BeginVertical();
            if (_selTable == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("← 從左側選一個特效開始預覽");
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重播", GUILayout.Width(50))) rig.Restart();
            GUILayout.Label("鏡頭", GUILayout.Width(30));
            if (GUILayout.Button("近", GUILayout.Width(30))) rig.ViewSize = 2f;
            if (GUILayout.Button("中", GUILayout.Width(30))) rig.ViewSize = 3f;
            if (GUILayout.Button("遊戲", GUILayout.Width(40))) rig.ViewSize = 5f;
            rig.BrightFloor = GUILayout.Toggle(rig.BrightFloor, "亮地板", GUILayout.Width(64));
            rig.ShowFigure = GUILayout.Toggle(rig.ShowFigure, "參考人形", GUILayout.Width(76));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            switch (_selTable.Kind)
            {
                case CustomVfxPreviewRig.Kind.Flame:
                    foreach (var m in new[] { CustomVfxPreviewRig.FlameMotion.Still, CustomVfxPreviewRig.FlameMotion.Orbit, CustomVfxPreviewRig.FlameMotion.Fall })
                    {
                        string label = m == CustomVfxPreviewRig.FlameMotion.Still ? "靜止" : m == CustomVfxPreviewRig.FlameMotion.Orbit ? "環繞" : "墜落";
                        GUI.color = rig.Motion == m ? Color.cyan : Color.white;
                        if (GUILayout.Button(label, GUILayout.Width(50)) && rig.Motion != m) { rig.Motion = m; rig.Restart(); }
                    }
                    GUI.color = Color.white;
                    if (rig.Motion == CustomVfxPreviewRig.FlameMotion.Orbit)
                    {
                        rig.OrbitRadius = NumField("半徑", "orbitR", rig.OrbitRadius);
                        rig.OrbitSpeed = NumField("速度", "orbitS", rig.OrbitSpeed);
                        rig.OrbitCount = Mathf.RoundToInt(NumField("顆數", "orbitN", rig.OrbitCount));
                    }
                    break;
                case CustomVfxPreviewRig.Kind.GroundFire:
                    rig.FireRadius = NumField("範圍半徑", "fireR", rig.FireRadius);
                    rig.FireDuration = NumField("燒幾秒", "fireD", rig.FireDuration);
                    GUILayout.Label("（GroundEffectTable 的 Radius／Duration；白圈＝傷害範圍）", _small);
                    break;
                case CustomVfxPreviewRig.Kind.Explosion:
                    rig.BlastRadius = NumField("範圍半徑", "blR", rig.BlastRadius);
                    GUILayout.Label("（拋物線＝AreaRadius×BulletScale；火焰拋擲彈是 5；白圈＝傷害範圍）", _small);
                    break;
                case CustomVfxPreviewRig.Kind.Beam:
                    foreach (var m in new[] { CustomVfxPreviewRig.BeamMotion.Sweep, CustomVfxPreviewRig.BeamMotion.Bounce, CustomVfxPreviewRig.BeamMotion.Chain, CustomVfxPreviewRig.BeamMotion.Refire })
                    {
                        string label = m == CustomVfxPreviewRig.BeamMotion.Sweep ? "掃動" : m == CustomVfxPreviewRig.BeamMotion.Bounce ? "反彈"
                                     : m == CustomVfxPreviewRig.BeamMotion.Chain ? "連鎖" : "重複發射";
                        GUI.color = rig.BeamMode == m ? Color.cyan : Color.white;
                        if (GUILayout.Button(label, GUILayout.Width(m == CustomVfxPreviewRig.BeamMotion.Refire ? 70 : 50)) && rig.BeamMode != m) { rig.BeamMode = m; rig.Restart(); }
                    }
                    GUI.color = Color.white;
                    rig.BeamWidth = NumField("粗細", "bmW", rig.BeamWidth);
                    GUILayout.Label("（武器表 BeamWidth：5＝1、29＝0.38、9＝0.25、66＝0.3、78＝0.25）", _small);
                    break;
                case CustomVfxPreviewRig.Kind.Spirit:
                    foreach (var m in new[] { CustomVfxPreviewRig.SpiritMotion.Wander, CustomVfxPreviewRig.SpiritMotion.Chase })
                    {
                        string label = m == CustomVfxPreviewRig.SpiritMotion.Wander ? "亂游" : "追人形";
                        GUI.color = rig.SpiritMode == m ? Color.cyan : Color.white;
                        if (GUILayout.Button(label, GUILayout.Width(60))) rig.SpiritMode = m;
                    }
                    GUI.color = Color.white;
                    float sp = NumField("游速", "sbS", rig.SpiritSpeed);
                    float sc = NumField("大小", "sbC", rig.SpiritScale);
                    if (!Mathf.Approximately(sc, rig.SpiritScale)) { rig.SpiritScale = sc; rig.Restart(); }
                    rig.SpiritSpeed = sp;
                    GUILayout.Label("（配方 Speed／武器 BulletScale；身長約 7.5＋頭，鏡頭切「遊戲」才看得到整條）", _small);
                    break;
                case CustomVfxPreviewRig.Kind.Spike:
                    rig.SpikeSpeed = NumField("子彈速度", "spS", rig.SpikeSpeed);
                    rig.SpikeStep = NumField("TrailStep", "spT", rig.SpikeStep);
                    break;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status, _small);

            Rect stage = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            CustomVfxPreviewRig.LastDrawFrame = Time.frameCount;
            if (Event.current.type == EventType.Repaint)
            {
                rig.EnsureTexture(Mathf.RoundToInt(stage.width), Mathf.RoundToInt(stage.height));
                if (rig.Texture != null) GUI.DrawTexture(stage, rig.Texture, ScaleMode.ScaleToFit, false);
            }
            GUILayout.Label("格線＝1 世界單位；參考人形高約 1.95（遊戲角色的可見高度）。遊戲裡的火舌會跟角色一起前後排序，這裡沒有角色。", _small);
            GUILayout.EndVertical();
        }

        float NumField(string label, string key, float value)
        {
            GUILayout.Label(label, GUILayout.ExpandWidth(false));
            if (!_numBuf.TryGetValue(key, out var buf)) buf = value.ToString("0.##");
            buf = GUILayout.TextField(buf, GUILayout.Width(44));
            _numBuf[key] = buf;
            return float.TryParse(buf, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : value;
        }

        // ───────────────────────── 右：參數 ─────────────────────────

        void DrawParams()
        {
            GUILayout.BeginVertical(GUILayout.Width(ParamW));
            if (_selTable == null || _selTable.Csv == null) { GUILayout.EndVertical(); return; }
            var csv = _selTable.Csv;
            var row = csv.FindRow(_selId);
            if (row == null) { GUILayout.Label("（這一列不見了，按「重讀表格與圖」）", _small); GUILayout.EndVertical(); return; }

            GUILayout.Label($"{_selTable.Table}  ID {_selId}", _section);
            GUILayout.Label("改了立刻重播；空白＝預設值（說明裡的「空=」）。", _small);

            GUILayout.BeginHorizontal();
            GUI.enabled = csv.Dirty;
            if (GUILayout.Button("存回 CSV", GUILayout.Width(90)))
            {
                csv.Save(out _status);
                _selTable.Apply(csv.BuildText());
                CustomVfxPreviewRig.Instance.Restart();
            }
            if (GUILayout.Button("放棄修改", GUILayout.Width(80)))
            {
                csv.Reload();
                _selTable.Apply(csv.BuildText());
                if (_selTable.Kind == CustomVfxPreviewRig.Kind.Flame) Tables[2].Apply(Tables[2].Csv.BuildText());   // 地上燃燒抓的是火焰資料
                CustomVfxPreviewRig.Instance.Restart();
                _status = $"已放棄 {_selTable.Table} 的修改";
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            _paramScroll = GUILayout.BeginScrollView(_paramScroll);
            for (int c = 1; c < csv.Columns.Count; c++)
            {
                string col = csv.Columns[c];
                if (col.StartsWith("__col")) continue;
                SplitHeader(csv.HeaderCells[c], out string title, out string help);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{col}  {title}", GUILayout.Width(ParamW - 150));
                string cur = csv.Get(row, c);
                bool wide = col == "Sprites" || col == "Note" || col.EndsWith("Sprite");
                string next = GUILayout.TextField(cur, GUILayout.Width(wide ? 130 : 120));
                GUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(help)) GUILayout.Label(help, _small);
                if (next != cur)
                {
                    csv.Set(row, c, next);
                    _selTable.Apply(csv.BuildText());
                    if (_selTable.Kind == CustomVfxPreviewRig.Kind.Flame) Tables[2].Apply(Tables[2].Csv.BuildText());
                    CustomVfxPreviewRig.Instance.Restart();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        /// <summary>表頭「Size(火的高度/世界單位；空=1)」→ 標題「火的高度」、說明「世界單位；空=1」。</summary>
        static void SplitHeader(string cell, out string title, out string help)
        {
            title = ""; help = "";
            if (string.IsNullOrEmpty(cell)) return;
            int open = cell.IndexOf('(');
            if (open < 0) open = cell.IndexOf('（');
            if (open < 0) return;
            string inner = cell.Substring(open + 1).TrimEnd(')', '）');
            int slash = inner.IndexOf('/');
            title = slash >= 0 ? inner.Substring(0, slash) : inner;
            help = slash >= 0 ? inner.Substring(slash + 1) : "";
        }
    }
}
