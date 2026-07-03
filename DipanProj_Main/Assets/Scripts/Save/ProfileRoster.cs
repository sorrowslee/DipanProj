using System.Collections.Generic;

namespace Dipan.Save
{
    /// <summary>角色名冊裡的一筆摘要（不含完整資料；畫角色選擇清單用）。</summary>
    public class CharacterProfile
    {
        public string characterId;
        public string name;
        public int slotIndex = -1;              // 屬於哪個存檔欄位（0..SlotCount-1；-1 = 未指定/舊資料）
        public int generation = 1;              // 周目（大進度）
        public int clearedModuleCount = 0;      // 完成關卡數（小進度）——讓存讀檔 UI 只讀 profiles.json 即可顯示
        public string createdAtUtc;
        public string lastPlayedUtc;
        public bool corrupt = false;        // 載入失敗（存檔損毀）時標記，UI 顯示警告、不靜默清空
    }

    /// <summary>
    /// 角色名冊（profiles.json）：所有角色的摘要 + 當前活躍角色。
    /// 開「角色選擇」畫面只讀這個小檔，不必載每個角色的完整存檔。
    /// </summary>
    public class ProfileRoster
    {
        public int schemaVersion = SaveConstants.CurrentSchemaVersion;
        public string activeCharacterId;
        public List<CharacterProfile> characters = new List<CharacterProfile>();

        public CharacterProfile Find(string id)
        {
            if (string.IsNullOrEmpty(id) || characters == null) return null;
            return characters.Find(c => c != null && c.characterId == id);
        }
    }
}
