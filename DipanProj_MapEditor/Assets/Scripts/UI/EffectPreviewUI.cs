using System.Collections.Generic;
using UnityEngine;
using DipanMapEditor.Core;

namespace DipanMapEditor.UI
{
    /// <summary>特效預覽器面板（IMGUI）：左側效果清單、右側大視窗輪播。
    /// 可選顏色、調 FPS（預設 15）、播放/暫停、暗黑風換色，以及把換色版本匯出成 PNG。
    /// 純資料/繪製類（非 MonoBehaviour），由 EditorUI 在 CurrentTool==EffectPreview 時建立並 Draw。</summary>
    public class EffectPreviewUI
    {
        const float ListW = 300f;

        Vector2 _listScroll;
        string _search = "";

        EffectLibrary.Entry _sel;
        int _colorIdx;
        EffectRecolor.Mode _recolor = EffectRecolor.Mode.None;

        int _fps = 15;
        string _fpsBuf = "15";
        bool _playing = true;
        float _clock;                 // 累積「幀進度」= 秒 × fps
        float _lastRepaint;
        string _status = "";

        static readonly Dictionary<string, Color> ColorSwatch = new Dictionary<string, Color>
        {
            { "blue",   new Color(0.35f, 0.55f, 1f) },
            { "green",  new Color(0.40f, 0.85f, 0.40f) },
            { "orange", new Color(1f, 0.60f, 0.20f) },
            { "red",    new Color(1f, 0.35f, 0.35f) },
            { "violet", new Color(0.70f, 0.45f, 1f) },
            { "yellow", new Color(1f, 0.90f, 0.30f) },
        };

        public EffectPreviewUI() { _lastRepaint = Time.realtimeSinceStartup; }

