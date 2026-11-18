using UnityEngine;

namespace Dipan.Vfx
{
    /// <summary>
    /// 掛在子彈上的小標記：「這顆子彈身上要燒 FlameTable 第 N 列的火」（2026-11-18，DipanVfx）。
    ///
    /// 為什麼不直接 <see cref="FlameVisual.Attach"/>：彈道系統的分裂子彈是**拿母彈 Instantiate 複製**出來的，
    /// 標記元件（含序列化欄位）會跟著被複製 ⇒ 每顆分裂出來的子彈在自己的 Start 也會長出一團火，
    /// 主遊戲不必知道分裂發生在哪裡。火本身是獨立物件（不當子彈的子物件），所以不會被子彈的旋轉、縮放影響。
    /// </summary>
    public sealed class FlameHost : MonoBehaviour
    {
        [SerializeField] int _flameId;
        [SerializeField] float _sizeMul = 1f;

        FlameVisual _flame;

        /// <summary>主遊戲在子彈生成後呼叫：<paramref name="sizeMul"/>＝玩家體型 × 集氣放大這類倍率。</summary>
        public static FlameHost Add(GameObject bullet, int flameId, float sizeMul)
        {
            if (bullet == null || flameId <= 0) return null;
            var h = bullet.GetComponent<FlameHost>();
            if (h == null) h = bullet.AddComponent<FlameHost>();
            h._flameId = flameId;
            h._sizeMul = sizeMul > 0f ? sizeMul : 1f;
            return h;
        }

        void Start()
        {
            if (_flame == null) _flame = FlameVisual.Attach(_flameId, transform, _sizeMul);
        }
    }
}
