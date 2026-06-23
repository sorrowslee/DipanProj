using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

namespace Dipan.Save
{
    /// <summary>載入結果。</summary>
    public enum LoadStatus
    {
        Ok,                    // 主檔正常
        RecoveredFromBackup,   // 主檔壞、從 .bak 救回
        Missing,               // 沒有任何檔（全新）
        Corrupt,               // 主檔與備份都壞（不要靜默清空，交給上層提示）
    }

    /// <summary>
    /// 低階存檔 IO：序列化、原子寫入、HMAC 校驗碼（sidecar）、備份救援。
    /// **完全不認識遊戲內容**（只吃/吐 DTO 物件），可單元測試。
    ///
    /// 每個「存檔單元」= 主檔 path + sidecar：
    ///   path                （可讀 JSON）
    ///   path + ".sig"        （HMAC-SHA256，偵測手改/損毀）
    ///   path + ".bak"        （上一次的好主檔）
    ///   path + ".bak.sig"
    /// 見 readme/SAVE_SYSTEM.md §4。
    /// </summary>
    public static class SaveSystem
    {
        // 內嵌密鑰：單機無 server，校驗只為「偵測竄改/損毀 + 勸退隨手亂改」，不是真 DRM。
        const string AppSecret = "Dipankara.SaveKey.v1";

        const string SigExt = ".sig";
        const string BakExt = ".bak";
        const string TmpExt = ".tmp";

        static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,          // 人類可讀
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
        };

        // ───────────── 高階：角色 / 名冊 ─────────────

        public static ProfileRoster LoadRoster()
        {
            if (TryReadObject(SavePaths.ProfilesPath, "roster", out ProfileRoster roster, out var status))
            {
                if (status == LoadStatus.RecoveredFromBackup)
                    Debug.LogWarning("[SaveSystem] 角色名冊主檔損毀，已從備份還原。");
                return roster;
            }
            if (status == LoadStatus.Corrupt)
                Debug.LogError("[SaveSystem] 角色名冊損毀且無法從備份還原，建立空名冊（請檢查 saves/profiles.json）。");
            return new ProfileRoster();
        }

        public static void SaveRoster(ProfileRoster roster)
        {
            SavePaths.EnsureRoot();
            WriteObject(SavePaths.ProfilesPath, roster, "roster");
        }

        public static bool LoadCharacter(string characterId, out CharacterSave save, out LoadStatus status)
        {
            bool ok = TryReadObject(SavePaths.CharacterPath(characterId), characterId, out save, out status);
            if (ok) save = Migrate(save);
            return ok;
        }

        public static void SaveCharacter(CharacterSave save)
        {
            if (save == null || string.IsNullOrEmpty(save.characterId))
            {
                Debug.LogError("[SaveSystem] SaveCharacter：save 為空或缺 characterId。");
                return;
            }
            SavePaths.EnsureCharDir(save.characterId);
            WriteObject(SavePaths.CharacterPath(save.characterId), save, save.characterId);
        }

        public static void DeleteCharacter(string characterId)
        {
            try
            {
                string dir = SavePaths.CharDir(characterId);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch (Exception e) { Debug.LogError($"[SaveSystem] 刪除角色失敗 {characterId}：{e.Message}"); }
        }

        /// <summary>結構遷移：版本落後就補新欄位預設值（目前只有 v1，預留鉤子）。</summary>
        static CharacterSave Migrate(CharacterSave save)
        {
            if (save == null) return null;
            // if (save.schemaVersion < 2) { /* v1 -> v2 ... */ save.schemaVersion = 2; }
            if (save.schemaVersion != SaveConstants.CurrentSchemaVersion)
                save.schemaVersion = SaveConstants.CurrentSchemaVersion;
            // null 防呆（手改檔可能刪掉整個區塊）
            if (save.inventory == null) save.inventory = new Dipan.Inventory.InventoryDTO();
            if (save.storages == null) save.storages = new System.Collections.Generic.List<Dipan.Inventory.StorageDTO>();
            if (save.stats == null) save.stats = new StatsDTO();
            if (save.progress == null) save.progress = new ProgressDTO();
            return save;
        }

        // ───────────── 低階：序列化 + 校驗 ─────────────

        public static string Serialize<T>(T obj) => JsonConvert.SerializeObject(obj, JsonSettings);
        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, JsonSettings);

