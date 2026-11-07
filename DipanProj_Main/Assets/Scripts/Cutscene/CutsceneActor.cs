using UnityEngine;

namespace Dipan.Cutscene
{
    /// <summary>
    /// 執行期的劇情演員（包一個 GameObject），由 <see cref="CutsceneDirector"/> 建立與驅動（非 MonoBehaviour）。
    ///  - npc：新生一個物件，路線 B 逐格動畫（<see cref="MonsterAnimator"/>）＋ A* 走位（<see cref="MonsterActuator"/>）＋腳底 Y 排序。
    ///  - player：接管場上玩家——暫停 <see cref="PlayerController"/>、掛一顆臨時 MonsterActuator 用同一套 A* 驅動；結束還原。
    /// 角色只有左右兩向（flipX）。
    /// </summary>
    public class CutsceneActor
    {
        public string id;
        public bool isPlayer;
        public GameObject go;
        public Transform tr;

        MonsterActuator _act;
        Rigidbody2D _rb;
        SpriteRenderer _sr;
        MonsterAnimator _npcAnim;
        PlayerAnimator _playerAnim;
        PlayerController _playerCtl;
        bool _sourceFacesRight = true;
        BlobShadow _shadow;   // npc 演員的腳下影子（飛行演員不掛，見 Npc）

        /// <summary>生一個 npc 演員（外觀走 &lt;spriteFolder&gt; 的 idle/walk，同怪物素材管線）。</summary>
        public static CutsceneActor Npc(string id, string spriteFolder, Vector2 pos, string facing,
                                        float scale, float animFps, float moveSpeed, float tileSize, bool flying = false)
        {
            var a = new CutsceneActor { id = id, isPlayer = false };
            var go = new GameObject("CsActor_" + id);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            if (scale > 0f) go.transform.localScale = Vector3.one * scale;
            a.go = go; a.tr = go.transform;

            a._sr = go.AddComponent<SpriteRenderer>();
            a._rb = go.AddComponent<Rigidbody2D>();
            a._rb.gravityScale = 0f; a._rb.freezeRotation = true;

            a._npcAnim = go.AddComponent<MonsterAnimator>();
            a._npcAnim.Setup(spriteFolder, animFps, moveSpeed, tileSize);

            a._act = go.AddComponent<MonsterActuator>();
            a._act.MoveSpeed = moveSpeed > 0f ? moveSpeed : 3f;
            a._act.AvoidObstacles = !flying;   // 飛行＝關掉 A*/避障，直線飛（不被可走層/牆吸附）

            go.AddComponent<YSortByFeet>();   // 腳底 Y 排序，和地上物/玩家正確交錯
            // 腳下影子（2026-11-07 補，PROBLEMS E46）：以前漏掛，演出裡所有演員都沒影子（怪物／地圖 NPC 都有）。
            // ⚠ **飛行生物**（怪物表 BrainType=Flying／Bat，例：血蝠）刻意不掛：演員沒有「地面點＋高度」（IAirborneVisual），
            //    影子只會貼在身體正下方，看起來像貼地滑行；真要飛行演員有地面影子，得替演員加離地高度，另案。
            // ⚠ **不能用演員的 `flying` 勾選判斷**：那顆的意思是「直線移動、不被牆擋」，劇本裡也拿來讓地面角色直線離場
            //    （迎賓大廳 exit_vampire 的夏綠蒂、exit_werewolf 的艾爾文）——第一版照它判斷，這兩段就沒影子。
            if (!IsFlyingCreature(spriteFolder)) a._shadow = go.AddComponent<BlobShadow>();
            a.Face(facing);
            return a;
        }

        /// <summary>
        /// 這個外觀在怪物表裡是不是飛行生物（BrainType = Flying／Bat，同 <c>MonsterController</c> 設 IsFlyer 的兩個 case）。
        /// 演員的 spriteFolder＝怪物表的 Name（同一套序列圖資料夾）；查不到（純演出用的角色、或場上沒有 MonsterSpawner）一律當地面角色。
        /// </summary>
        static bool IsFlyingCreature(string spriteFolder)
        {
            if (string.IsNullOrWhiteSpace(spriteFolder)) return false;
            var spawner = Object.FindObjectOfType<MonsterSpawner>();
            if (spawner == null || spawner.AllData == null) return false;
            string key = spriteFolder.Trim();
            foreach (var d in spawner.AllData)
            {
                if (d == null || d.Name == null) continue;
                if (string.Equals(d.Name.Trim(), key, System.StringComparison.OrdinalIgnoreCase))
                    return d.BrainType == "Flying" || d.BrainType == "Bat";
            }
            return false;
        }

