using System.Collections.Generic;
using UnityEngine;
using Dipan.Drama;

/// <summary>
/// 一個場上 NPC 的「互動大腦」：按 F 交談 → 對話（DramaTable，Type 1/2 皆可）→ 對話結束後
/// 開介面（panelId，走 InteractionManager.OpenPanelById 同一張表）→ 接觸發鏈（setFlag／next）。
/// 沒填 dramaId 只填 panelId ＝ 按 F 直接開介面（純商人）。兩者都沒填＝這個 NPC 純裝飾、不可互動。
///
/// 由 <see cref="NpcSpawner"/> 生成時掛上並 Configure；「找最近目標＋提示＋收 F 鍵」由
/// <see cref="InteractionManager"/> 統一處理（NPC 會移動，走 <see cref="Active"/> 登記表動態比距離，同掉落物）。
///
/// 一次性語意：對話可**反覆觸發**（NPC 隨時能再聊）；但 setFlag／next 的鏈**每次進圖只跑第一次**
/// （NPC 隨換圖重生，等同互動點的「關卡單次」預設）。要跨圖/跨周目一次性，next 指到的 trigger
/// 自己用「條件旗標／重複規則」把門關上（TriggerChain.Activate 會查它的條件）。
/// </summary>
public class NpcAgent : MonoBehaviour
{
    /// <summary>場上所有 NPC 的登記表（InteractionManager 每幀比距離用；同 MonsterController.Active 模式）。</summary>
    public static readonly List<NpcAgent> Active = new List<NpcAgent>();

    public int DramaId;
    public string PanelId = "";
    public string PanelArg = "";
    public string NextTrigger = "";
    public string SetFlagName = "";
    public string ShownName = "";

    MonsterController _mc;
    NpcBrain _brain;
    NpcTalkMarker _marker;
    Collider2D _col;
    bool _chainFired;   // 鏈每次進圖只跑一次（NPC 隨換圖重生＝關卡單次語意）
    bool _flipBeforeTalk;      // 對話前的朝向（flipX），對話結束轉回——「借過頭來看玩家一下」的語意
    bool _hasFlipBeforeTalk;

    public bool CanInteract => DramaId > 0 || PanelId.Length > 0;

    void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() { Active.Remove(this); }
    void OnDestroy() { if (_marker != null) Destroy(_marker.gameObject); }

    public void Configure(MonsterController mc, NpcBrain brain, Dipan.MapRuntime.NpcInstance inst, NpcData data)
    {
        _mc = mc;
        _brain = brain;
        DramaId = Mathf.Max(0, inst.dramaId);
        PanelId = (inst.panelId ?? "").Trim();
        PanelArg = (inst.panelArg ?? "").Trim();
        NextTrigger = (inst.next ?? "").Trim();
        SetFlagName = (inst.setFlag ?? "").Trim();
        ShownName = data != null ? data.ShownName : "";
        if (CanInteract) _marker = NpcTalkMarker.Create(transform);   // 頭上對話泡泡（純程式畫、零素材）
    }

    /// <summary>提示定位點＝頭頂（碰撞框上緣；框在 MonsterController.Start 才 fit 好，取不到退回 transform）。</summary>
    public Vector3 TipWorldPos
    {
        get
        {
            if (_col == null) _col = GetComponent<Collider2D>();
            return _col != null ? new Vector3(transform.position.x, _col.bounds.max.y, 0f) : transform.position;
        }
    }

    public string TipText(KeyCode key) => DramaId > 0 ? $"按 {key} 鍵交談" : $"按 {key} 鍵";

    /// <summary>玩家按 F（由 InteractionManager 呼叫）。</summary>
    public void Interact()
    {
        RememberFacing();   // 記住對話前的朝向（對話結束轉回；巡邏中＝當下的行進朝向）
        FacePlayer();       // 只有這一刻轉向玩家——平時 NPC 完全不看玩家（DetectionRange=0，見 NpcSpawner）

        if (DramaId > 0)
        {
            var d = DramaDatabase.Instance.Get(DramaId);
            if (d == null)
            {
                Debug.LogWarning($"[NpcAgent] NPC「{name}」的 dramaId={DramaId} 在 DramaTable 找不到，改直接開介面/接鏈。");
                RestoreFacing();
                OnTalkClosedCore();
                return;
            }
            if (_brain != null) _brain.Talking = true;               // 對話中站住不走
            TriggerChain.CompleteAfterDramaAction(OnTalkClosed);     // 面板關閉才續（開介面／接鏈）
            if (d.Type == 2) DramaTalkController.Play(d.TalkGroup, allowSkip: true);
            else Dipan.UI.DramaPanel.Show(DramaId);
        }
        else
        {
            OpenPanel();
            FireChainOnce();
        }
    }

    // 對話面板關閉（TriggerChain.NotifyDramaClosed → 延一幀）後續。
    void OnTalkClosed()
    {
        if (this == null) return;   // 換圖等因素已銷毀（TriggerChain.Setup 也會清掉未結的回呼）
        RestoreFacing();            // 轉回對話前的朝向（原地 NPC 回頭、巡邏 NPC 接回行進方向，續走時 FaceMovement 會接手）
        OnTalkClosedCore();
    }

    void OnTalkClosedCore()
    {
        if (_brain != null) _brain.Talking = false;
        OpenPanel();
        FireChainOnce();
    }

    void RememberFacing()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) { _hasFlipBeforeTalk = false; return; }
        _flipBeforeTalk = sr.flipX;
        _hasFlipBeforeTalk = true;
    }

    void RestoreFacing()
    {
        if (!_hasFlipBeforeTalk) return;
        _hasFlipBeforeTalk = false;
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.flipX = _flipBeforeTalk;
    }

    void OpenPanel()
    {
        if (PanelId.Length == 0) return;
        InteractionManager.OpenPanelById(PanelId, PanelArg, $"NPC「{name}」");
    }

    void FireChainOnce()
    {
        if (_chainFired) return;
        _chainFired = true;
        if (SetFlagName.Length > 0) TriggerChain.SetFlag(SetFlagName);
        if (NextTrigger.Length > 0) TriggerChain.Activate(NextTrigger);
    }

    void FacePlayer()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        var sr = GetComponent<SpriteRenderer>();
        if (p == null || sr == null) return;
        bool right = p.transform.position.x > transform.position.x;
        bool srcRight = _mc == null || _mc.SpriteSourceFacesRight;
        sr.flipX = (right != srcRight);
    }
}
