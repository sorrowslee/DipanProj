using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;

namespace Dipan.Diagnostics
{
    /// <summary>
    /// 碰撞可視化疊層（除錯用）：把**實際生成的碰撞形狀**畫在畫面上。按 <b>P</b> 開效能面板後按 <b>C</b> 開關。
    ///
    /// <para><b>為什麼畫「實際碰撞」而不是畫「佔位遮罩」</b>：遮罩只是輸入，中間還隔著降取樣、
    /// 實心判定門檻、run 合併、畫布邊界夾取、物件的縮放與翻轉。畫遮罩只能證明遮罩對，
    /// 畫實際 Collider 才能回答「我到底為什麼走不過去」。</para>
    ///
    /// <para><b>顏色</b>：綠＝地上物（家具/屏風…）、紅＝牆（可走層塗的）、藍＝水/坑（只擋腳）、
    /// 黃＝玩家、橘＝怪物。<b>綠色與紅色是兩套獨立的系統</b>——地上物的擋路範圍不在可走層上，
    /// 在可走層塗色改不動綠色的部分（見 readme/PROBLEMS.md B9）。</para>
    ///
    /// <para><b>怎麼疊在後處理之上</b>：自己開一台相機（cullingMask=0，什麼都不畫），
    /// depth 排在主相機與互動星星相機之後，在它的 <c>OnPostRender</c> 用 GL 即時模式畫。
    /// 這樣暗場景/幽暗氛圍不會把它壓暗（同 <see cref="OverlayCameraController"/> 的思路）。
    /// 關閉時相機直接 disable，<c>OnPostRender</c> 不會被呼叫 ⇒ <b>零成本</b>。</para>
    ///
    /// <para>只畫「畫面內」的碰撞：用 <c>Physics2D.OverlapArea</c> 取可視範圍，邪佛廣場那種
    /// 288 個地上物的圖也不會因為開了疊層就卡。</para>
    ///
    /// 自動生成、跨場景常駐、零接線（同 PerfHud / OverlayCameraController 模式）。
    /// </summary>
    [DisallowMultipleComponent]
    public class CollisionDebugOverlay : MonoBehaviour
    {
        /// <summary>是否顯示。由 PerfHud 切換（面板開著時按 C，或點面板上的按鈕）。</summary>
        public static bool Enabled { get; private set; }

        public static void Toggle() { Enabled = !Enabled; }

        static CollisionDebugOverlay _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (_instance != null) return;
            var go = new GameObject("[CollisionDebugOverlay]");
            _instance = go.AddComponent<CollisionDebugOverlay>();
            DontDestroyOnLoad(go);
        }

        // ---- 顏色（填色較淡、外框較亮）----
        static readonly Color ObjectColor  = new Color(0.25f, 0.95f, 0.45f);   // 地上物
        static readonly Color WallColor    = new Color(0.95f, 0.30f, 0.25f);   // 牆（可走層 '1'）
        static readonly Color BlockerColor = new Color(0.35f, 0.60f, 1.00f);   // 水/坑（可走層 '2'）
        static readonly Color PlayerColor  = new Color(1.00f, 0.85f, 0.20f);   // 玩家
        static readonly Color EnemyColor   = new Color(1.00f, 0.55f, 0.15f);   // 怪物
        static readonly Color TeleportColor = new Color(0.30f, 1.00f, 0.55f);   // 傳送點踩踏矩形
        const float FillAlpha = 0.25f;
        const float LineAlpha = 0.95f;

        Camera _cam;
        Material _mat;
        readonly List<Collider2D> _hits = new List<Collider2D>(512);
        readonly List<Vector2> _path = new List<Vector2>(256);
        readonly List<BoxCollider2D> _boxBuf = new List<BoxCollider2D>(64);
        ContactFilter2D _filter;

        // Layer 索引快取（每個 collider 都查一次 NameToLayer 是字串比對，很浪費）
        int _layerPlayer, _layerEnemy, _layerAlly, _layerWater;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            _cam = gameObject.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.Depth;   // 只疊上去，不清畫面
            _cam.cullingMask = 0;                       // 什麼都不畫，純粹為了拿 OnPostRender
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.useOcclusionCulling = false;
            _cam.enabled = false;
            gameObject.tag = "Untagged";                // 不搶 MainCamera

