using UnityEngine;

namespace Dipan.UI
{
    /// <summary>
    /// 記住呼叫端**原本**設定的 icon 方框與位置。
    ///
    /// <see cref="IconFit"/> 每次畫圖都會改寫 icon 的 sizeDelta / anchoredPosition，
    /// 所以不能拿「現在的值」當基準重算（會越畫越偏、越畫越大）。第一次畫的時候把原始值存在這裡，
    /// 之後每次都從同一個基準算。純資料、沒有 Update，掛著不花效能。
    /// </summary>
    [DisallowMultipleComponent]
    public class IconFitBox : MonoBehaviour
    {
        /// <summary>呼叫端要的「內容框」——正規化後，圖的**不透明內容**會剛好塞滿這個框。</summary>
        public Vector2 baseSize;
        /// <summary>呼叫端原本的位置（有些面板會把 icon 往上/往下挪一點）。</summary>
        public Vector2 basePos;
        public bool captured;
    }
}