        public void Draw(Rect area)
        {
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.BeginHorizontal();

            DrawList();
            DrawStage();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ---- 左側：效果清單（依類別分組 + 搜尋）----
        void DrawList()
        {
            GUILayout.BeginVertical(GUILayout.Width(ListW));

            GUILayout.Label("特效清單（整理自 StreamingAssets/Effects）");
            var entries = EffectLibrary.Entries;
            GUILayout.BeginHorizontal();
            GUILayout.Label("搜尋", GUILayout.Width(34));
            _search = GUILayout.TextField(_search ?? "");
            if (GUILayout.Button("清", GUILayout.Width(30))) _search = "";
            GUILayout.EndHorizontal();

            if (entries.Count == 0)
            {
                GUILayout.Space(6);
                GUILayout.Label("（找不到特效）\n請先執行 Tools/organize_effects.py\n把 Gigapack 整理進 StreamingAssets/Effects。");
                GUILayout.EndVertical();
                return;
            }

            string filter = (_search ?? "").Trim().ToLowerInvariant();
            _listScroll = GUILayout.BeginScrollView(_listScroll);

            string curCat = null;
            int shown = 0;
            foreach (var e in entries)
            {
                if (filter.Length > 0 && e.name.ToLowerInvariant().IndexOf(filter) < 0) continue;
                if (e.category != curCat)
                {
                    curCat = e.category;
                    GUILayout.Space(4);
                    GUI.color = new Color(0.7f, 0.9f, 1f);
                    GUILayout.Label("— " + curCat + " —");
                    GUI.color = Color.white;
                }
                GUI.color = (e == _sel) ? Color.cyan : Color.white;
                if (GUILayout.Button(e.name)) Select(e);
                GUI.color = Color.white;
                shown++;
            }
            if (shown == 0) GUILayout.Label("（沒有符合搜尋的特效）");

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        void Select(EffectLibrary.Entry e)
        {
            _sel = e;
            _colorIdx = 0;
            _clock = 0;
            _status = "";
        }

        // ---- 右側：預覽視窗 + 控制項 ----
        void DrawStage()
        {
            GUILayout.BeginVertical();

            if (_sel == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("← 從左側選一個特效開始預覽");
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            var color = _sel.colors[Mathf.Clamp(_colorIdx, 0, _sel.colors.Count - 1)];
            int total = color.frames.Count;

            // 依實際時間推進動畫（只在 Repaint 累計，避免多次 OnGUI 重複加）
            if (Event.current.type == EventType.Repaint)
            {
                float now = Time.realtimeSinceStartup;
                float dt = now - _lastRepaint;
                _lastRepaint = now;
                if (_playing && dt > 0f && dt < 0.25f) _clock += dt * Mathf.Max(1, _fps);
            }
            int frame = (total > 0) ? ((int)_clock % total) : 0;

            // 標題 / 資訊
            GUILayout.Label($"{_sel.category} / {_sel.name}　顏色：{color.name}　幀：{frame + 1}/{total}");

            // 控制列
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_playing ? "暫停" : "播放", GUILayout.Width(70))) _playing = !_playing;
            if (GUILayout.Button("重播", GUILayout.Width(60))) _clock = 0;
            GUILayout.Space(10);
            GUILayout.Label("FPS", GUILayout.Width(30));
            if (GUILayout.Button("－", GUILayout.Width(24))) SetFps(_fps - 1);
            _fpsBuf = GUILayout.TextField(_fpsBuf, GUILayout.Width(40));
            if (GUILayout.Button("＋", GUILayout.Width(24))) SetFps(_fps + 1);
            if (int.TryParse(_fpsBuf, out int typed) && typed != _fps) SetFps(typed);
            if (GUILayout.Button("15", GUILayout.Width(34))) SetFps(15);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // 顏色選擇
            GUILayout.BeginHorizontal();
            GUILayout.Label("顏色", GUILayout.Width(34));
            for (int i = 0; i < _sel.colors.Count; i++)
            {
                var cs = _sel.colors[i];
                GUI.color = (i == _colorIdx) ? Color.white
                          : (ColorSwatch.TryGetValue(cs.name, out var sw) ? sw : Color.gray);
                if (GUILayout.Button(cs.name, GUILayout.Width(64))) { _colorIdx = i; _clock = 0; }
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            // 換色（暗黑風）
            GUILayout.BeginHorizontal();
            GUILayout.Label("套色", GUILayout.Width(34));
            foreach (var m in EffectRecolor.AllModes)
            {
                GUI.color = (m == _recolor) ? Color.cyan : Color.white;
                if (GUILayout.Button(EffectRecolor.DisplayName(m), GUILayout.Width(84))) _recolor = m;
            }
            GUI.color = Color.white;
            if (GUILayout.Button("匯出此換色版", GUILayout.Width(110))) ExportCurrent(color);
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);

            // 預覽舞台（暗底，貼合遊戲的暗場氛圍）
            Rect stage = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawStageBackground(stage);

            var tex = (total > 0) ? EffectRecolor.GetRecolored(color.frames[frame], _recolor) : null;
            if (tex != null)
            {
                // 依貼圖比例置中、放大到幾乎填滿舞台（像素風用 Point，已在載入時設好）
                float pad = 24f;
                float availW = stage.width - pad * 2, availH = stage.height - pad * 2;
                float scale = Mathf.Min(availW / tex.width, availH / tex.height);
                scale = Mathf.Max(scale, 0.01f);
                float w = tex.width * scale, h = tex.height * scale;
                var dst = new Rect(stage.x + (stage.width - w) / 2f, stage.y + (stage.height - h) / 2f, w, h);
                GUI.DrawTexture(dst, tex, ScaleMode.StretchToFill, true);
            }

            GUILayout.EndVertical();
        }

        void SetFps(int v)
        {
            _fps = Mathf.Clamp(v, 1, 60);
            _fpsBuf = _fps.ToString();
        }

        static Texture2D _stageTex;
        static void DrawStageBackground(Rect r)
        {
            if (_stageTex == null)
            {
                _stageTex = new Texture2D(1, 1);
                _stageTex.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.10f, 1f)); // 近黑，模擬暗場
                _stageTex.Apply();
            }
            GUI.DrawTexture(r, _stageTex, ScaleMode.StretchToFill);
        }

        void ExportCurrent(EffectLibrary.ColorSet color)
        {
            int n = EffectRecolor.ExportColorSet(_sel, color, _recolor, out string outDir);
            if (n > 0)
                _status = $"已匯出 {n} 幀 → StreamingAssets/Effects_Recolored/…/{System.IO.Path.GetFileName(outDir)}";
            else
                _status = "匯出失敗（沒有可寫出的幀）";
        }
    }
}