        /// <summary>接管場上玩家當「主角傀儡」。</summary>
        public static CutsceneActor Player(string id, string facing, float moveSpeed)
        {
            var a = new CutsceneActor { id = id, isPlayer = true };
            var pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo == null) { Debug.LogWarning("[Cutscene] 找不到玩家（Player tag），player 演員略過。"); return a; }
            a.go = pgo; a.tr = pgo.transform;
            a._sr = pgo.GetComponent<SpriteRenderer>();
            if (a._sr == null) a._sr = pgo.GetComponentInChildren<SpriteRenderer>();
            a._rb = pgo.GetComponent<Rigidbody2D>();
            a._playerCtl = pgo.GetComponent<PlayerController>();
            a._playerAnim = pgo.GetComponent<PlayerAnimator>();
            if (a._playerCtl != null)
            {
                a._sourceFacesRight = a._playerCtl.SpriteSourceFacesRight;
                if (moveSpeed <= 0f) moveSpeed = a._playerCtl.MoveSpeed;
                a._playerCtl.enabled = false;   // 暫停輸入驅動（FixedUpdate 不再把速度歸零）
            }
            a._act = pgo.AddComponent<MonsterActuator>();
            a._act.MoveSpeed = moveSpeed > 0f ? moveSpeed : 5f;
            if (a._rb != null) a._rb.velocity = Vector2.zero;
            a.SetIdle();   // 停用 PlayerController 後沒人驅動動畫 → 先設 idle，否則卡在進場時的走路幀
            a.Face(facing);
            return a;
        }

        /// <summary>朝目標推進一幀（A* 走位）＋播走路動畫＋依水平速度自動轉向。</summary>
        public void TickMove(Vector2 target)
        {
            if (_act == null) return;
            _act.MoveTowards(target);
            float sp = _rb != null ? _rb.velocity.magnitude : 0f;
            SetWalk(sp);
            // 依「往目標的水平方向」轉向（不看瞬時速度，避免 A* 折線/解卡讓頭左右亂轉）；很接近時保持原朝向。
            if (tr != null)
            {
                float dx = target.x - tr.position.x;
                if (Mathf.Abs(dx) > 0.15f) Face(dx >= 0f ? "right" : "left");
            }
        }

        public bool Reached(Vector2 target, float tol)
            => tr != null && ((Vector2)tr.position - target).sqrMagnitude <= tol * tol;

        public void StopMove()
        {
            if (_act != null) _act.Stop();
            if (_rb != null) _rb.velocity = Vector2.zero;
            SetIdle();
        }

        void SetWalk(float sp)
        {
            if (_npcAnim != null) _npcAnim.SetState(MonsterAnimator.State.Walk, sp);
            else if (_playerAnim != null) _playerAnim.SetState(PlayerAnimator.State.Walk, sp);
        }
        void SetIdle()
        {
            if (_npcAnim != null) _npcAnim.SetState(MonsterAnimator.State.Idle, 0f);
            else if (_playerAnim != null) _playerAnim.SetState(PlayerAnimator.State.Idle, 0f);
        }

        public void Face(string dir)
        {
            if (_sr == null || string.IsNullOrEmpty(dir)) return;
            bool faceRight = dir == "right";
            _sr.flipX = (faceRight != _sourceFacesRight);
        }

        public void SetMoveSpeed(float v) { if (_act != null && v > 0f) _act.MoveSpeed = v; }

        // ⚠ 影子是獨立物件、不是子物件：只停用演員的話影子會定格在原地（PROBLEMS E19），所以一起開關。
        public void SetActive(bool on)
        {
            if (go == null || isPlayer) return;
            go.SetActive(on);
            if (_shadow != null) _shadow.SetVisible(on);
        }

        /// <summary>瞬移到某點並停下（略過演出時把走位快轉到終點用；npc 才動，主角傀儡不瞬移）。</summary>
        public void Warp(Vector2 pos)
        {
            if (isPlayer || tr == null) return;
            StopMove();
            if (_rb != null) _rb.position = pos;
            tr.position = new Vector3(pos.x, pos.y, tr.position.z);
        }

        public void EnsureVisible() { if (go != null && !isPlayer && !go.activeSelf) SetActive(true); }

        /// <summary>
        /// 鏡頭要對準的「身體中心」（世界座標），給角色介紹（intro）用。
        /// 主角走 <see cref="PlayerController.BodyCenterWorldPos"/>（可見身體幾何，PROBLEMS E14）；
        /// npc 演員取 SpriteRenderer 的 bounds 中心（＝畫布中心，route B 素材通常接近身體中心；
        /// 偏差由步驟的鏡頭偏移欄位補，不在這裡猜）。
        /// </summary>
        public Vector2 VisualCenter()
        {
            if (isPlayer && _playerCtl != null) return _playerCtl.BodyCenterWorldPos;
            if (_sr != null && _sr.sprite != null) return _sr.bounds.center;
            return tr != null ? (Vector2)tr.position : Vector2.zero;
        }

        public void Cleanup()
        {
            if (isPlayer)
            {
                if (_act != null) Object.Destroy(_act);
                if (_rb != null) _rb.velocity = Vector2.zero;
                if (_playerCtl != null) _playerCtl.enabled = true;   // 還原玩家控制
            }
            else if (go != null) Object.Destroy(go);
        }
    }
}