            // GL 即時模式用的內建純色材質。ZTest Always：我們是最後才畫的疊層，永遠要看得到。
            // （UI 是 Screen Space Overlay，永遠在所有相機之後畫，所以不會被這層蓋住。）
            var sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null)
            {
                Debug.LogWarning("[CollisionDebugOverlay] 找不到 Hidden/Internal-Colored，疊層停用。");
                enabled = false;
                return;
            }
            _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _mat.SetInt("_ZWrite", 0);
            _mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

            _layerPlayer = LayerMask.NameToLayer("Player");
            _layerEnemy  = LayerMask.NameToLayer("Enemy");
            _layerAlly   = LayerMask.NameToLayer("Ally");
            _layerWater  = LayerMask.NameToLayer("Water");

            // 只看會擋路/會被打的那幾層，其餘（互動觸發、子彈…）不畫，免得整片花掉。
            // useTriggers 必須開：怪物的碰撞框全是 isTrigger（見 readme/MONSTER_SETUP.md）。
            int mask = 0;
            foreach (var n in new[] { "Environment", "Water", "Player", "Enemy", "Ally" })
            {
                int idx = LayerMask.NameToLayer(n);
                if (idx >= 0) mask |= 1 << idx;
            }
            _filter = new ContactFilter2D { useTriggers = true };
            _filter.SetLayerMask(mask);
        }

        void OnDestroy()
        {
            if (_mat != null)
            {
                if (Application.isPlaying) Destroy(_mat); else DestroyImmediate(_mat);
            }
            if (_instance == this) _instance = null;
        }

        void LateUpdate()
        {
            var main = Camera.main;
            if (main == null || _cam == null) { if (_cam != null) _cam.enabled = false; return; }

            _cam.enabled = Enabled;                     // 關閉時完全不進渲染流程
            if (!Enabled) return;

            transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            _cam.orthographic = main.orthographic;
            _cam.orthographicSize = main.orthographicSize;
            _cam.nearClipPlane = main.nearClipPlane;
            _cam.farClipPlane = main.farClipPlane;
            _cam.rect = main.rect;
            _cam.depth = main.depth + 2;                // 疊在主相機(後處理)與互動星星相機(+1)之上
        }

        void OnPostRender()
        {
            if (!Enabled || _mat == null || _cam == null) return;

            // 可視範圍（稍微外擴，避免邊緣物件半截不見）
            float halfH = _cam.orthographicSize + 1f;
            float halfW = halfH * _cam.aspect + 1f;
            Vector2 c = transform.position;
            Vector2 min = new Vector2(c.x - halfW, c.y - halfH);
            Vector2 max = new Vector2(c.x + halfW, c.y + halfH);

            _hits.Clear();
            Physics2D.OverlapArea(min, max, _filter, _hits);

            _mat.SetPass(0);
            GL.PushMatrix();

            for (int i = 0; i < _hits.Count; i++)
            {
                var col = _hits[i];
                if (col == null || !col.enabled) continue;
                DrawCollider(col, min, max);
            }

            DrawTeleportZones();

            GL.PopMatrix();
        }

        /// <summary>
        /// 傳送點的**踩踏矩形**（青綠）＋錨點十字。
        ///
        /// <para>沒有它就沒辦法回答「我明明站在光盤上為什麼不傳送」——傳送點自從改成
        /// <see cref="TeleportAnchor"/> 的點模式後，觸發區是一個**遊戲裡完全看不見的矩形**，
        /// 判定點是<b>黃色碰撞圓的圓心</b>（＝<c>transform.position</c>，物理與可走層都用它），
        /// 不是黃色十字標的腳底——兩者差將近一整格。把三者一起畫出來，
        /// 「圓心有沒有在青綠框裡」一眼就有答案。</para>
        ///
        /// <para>被觸發鏈停用中的傳送點畫成虛淡色——那種「框對了卻不會傳」是另一種原因。</para>
        /// 舊的格子模式傳送點不畫（它的觸發區＝畫得出來的格子，本來就看得到）。
        /// </summary>
        void DrawTeleportZones()
        {
            var mgr = MapManager.Instance;
            var map = mgr != null && mgr.mapLoader != null ? mgr.mapLoader.Map : null;
            var regions = map?.TriggerLayer?.regions;
            if (regions == null) return;

            string typeId = mgr.mapLoader.teleportTypeId;
            for (int i = 0; i < regions.Count; i++)
            {
                var r = regions[i];
                if (r == null || r.typeId != typeId) continue;
                if (!TeleportAnchor.TryTouchRect(r, out Rect rect)) continue;

                bool active = TriggerChain.IsActive(r);
                Color tint = active ? TeleportColor : new Color(TeleportColor.r, TeleportColor.g, TeleportColor.b, 0.35f);
                FillRectWorld(rect, new Color(tint.r, tint.g, tint.b, active ? FillAlpha : FillAlpha * 0.4f));
                OutlineRect(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMax), tint);
                DrawCross(rect.center, 0.18f, new Color(tint.r, tint.g, tint.b, LineAlpha));
            }
        }

        /// <summary>
        /// 傳送點診斷讀數：玩家腳底座標、最近的傳送點踩踏矩形、以及「腳底在不在裡面」。
        /// 畫面上有框還不夠——框對了卻不觸發時，得看得到「判定當下用的是哪個座標」。
        /// </summary>
        void OnGUI()
        {
            if (!Enabled) return;
            var mgr = MapManager.Instance;
            var map = mgr != null && mgr.mapLoader != null ? mgr.mapLoader.Map : null;
            var regions = map?.TriggerLayer?.regions;
            if (regions == null) return;

            var pgo = GameObject.FindGameObjectWithTag("Player");
            var pc = pgo != null ? pgo.GetComponent<PlayerController>() : null;
            // 傳送點的判定點＝ transform.position（＝碰撞圓心，物理與可走層都用它）。
            // 腳底只是參考：兩者差將近一整格，那個落差正是「牆邊的東西看起來碰到了卻沒反應」的來源。
            string feetTxt = pgo != null
                ? $"判定點 ({pgo.transform.position.x:F2}, {pgo.transform.position.y:F2})"
                  + (pc != null ? $"　腳底 ({pc.FeetWorldPos.x:F2}, {pc.FeetWorldPos.y:F2})　落差 {pc.FeetWorldPos.y - pgo.transform.position.y:F2}" : "")
                : "⚠ 找不到玩家";

            // 最近的傳送點
            string near = "（此圖沒有點模式傳送點）";
            if (pgo != null)
            {
                Vector2 probe = pgo.transform.position;
                float best = float.MaxValue;
                for (int i = 0; i < regions.Count; i++)
                {
                    var r = regions[i];
                    if (r == null || r.typeId != mgr.mapLoader.teleportTypeId) continue;
                    if (!TeleportAnchor.TryTouchRect(r, out Rect rect)) continue;
                    float d = Vector2.Distance(probe, rect.center);
                    if (d >= best) continue;
                    best = d;
                    near = $"最近：{r.name}　框 x[{rect.xMin:F2},{rect.xMax:F2}] y[{rect.yMin:F2},{rect.yMax:F2}]"
                         + $"　判定點在框內={(rect.Contains(probe) ? "是" : "否")}　啟用={(TriggerChain.IsActive(r) ? "是" : "否")}";
                }
            }

            var st = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = false };
            st.normal.textColor = new Color(0.3f, 1f, 0.55f);
            GUI.Label(new Rect(12f, Screen.height - 46f, Screen.width - 24f, 20f), feetTxt, st);
            GUI.Label(new Rect(12f, Screen.height - 26f, Screen.width - 24f, 20f), near, st);
        }

        void FillRectWorld(Rect rect, Color c)
        {
            GL.Begin(GL.QUADS);
            GL.Color(c);
            GL.Vertex3(rect.xMin, rect.yMin, 0f);
            GL.Vertex3(rect.xMax, rect.yMin, 0f);
            GL.Vertex3(rect.xMax, rect.yMax, 0f);
            GL.Vertex3(rect.xMin, rect.yMax, 0f);
            GL.End();
        }

        void DrawCollider(Collider2D col, Vector2 viewMin, Vector2 viewMax)
        {
            Color tint = ColorFor(col);

            // ① CompositeCollider2D：畫合併後的外框（＝真正的碰撞外形），
            //    並用同物件上那些 usedByComposite 的 box 來填色（那才是形狀的來源）。
            if (col is CompositeCollider2D comp)
            {
                // 牆的 composite 涵蓋整張地圖（幾百條 box），逐條做視野裁切，只畫看得到的。
                col.GetComponents(_boxBuf);
                for (int b = 0; b < _boxBuf.Count; b++)
                    if (_boxBuf[b].usedByComposite) DrawBox(_boxBuf[b], tint, viewMin, viewMax, outline: false);

                for (int p = 0; p < comp.pathCount; p++)
                {
                    _path.Clear();
                    comp.GetPath(p, _path);
                    if (_path.Count < 2) continue;
                    GL.Begin(GL.LINES);
                    GL.Color(new Color(tint.r, tint.g, tint.b, LineAlpha));
                    for (int k = 0; k < _path.Count; k++)
                    {
                        Vector3 a = comp.transform.TransformPoint(_path[k]);
                        Vector3 d = comp.transform.TransformPoint(_path[(k + 1) % _path.Count]);
                        GL.Vertex3(a.x, a.y, 0f);
                        GL.Vertex3(d.x, d.y, 0f);
                    }
                    GL.End();
                }
                return;
            }

            // ② 一般方框（近乎實心的地上物、以及任何還沒走 Composite 的東西）
            if (col is BoxCollider2D box)
            {
                DrawBox(box, tint, viewMin, viewMax, outline: true);
                return;
            }

            // ③ 圓形（玩家、怪物）。順便標出「腳底」——玩家的碰撞圓心在貼圖中心（腰部高度）
            //    而不是腳底，這是「看起來還沒碰到卻已經被擋住」最常見的來源。
            if (col is CircleCollider2D cir)
            {
                DrawCircle(cir, tint);
                var pc = cir.GetComponent<PlayerController>();
                if (pc != null) DrawCross(pc.FeetWorldPos, 0.18f, new Color(tint.r, tint.g, tint.b, LineAlpha));
                return;
            }

            // ④ 其他型別：用 bounds 畫個外框，至少看得到它在
            OutlineRect(col.bounds.min, col.bounds.max, tint);
        }

        Color ColorFor(Collider2D col)
        {
            int layer = col.gameObject.layer;
            if (layer == _layerPlayer) return PlayerColor;
            if (layer == _layerEnemy || layer == _layerAlly) return EnemyColor;
            if (layer == _layerWater) return BlockerColor;

            // Environment 層底下有兩種來源，用物件名分辨（MapLoader 建的名字）：
            //   Map_Walls / Map_Blockers = 可走層塗出來的；Obj_xxx = 地上物。
            string n = col.gameObject.name;
            if (n.StartsWith("Map_Walls")) return WallColor;
            if (n.StartsWith("Map_Blockers")) return BlockerColor;
            return ObjectColor;
        }

        // ---- 基本繪圖 ----

        /// <summary>畫一顆方框（填色，可選外框）。完全在視野外就直接跳過。</summary>
        void DrawBox(BoxCollider2D box, Color tint, Vector2 viewMin, Vector2 viewMax, bool outline)
        {
            GetBoxCorners(box, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3);

            float minX = Mathf.Min(Mathf.Min(p0.x, p1.x), Mathf.Min(p2.x, p3.x));
            float maxX = Mathf.Max(Mathf.Max(p0.x, p1.x), Mathf.Max(p2.x, p3.x));
            float minY = Mathf.Min(Mathf.Min(p0.y, p1.y), Mathf.Min(p2.y, p3.y));
            float maxY = Mathf.Max(Mathf.Max(p0.y, p1.y), Mathf.Max(p2.y, p3.y));
            if (maxX < viewMin.x || minX > viewMax.x || maxY < viewMin.y || minY > viewMax.y) return;

            GL.Begin(GL.QUADS);
            GL.Color(new Color(tint.r, tint.g, tint.b, FillAlpha));
            GL.Vertex(p0); GL.Vertex(p1); GL.Vertex(p2); GL.Vertex(p3);
            GL.End();

            if (!outline) return;
            GL.Begin(GL.LINES);
            GL.Color(new Color(tint.r, tint.g, tint.b, LineAlpha));
            GL.Vertex(p0); GL.Vertex(p1);
            GL.Vertex(p1); GL.Vertex(p2);
            GL.Vertex(p2); GL.Vertex(p3);
            GL.Vertex(p3); GL.Vertex(p0);
            GL.End();
        }

        static void GetBoxCorners(BoxCollider2D box, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
        {
            Vector2 o = box.offset, h = box.size * 0.5f;
            var t = box.transform;   // 位置/縮放/翻轉都在這裡，與遊戲實際使用的完全一致
            p0 = t.TransformPoint(new Vector3(o.x - h.x, o.y - h.y, 0f));
            p1 = t.TransformPoint(new Vector3(o.x + h.x, o.y - h.y, 0f));
            p2 = t.TransformPoint(new Vector3(o.x + h.x, o.y + h.y, 0f));
            p3 = t.TransformPoint(new Vector3(o.x - h.x, o.y + h.y, 0f));
        }

        void DrawCircle(CircleCollider2D cir, Color tint)
        {
            var t = cir.transform;
            Vector3 center = t.TransformPoint(cir.offset);
            // 圓在非等比縮放下其實是橢圓，但本專案角色都是等比縮放，取 x 就夠
            float r = cir.radius * Mathf.Abs(t.lossyScale.x);
            const int Seg = 32;

            GL.Begin(GL.TRIANGLES);
            GL.Color(new Color(tint.r, tint.g, tint.b, FillAlpha));
            for (int i = 0; i < Seg; i++)
            {
                float a0 = i / (float)Seg * Mathf.PI * 2f, a1 = (i + 1) / (float)Seg * Mathf.PI * 2f;
                GL.Vertex3(center.x, center.y, 0f);
                GL.Vertex3(center.x + Mathf.Cos(a0) * r, center.y + Mathf.Sin(a0) * r, 0f);
                GL.Vertex3(center.x + Mathf.Cos(a1) * r, center.y + Mathf.Sin(a1) * r, 0f);
            }
            GL.End();

            GL.Begin(GL.LINES);
            GL.Color(new Color(tint.r, tint.g, tint.b, LineAlpha));
            for (int i = 0; i < Seg; i++)
            {
                float a0 = i / (float)Seg * Mathf.PI * 2f, a1 = (i + 1) / (float)Seg * Mathf.PI * 2f;
                GL.Vertex3(center.x + Mathf.Cos(a0) * r, center.y + Mathf.Sin(a0) * r, 0f);
                GL.Vertex3(center.x + Mathf.Cos(a1) * r, center.y + Mathf.Sin(a1) * r, 0f);
            }
            GL.End();
        }

        void DrawCross(Vector2 p, float size, Color c)
        {
            GL.Begin(GL.LINES);
            GL.Color(c);
            GL.Vertex3(p.x - size, p.y, 0f); GL.Vertex3(p.x + size, p.y, 0f);
            GL.Vertex3(p.x, p.y - size, 0f); GL.Vertex3(p.x, p.y + size, 0f);
            GL.End();
        }

        void OutlineRect(Vector2 min, Vector2 max, Color tint)
        {
            GL.Begin(GL.LINES);
            GL.Color(new Color(tint.r, tint.g, tint.b, LineAlpha));
            GL.Vertex3(min.x, min.y, 0f); GL.Vertex3(max.x, min.y, 0f);
            GL.Vertex3(max.x, min.y, 0f); GL.Vertex3(max.x, max.y, 0f);
            GL.Vertex3(max.x, max.y, 0f); GL.Vertex3(min.x, max.y, 0f);
            GL.Vertex3(min.x, max.y, 0f); GL.Vertex3(min.x, min.y, 0f);
            GL.End();
        }
    }
}
