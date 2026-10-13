using UnityEngine;

/// <summary>
/// 「虛體」層（Layer 10 = <c>Mist</c>）：怪物進入**無法被攻擊的形態**時把自己搬到這一層（血霧型，見 <see cref="BloodMistBrain"/>）。
///
/// <para><b>為什麼用換層而不是只在 TakeDamage 擋：</b>玩家所有武器（直飛彈、雷射、地面 AOE…）找目標一律是
/// 「CircleCast／Overlap 對 Enemy 層」（見 ARCHITECTURE「子彈碰撞」與 PlayerController 的 TargetLayers），
/// 只擋 TakeDamage 的話子彈還是會**撞上霧、被吃掉、噴擊中特效**——畫面上就是「打中了但沒扣血」。
/// 換到一個沒有任何武器瞄準的層，所有武器**天生**就會穿過去，不必一把一把改。
/// <see cref="MonsterController.TakeDamage"/> 另外有一道 <c>IsUntargetable</c> 防線，接住不看層的傷害來源（召喚物直接呼叫等）。</para>
///
/// <para><b>碰撞：</b>比照 Enemy 層的設定，再額外穿過 Player／Enemy／Ally／自己——
/// 也就是「只被牆擋」，霧可以穿過其他怪與玩家。進場前用 <see cref="Physics2D.IgnoreLayerCollision"/> 設好，
/// **不必手改 Physics2D 的碰撞矩陣**（同 <see cref="FactionLayers"/>）。</para>
///
/// 前置：Project Settings &gt; Tags and Layers 的 user layer 10 = "Mist"（2026-10-13 已加進 TagManager.asset）。
/// 沒有這一層時 <see cref="Layer"/> 為 -1：怪物不換層、只靠 TakeDamage 防線（子彈會被霧吃掉但不扣血），並印一次 warning。
/// </summary>
public static class MistLayer
{
    public const string LayerName = "Mist";

    /// <summary>Mist 層編號；專案沒設這一層時為 -1。</summary>
    public static int Layer { get; private set; } = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Layer = LayerMask.NameToLayer(LayerName);
        if (Layer < 0)
        {
            Debug.LogWarning("[MistLayer] 找不到 'Mist' 層。請在 Project Settings > Tags and Layers 把 user layer 10 設成 Mist，" +
                             "否則血霧狀態的怪會退回「子彈打得到霧但不扣血」。");
            return;
        }

        int enemy = LayerMask.NameToLayer("Enemy");
        // 先完整抄一份 Enemy 層的碰撞設定（牆、水…照舊擋）
        if (enemy >= 0)
            for (int i = 0; i < 32; i++)
                Physics2D.IgnoreLayerCollision(Layer, i, Physics2D.GetIgnoreLayerCollision(enemy, i));

        // 再穿過所有「角色」層：霧不推人、不被其他怪擋
        foreach (string n in new[] { "Player", "Enemy", "Ally" })
        {
            int l = LayerMask.NameToLayer(n);
            if (l >= 0) Physics2D.IgnoreLayerCollision(Layer, l, true);
        }
        Physics2D.IgnoreLayerCollision(Layer, Layer, true);
    }
}
