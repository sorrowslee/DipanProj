using UnityEngine;

namespace Dipan.Inventory
{
    /// <summary>
    /// 把 Assets/Data/GemTable.csv 拖進 Inspector 提供給 <see cref="GemDatabase"/>。
    /// 與 ItemTableProvider / LanguageTableProvider 同一套慣例：常駐單例場景上沒有可拖檔的物件，
    /// 所以由場景元件持有 TextAsset 參照（建議掛在 GameManagers 上）。見 readme/GEM_SOCKET.md。
    /// </summary>
    public class GemTableProvider : MonoBehaviour
    {
        [Tooltip("Assets/Data/GemTable.csv")]
        public TextAsset gemCSV;
    }
}
