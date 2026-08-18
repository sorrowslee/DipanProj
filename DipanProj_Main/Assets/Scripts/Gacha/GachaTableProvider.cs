using UnityEngine;

namespace Dipan.Gacha
{
    /// <summary>
    /// 把抽選系統要用的 CSV 交給那些靜態表的小元件（與 ItemTableProvider / SceneFxTableProvider 同套路）。
    /// 掛在場景的 GameManagers 上，把 Assets/Data 底下的表拖進對應欄位。
    ///
    /// 為什麼需要：GachaPoolTable / GachaRollTable / BloodlineTable 都是靜態類別、場景上沒有可拖檔的物件，
    /// 所以由這個被動 provider 持有 TextAsset 參照，那些表在載入時 FindObjectOfType 取用。
    ///
    /// <b>新增一個抽選大項時</b>：做好 BaseXxxRoll.csv 後，記得把它拖進 <see cref="rollTableCSVs"/> 陣列
    /// （陣列長度 +1）。漏拖不會報錯，只會在該池打開時顯示「還沒有可抽的東西」。
    /// </summary>
    public class GachaTableProvider : MonoBehaviour
    {
        [Tooltip("把 Assets/Data/GachaPoolTable.csv 拖進來")]
        public TextAsset poolTableCSV;

        [Tooltip("把 Assets/Data/ 底下所有 BaseXxxRoll.csv 拖進來（一個大項一張）")]
        public TextAsset[] rollTableCSVs;

        [Tooltip("把 Assets/Data/BloodlineTable.csv 拖進來（表B：每種血統的外型與屬性）")]
        public TextAsset bloodlineTableCSV;

        [Tooltip("把 Assets/Data/BloodlineSeriesTable.csv 拖進來（表A：系列 → 三個階段）")]
        public TextAsset bloodlineSeriesTableCSV;

        // ── 靜態表的取用入口（找不到 provider 時回 null，各表會自己印指引警告）──

        static GachaTableProvider Find() => FindObjectOfType<GachaTableProvider>();

        public static TextAsset PoolTableCsv
        {
            get { var p = Find(); return p != null ? p.poolTableCSV : null; }
        }

        public static TextAsset[] RollTableCsvs
        {
            get { var p = Find(); return p != null ? p.rollTableCSVs : null; }
        }

        public static TextAsset BloodlineTableCsv
        {
            get { var p = Find(); return p != null ? p.bloodlineTableCSV : null; }
        }

        public static TextAsset BloodlineSeriesTableCsv
        {
            get { var p = Find(); return p != null ? p.bloodlineSeriesTableCSV : null; }
        }
    }
}