        static string Sig(string content, string salt)
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(AppSecret + "|" + salt));
            return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(content)));
        }

        // ───────────── 低階：寫（原子 + 備份 + sig）─────────────

        /// <summary>原子寫入一個存檔單元：先備份現有好檔，再原子換上新主檔與 sig。</summary>
        public static void WriteObject<T>(string path, T obj, string salt)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                string json = Serialize(obj);
                string sig = Sig(json, salt);

                // 備份現有主檔（盡力而為；備份壞掉不影響這次寫入）
                if (File.Exists(path))
                {
                    try
                    {
                        File.Copy(path, path + BakExt, true);
                        if (File.Exists(path + SigExt)) File.Copy(path + SigExt, path + BakExt + SigExt, true);
                    }
                    catch (Exception e) { Debug.LogWarning($"[SaveSystem] 備份失敗（不影響本次寫入）：{e.Message}"); }
                }

                AtomicWriteText(path, json);
                AtomicWriteText(path + SigExt, sig);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 寫檔失敗 {path}：{e.Message}");
            }
        }

        static void AtomicWriteText(string finalPath, string content)
        {
            string tmp = finalPath + TmpExt;
            File.WriteAllText(tmp, content, new UTF8Encoding(false));   // 不寫 BOM
            ReplaceFile(tmp, finalPath);
        }

        static void ReplaceFile(string tmp, string finalPath)
        {
            if (File.Exists(finalPath))
            {
                try { File.Replace(tmp, finalPath, null); return; }
                catch { /* 某些檔系統不支援 Replace，退回刪除+搬移 */ }
                try { File.Delete(finalPath); } catch { }
            }
            File.Move(tmp, finalPath);
        }

        // ───────────── 低階：讀（校驗 + 救援）─────────────

        /// <summary>讀一個存檔單元：主檔校驗通過就用；否則退回 .bak；都不行回 false + 狀態。</summary>
        public static bool TryReadObject<T>(string path, string salt, out T result, out LoadStatus status) where T : class
        {
            result = null;

            if (TryLoadOne(path, path + SigExt, salt, out result))
            {
                status = LoadStatus.Ok;
                return true;
            }

            // 主檔失敗 → 試備份
            if (TryLoadOne(path + BakExt, path + BakExt + SigExt, salt, out result))
            {
                status = LoadStatus.RecoveredFromBackup;
                return true;
            }

            bool anyExisted = File.Exists(path) || File.Exists(path + BakExt);
            status = anyExisted ? LoadStatus.Corrupt : LoadStatus.Missing;
            return false;
        }

        static bool TryLoadOne<T>(string jsonPath, string sigPath, string salt, out T result) where T : class
        {
            result = null;
            if (!File.Exists(jsonPath)) return false;

            string json;
            try { json = File.ReadAllText(jsonPath); }
            catch (Exception e) { Debug.LogWarning($"[SaveSystem] 讀取失敗 {jsonPath}：{e.Message}"); return false; }

            // 校驗（缺 sig 或不符 → 視為不可信，走救援）
            if (!File.Exists(sigPath))
            {
                Debug.LogWarning($"[SaveSystem] 缺校驗檔：{sigPath}（視為損毀）");
                return false;
            }
            string expected;
            try { expected = File.ReadAllText(sigPath).Trim(); }
            catch { return false; }
            if (expected != Sig(json, salt))
            {
                Debug.LogWarning($"[SaveSystem] 校驗碼不符：{jsonPath}（檔案被改過或損毀）");
                return false;
            }

            try { result = Deserialize<T>(json); }
            catch (Exception e) { Debug.LogWarning($"[SaveSystem] JSON 解析失敗 {jsonPath}：{e.Message}"); return false; }

            return result != null;
        }
    }
}
