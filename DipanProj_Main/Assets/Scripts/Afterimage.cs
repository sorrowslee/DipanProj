using UnityEngine;

/// <summary>
/// **殘影**：把角色「這一幀的樣子」留在原地，半透明、快速淡出。
/// 連續丟幾個就是一條拖尾，用來表現「移動得非常快」——跳躍踐踏的下墜段在用。
///
/// <para>用法一行：<c>Afterimage.Spawn(角色的SpriteRenderer)</c>。之後自己淡出、自己銷毀。</para>
///
/// <para><b>為什麼是殘影不是速度線</b>（作者 2026-09-18 問「可以加速度線嗎」）：
/// 俯視角 2D 的角色是「往畫面下方掉」，放射狀速度線得畫成角色上方的拖尾，可讀性遠不如橫向捲軸或 3D；
/// 而且速度線是偏卡通的語彙，跟本專案陰暗寫實的美術會打架。殘影傳達同一件事，
/// 而且**它就是角色自己的剪影**，風格上永遠不會出戲。</para>
///
/// <para>刻意用**預設的 sprite 材質**（不沿用角色身上的 SpriteFlash／環境融合材質）：
/// 那些效果是靠 MaterialPropertyBlock 每幀餵值的，複製不過來；而殘影只需要「半透明的同一張圖」，
/// 用預設材質最穩，也不會被受擊閃白之類的狀態汙染。</para>
/// </summary>
[DisallowMultipleComponent]
public class Afterimage : MonoBehaviour
{
    SpriteRenderer _sr;
    float _life, _t;
    Color _from;

    /// <summary>
    /// 留一個殘影。<paramref name="tint"/> 預設是原色（只降透明度）；想要更像「速度殘影」可以帶一點冷色。
    /// <paramref name="orderOffset"/> 預設 -1 ＝畫在角色底下一階，免得蓋住本體。
    /// </summary>
    public static void Spawn(SpriteRenderer src, float life = 0.20f, float alpha = 0.40f,
                             Color tint = default, int orderOffset = -1)
    {
        if (src == null || src.sprite == null || life <= 0.001f) return;
        if (tint == default) tint = Color.white;

        var go = new GameObject("Afterimage");
        var t = go.transform;
        t.position = src.transform.position;
        t.rotation = src.transform.rotation;
        t.localScale = src.transform.lossyScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = src.sprite;
        sr.flipX = src.flipX;
        sr.flipY = src.flipY;
        sr.sortingLayerID = src.sortingLayerID;
        sr.sortingOrder = src.sortingOrder + orderOffset;

        var c = tint; c.a = alpha;
        sr.color = c;

        var fx = go.AddComponent<Afterimage>();
        fx._sr = sr;
        fx._life = life;
        fx._from = c;
    }

    void Update()
    {
        // 用一般（受 timeScale 影響）的時間：落地定格時殘影也跟著停住，整段演出的節奏才一致。
        _t += Time.deltaTime;
        float k = 1f - Mathf.Clamp01(_t / _life);
        if (k <= 0.001f) { Destroy(gameObject); return; }

        var c = _from;
        c.a = _from.a * k * k;   // 平方衰減：前段還看得清楚，尾巴收得快，拖尾才不會糊成一團
        if (_sr != null) _sr.color = c;
    }
}
