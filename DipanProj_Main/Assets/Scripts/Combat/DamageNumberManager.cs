using UnityEngine;

/// <summary>
/// 頭上的浮動傷害數字（floating combat text）：被打到時在頭頂跳出「-3」表演一段動畫後消失。
/// 玩家與怪物共用——由 <see cref="MonsterController"/> / <see cref="PlayerController"/> 在「確定吃到傷害後」
/// （過了無敵判定）呼叫，所以被無敵時間擋掉的攻擊不會跳數字、數字 = 減傷後的最終傷害。
///
/// 本檔（Manager）負責「生成」：頭頂位置、**描邊（深色陰影複本）**、**份量分級（傷害越大字越大）**、顏色。
/// 「動態表演」（怎麼飄/彈/轉/淡）由 <see cref="DamageNumberInstance"/> 負責——換表演風格只改那一個檔。
///
/// 世界座標 TextMesh（不走 Canvas）、零 prefab、懶漢單例自動生成——風格對齊 VfxManager / PerfHud。
/// 見 readme/COMBAT.md。
/// </summary>
public class DamageNumberManager : MonoBehaviour
{
    // ── 外觀 / 動畫常數（要調就改這裡）──
    const float CharacterSize = 0.08f;   // TextMesh 世界縮放基準（再乘份量分級倍率）
    const int FontSize = 64;             // 字型解析度（大=清晰；世界大小由 CharacterSize 控）
    const float HeadGap = 0.25f;         // 頭頂之上的間距
    const float Lifetime = 0.85f;        // 存活秒數
    const float RiseSpeed = 1.6f;        // 基準上升速度（世界單位/秒；各表演風格可自行詮釋）
    // 角色/怪物改走 Y 排序帶（MapDepthSort，繞回 16-bit 後約 1~1.7 萬），傷害數字要抬到那之上才不會被角色/地上物蓋掉。
    const int SortingOrder = 24000;      // 高於角色/地上物 Y 排序帶（16-bit 安全，<32767）
    const float ShadowOffset = 0.045f;   // 描邊/陰影偏移（世界單位）

    // ── 份量分級門檻（傷害越大、字越大、越搶眼）──
    const float MidThreshold = 5f;       // ≥ 這個值 → 中字
    const float BigThreshold = 12f;      // ≥ 這個值 → 大字

    static readonly Color PlayerHurtColor = new Color(1f, 0.30f, 0.30f, 1f);  // 玩家受傷 = 紅
    static readonly Color EnemyHurtColor = new Color(1f, 0.92f, 0.50f, 1f);   // 打到怪 = 暖黃

    static DamageNumberManager _instance;
    static bool _quitting;

    static DamageNumberManager Instance
    {
        get
        {
            if (_quitting) return null;
            if (_instance == null)
            {
                var go = new GameObject("[DamageNumberManager]");
                _instance = go.AddComponent<DamageNumberManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance == null) _instance = this;
        _quitting = false;
    }

    void OnApplicationQuit() => _quitting = true;
    void OnDestroy() { if (_instance == this) _instance = null; }

    /// <summary>在目標頭上顯示傷害數字（顏色依目標是否為玩家自動選）。amount &lt;= 0 不顯示。</summary>
    public static void Show(GameObject target, float amount)
    {
        if (target == null || amount <= 0f) return;
        var mgr = Instance;
        if (mgr == null) return;
        bool isPlayer = target.CompareTag("Player");
        mgr.SpawnText(mgr.HeadPosition(target), amount, isPlayer ? PlayerHurtColor : EnemyHurtColor);
    }

    /// <summary>泛用：在指定世界座標、用指定顏色顯示一個傷害數字。</summary>
    public static void ShowAt(Vector3 worldPos, float amount, Color color)
    {
        if (amount <= 0f) return;
        var mgr = Instance;
        if (mgr != null) mgr.SpawnText(worldPos, amount, color);
    }

    // 取頭頂位置：優先碰撞框頂端、其次繪製框頂端，皆無則 transform + 預設高度
    Vector3 HeadPosition(GameObject target)
    {
        var col = target.GetComponent<Collider2D>();
        if (col != null)
            return new Vector3(col.bounds.center.x, col.bounds.max.y + HeadGap, target.transform.position.z);
        var rend = target.GetComponentInChildren<Renderer>();
        if (rend != null)
            return new Vector3(rend.bounds.center.x, rend.bounds.max.y + HeadGap, target.transform.position.z);
        return target.transform.position + Vector3.up * 0.6f;
    }

    void SpawnText(Vector3 pos, float amount, Color color)
    {
        pos.x += Random.Range(-0.18f, 0.18f);   // 微抖：避免多個數字完全重疊

        float charSize = CharacterSize * TierScale(amount);   // 份量分級
        string text = "-" + FormatAmount(amount);

        var go = new GameObject("DamageNumber");
        go.transform.position = pos;

        // 描邊感：黑色陰影複本（主數字的子物件，會跟著主數字的動畫一起動），右下偏移、低一階排序
        var shadowGo = new GameObject("Shadow");
        shadowGo.transform.SetParent(go.transform, false);
        shadowGo.transform.localPosition = new Vector3(ShadowOffset, -ShadowOffset, 0f);
        BuildText(shadowGo, text, charSize, new Color(0f, 0f, 0f, 0.85f), SortingOrder - 1);

        // 主數字
        BuildText(go, text, charSize, color, SortingOrder);

        go.AddComponent<DamageNumberInstance>().Init(Lifetime, RiseSpeed);
    }

    // 建一個世界座標 TextMesh（共用內建字型材質、設定排序）
    TextMesh BuildText(GameObject go, string text, float charSize, Color color, int sortingOrder)
    {
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = Dipan.UI.UIBuilder.DefaultFont;
        tm.fontSize = FontSize;
        tm.characterSize = charSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && tm.font != null)
        {
            mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = sortingOrder;
        }
        return tm;
    }

    // 份量分級：小傷 ×1、中傷 ×1.3、大傷 ×1.7
    static float TierScale(float amount)
        => amount >= BigThreshold ? 1.7f : (amount >= MidThreshold ? 1.3f : 1.0f);

    static string FormatAmount(float amount)
        => Mathf.Approximately(amount, Mathf.Round(amount))
            ? Mathf.RoundToInt(amount).ToString()
            : amount.ToString("0.#");
}
