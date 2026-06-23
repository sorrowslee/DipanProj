# 存檔系統 (Save System) — 本地、多角色、跨平台

> 返回 [文件總覽](README.md)
>
> 背包資料層見 [INVENTORY.md](INVENTORY.md)；地圖狀態持久化（Phase 2）見 [MAP_SYSTEM.md](MAP_SYSTEM.md)；UI 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)。
>
> **狀態：✅ Phase 1 程式完成（2026-06-23）、待 Unity 實機驗證。** 物品本地存檔 + 多角色 + 轉生 + 校驗/備份/原子寫入已實作（見 §13）。儲藏箱/屬性/地圖狀態/角色選擇 UI/Steam Cloud 為後續。本文件是規劃藍圖；實作若與現況有出入，以實作為準並回頭修本文件。

把「玩家所擁有的東西」（背包 + 裝備 + 將來的儲藏箱，以及之後的角色屬性與地圖進度）存到**本地端**，跨 Windows / macOS，**無 server**。一次只有一個活躍角色（取名遊玩 → 卡關時留一樣物品 → **轉生**創新角色繼承該物品），但底層做成**多角色獨立存檔**以保留彈性。

---

## 0. 定案決策（先讀）

| 項目 | 決定 | 理由 |
|---|---|---|
| 存哪裡 | **`Application.persistentDataPath`** | Unity 官方跨平台路徑，Win/Mac 各自對到使用者資料夾，零特例（見 §2） |
| 格式 | **人類可讀 JSON**（Newtonsoft，專案已有） | 好除錯、好遷移；專案地圖系統已用同一套 JSON |
| 防竄改 | **可讀 JSON ＋ HMAC 校驗碼（sidecar）** | 單機無 server，不做真加密；校驗只為**偵測手改/損毀**（偵測到→警告或退回備份），不擋鐵了心要改的人 |
| 角色模型 | **多角色獨立存檔**（roster ＋ 每角色一份檔） | 即使現在同時只有一個活躍角色，多角色機制最保險，且天然支援「轉生繼承」 |
| 存檔容器 | **統一角色存檔**（這次先填物品，結構預留屬性/進度/地圖狀態） | 一個角色一份檔，未來接 [MAP_SYSTEM.md](MAP_SYSTEM.md) Phase 2 與角色屬性時**加上去**而非重構 |
| Steam Cloud | **現在純本地，但檔案佈局預留 Cloud** | 日後接 Steamworks Auto-Cloud 只需在後台設路徑樣式，程式零改（見 §8） |
| 寫檔安全 | **原子寫入（temp→rename）＋ 一份備份（.bak）** | 遊戲可能當掉/被強制關；半寫的檔不能蓋掉好檔 |

> **設計鐵則**（沿用專案紀律）：**遊戲系統不認識「檔案」**。`InventorySystem` 之流只負責「給快照 / 吃快照」（純資料 DTO），檔案 IO、路徑、校驗、備份全部收在 `SaveSystem` / `SaveManager`。這跟「彈道系統不算傷害」「GroundEffect 資料 vs 視覺」「UI 純呈現層」是同一套解耦哲學。

---

## 1. 名詞

| 名詞 | 意義 |
|---|---|
| 角色 / Character | 玩家建立的一個遊玩主體，有名字與獨立的物品/屬性/進度。對應磁碟上一個資料夾 + 一份 `character.json`。 |
| 存檔欄位 / Profile（roster 項） | `profiles.json` 裡描述某角色的摘要（id、名字、世代、最後遊玩時間…），用來畫「角色選擇/管理」清單，**不含完整資料**。 |
| 活躍角色 / Active | 當前正在玩的角色。`profiles.json` 記 `activeCharacterId`。 |
| 轉生 / Reincarnation | 卡關時選一樣物品保留 → 建立新角色繼承該物品 → 切為活躍。世代數 `generation` 遞增。 |
| 快照 / Snapshot（DTO） | 某個遊戲系統把當前狀態打包成的純資料物件（如 `InventoryDTO`），供存檔序列化；反向可吃回去還原。 |
| 校驗碼 / Checksum | 對存檔位元組算的 HMAC-SHA256，存成 sidecar（`character.sig`），載入時比對偵測竄改/損毀。 |

---

## 2. 存哪裡（跨平台的核心答案）

統一用 **`Application.persistentDataPath`**——這就是「本地端要存哪」的標準答案，Unity 已幫你處理 OS 差異：

| 平台 | 實際路徑（`<Company>`/`<Product>` 取自 Player Settings） |
|---|---|
| **Windows** | `C:\Users\<使用者>\AppData\LocalLow\<Company>\<Product>\` |
| **macOS** | `~/Library/Application Support/<Company>/<Product>/` |

> 不要自己用 `Environment.SpecialFolder` 或硬寫路徑——`persistentDataPath` 已是兩平台都對的可寫入位置，也是日後 Steam Cloud Auto-Cloud 最好對應的根。Player Settings 的 **Company Name / Product Name 一旦上線就別亂改**（改了路徑會變，舊存檔就找不到）。

### 檔案佈局

```
<persistentDataPath>/
├─ saves/
│  ├─ profiles.json            ← 角色名冊（roster）＋ activeCharacterId（小檔、常讀）
│  ├─ profiles.sig             ← profiles.json 的校驗碼
│  └─ char_<guid>/             ← 每個角色一個資料夾
│     ├─ character.json        ← 該角色的「統一存檔」（物品/屬性/進度/地圖狀態）
│     ├─ character.sig         ← character.json 的校驗碼
│     └─ character.bak         ← 上一次的好檔（救援用）
└─ settings.json               ← 全域設定（音量/按鍵/語言…，**不屬於任何角色**）
```

- **一角色一資料夾**：Steam Cloud 樣式好寫（`saves/**`），刪角色＝刪資料夾，乾淨。
- **roster 與完整資料分離**：開「角色選擇」畫面只讀小小的 `profiles.json`，不必把每個角色的大檔都載進來。
- **全域設定獨立**：音量、按鍵、語言這些是「這台機器/這個玩家」的偏好，不該綁在角色身上，放 `settings.json`。

---

## 3. 統一角色存檔結構（`character.json`）

這次**先把物品填起來**，其餘區塊先佔位（填預設/空），結構刻意預留給未來。所有物品**只記 ID 與數量**，不存名稱/圖示等定義（那些由 `ItemTable.csv` / `ItemDatabase` 在載入時解析，見 §6 的相容性）。

```jsonc
{
  "schemaVersion": 1,                     // 遷移用（見 §7）
  "characterId": "9f3c…guid",
  "name": "玩家取的名字",
  "generation": 1,                        // 轉生世代（第幾代）
  "createdAtUtc": "2026-06-23T08:00:00Z",
  "lastPlayedUtc": "2026-06-23T09:12:00Z",
  "playTimeSeconds": 5230,

  // ── 這次的核心：物品 ──────────────────────────────
  "inventory": {
    // 稀疏：只存非空格（slot = 0..GridCount-1）
    "grid": [
      { "slot": 0, "itemId": 1,   "count": 1 },
      { "slot": 5, "itemId": 101, "count": 87 }
    ],
    // 只存有裝東西的欄；空欄省略
    "equipment": { "Weapon": 1, "Amulet": 103 }
  },

  // 儲藏箱（將來會有；現在可為空陣列）。每個箱子一筆、各有獨立格子。
  "storages": [
    {
      "storageId": "home_chest",          // 該箱的穩定鍵（之後由箱子定義表給）
      "rows": 6, "cols": 9,
      "grid": [ { "slot": 0, "itemId": 102, "count": 12 } ]
    }
  ],

  // ── 以下先佔位，這次不填內容（或填預設）──────────────
  "stats": {                              // 角色屬性（HP 上限、金錢、等級…）— 待屬性系統
    "currency": 0
  },
  "progress": {                           // 解鎖關卡、劇情旗標、轉生繼承紀錄
    "inheritedItemId": 0,                 // 本代從上一代繼承來的物品（0 = 無）
    "unlockedModules": [],
    "flags": {}
  },
  "mapStates": {}                         // 對接 MAP_SYSTEM Phase 2 的「每張地圖狀態庫」
}
```

設計要點：
- **稀疏存格**：63 格大多是空的，只存非空格省空間、也對「之後背包格數變動」較有韌性（還原時超出新格數的格子走 `AddItem` 溢位處理，見 §6）。
- **`mapStates` 預留**：[MAP_SYSTEM.md](MAP_SYSTEM.md) 的 `MapManager` 持有 `Dictionary<int mapId, MapState>`（怪死了沒、撿過什麼、地上掉落物…）。Phase 2 做存檔永久化時，就把它序列化進這個欄位，**不必另開存檔系統**。
- **`generation` / `progress.inheritedItemId`**：轉生機制的紀錄（見 §5）。
- **校驗碼不放在這份 JSON 裡**：放 sidecar `character.sig`，讓 `character.json` 保持乾淨可讀（見 §4.2）。

---

## 4. 寫檔／讀檔的可靠性

### 4.1 原子寫入（防半寫損毀）

遊戲可能在寫檔當下被當掉或強關，**絕不能讓半寫的檔蓋掉好檔**。流程：

1. 序列化成字串 `s`。
2. 寫進暫存檔 `character.json.tmp`，`flush` + 關閉。
3. 算 `s` 的校驗碼，寫 `character.sig.tmp`。
4. 若現有 `character.json` 通過校驗（是好檔）→ 複製成 `character.bak`。
5. 用 `File.Replace`（或先刪後 `Move`）把 `.tmp` 換成正式檔——這一步盡量接近原子。
6. `.sig` 同樣換上。

### 4.2 校驗碼（偵測竄改/損毀，不是加密）

- 對 `character.json` 的位元組算 **HMAC-SHA256**（金鑰是內嵌在程式裡的固定字串 + 角色 id 當鹽），結果存到 sidecar `character.sig`。
- **載入時**重算並比對：
  - 一致 → 正常載入。
  - 不一致（被手改、檔毀、或 `.sig` 缺）→ 視為「可疑/損毀」，走 §4.3 救援。
- 這是「**可讀 JSON ＋ 校驗碼**」：玩家想看/想改 JSON 都行，但改了沒同步 `.sig` 就會被偵測到。單機遊戲不做真正 DRM——目標是**抓損毀 + 勸退隨手亂改**，不是防破解。
- （替代做法）也可把校驗碼當作 JSON 內一個 `_checksum` 欄、算雜湊時把該欄清空再算，省一個檔案；但 sidecar 寫法最單純、JSON 也最乾淨，**建議用 sidecar**。

### 4.3 載入救援順序

1. 讀 `character.json` → 校驗通過 → 解析成功 → ✅ 用它。
2. 失敗（缺檔/校驗不過/解析爆）→ 試 `character.bak`（同樣校驗+解析）→ 成功就用它並提示「已從備份還原」。
3. 兩者都失敗 → **不要靜默清空**。標記該角色「存檔損毀」，在角色選擇畫面顯示警告，讓玩家決定（重試/刪除/回報），避免一個壞檔把玩家資料無聲清掉。

---

## 5. 角色生命週期與轉生

### 5.1 一般流程

- **建立角色**：輸入名字 → 產生 `characterId`（GUID）→ 建資料夾 + 初始 `character.json`（空背包、`generation=1`）→ 寫進 `profiles.json` 並設為活躍。
  - **名字不是存檔前提**：存檔以 `characterId`（GUID）為鍵，`name` 只是顯示欄位，沒填也存得起來。
  - **測試角色預設名 `test001`**：正式建角流程（UI 輸入框）接上前，測試用角色一律給預設名 `test001`，方便驗證存讀；之後玩家輸入的名字會覆蓋它。
- **載入角色**：從 roster 選一個 → 讀其 `character.json` → 把各區塊**還原**進對應的活躍系統（`InventorySystem.RestoreState(dto)` 等，見 §6）。
- **儲存**：把各活躍系統的**快照**收集成 `CharacterSave`，原子寫回該角色資料夾（時機見 §9）。
- **刪除角色**：刪整個 `char_<guid>/` 資料夾，從 roster 移除。

### 5.2 轉生（你描述的核心玩法）

> 卡關 → 選一樣物品留下 → 創新角色繼承它 → 重新遊玩。同時只有一個活躍角色，但**舊角色檔保留**（多角色機制讓這變得免費且安全）。

`SaveManager.Reincarnate(carryItemId, newName)` 流程：

1. 先把當前角色**存一次**（落定它的最終狀態）。
2. 建新角色：新 GUID、`generation = 舊代 + 1`、`progress.inheritedItemId = carryItemId`。
3. 把 `carryItemId` 放進新角色的初始背包（透過 `InventorySystem.AddItem`，吃滿堆疊規則）。
4. **舊角色去留**：因為採多角色存檔，**預設保留舊角色檔**（只是不再活躍），未來要做「轉生族譜/回顧」很方便；若想精簡也可在這裡刪掉舊角色。**建議先保留**。
5. 設新角色為活躍、寫 roster、開始新一輪。

> 之後若改成「同時可有多個活躍角色」或「繼承多樣物品」，這套結構都不必重來——只是 `Reincarnate` 的參數與 UI 變化。

---

## 6. 與現有系統的接法（解耦邊界）

**`SaveManager` 依賴各遊戲系統；各遊戲系統不依賴 `SaveManager`**——它們只多開「打包/還原」兩個方法，回傳/接收純資料 DTO。這守住「資料層不碰檔案」。

### 6.1 `InventorySystem`（[INVENTORY.md](INVENTORY.md)，已存在，需小幅擴充）

現況：持有 `ItemStack[] _grid` + `Dictionary<EquipSlot,int> _equip`，變動發 `OnChanged`。要加兩個方法（不碰檔案、不碰 UI）：

```csharp
// 打包：把目前背包/裝備變成可序列化 DTO（稀疏，只收非空）
public InventoryDTO CaptureState();

// 還原：吃 DTO 重建背包/裝備，最後 Raise() 一次讓 UI 重繪
public void RestoreState(InventoryDTO dto);
```

- `SaveManager` 在「存檔」時呼叫 `CaptureState()`，在「載入角色」時呼叫 `RestoreState(dto)`。
- `SaveManager` 訂閱 `InventorySystem.OnChanged` → 標記「dirty」（待存），配合 §9 的存檔時機。
- **InventorySystem 完全不知道有檔案這回事**，維持純資料層。

### 6.2 物品 ID 的相容性（重要，跨改版不崩）

存檔只記 `itemId`，定義靠 `ItemTable.csv` 在載入時解析。改版會動到物品表，所以還原時要**防呆**：

- 某 `itemId` 在現行 `ItemTable` 找不到（物品被移除/改號）→ **跳過該格 + 記 log**，不要讓整份存檔解析失敗。
- `count` 還原時**夾到該物品的 `MaxStack`**（表上限若調小了，避免超疊）。
- 背包格數（`Columns`/`Rows`）若未來改變：用 `slot` 索引還原，超出新 `GridCount` 的格子改走 `AddItem` 找空位塞回（塞不下就溢位處理/提示）。
- **紀律：物品 ID 一旦上線就不要回收或重新編號**（同 [MAP_SYSTEM.md](MAP_SYSTEM.md) 對地圖 `ID` 的要求）。

### 6.3 儲藏箱（將來）

做法與背包同模——`StorageSystem` 持有一或多個箱子的格子資料，開 `CaptureState()/RestoreState()`，序列化進 `character.json` 的 `storages[]`。每個箱子要一個**穩定 `storageId`**當鍵。

### 6.4 地圖狀態（[MAP_SYSTEM.md](MAP_SYSTEM.md) Phase 2）

`MapManager` 已規劃成持久單例、持有每張地圖的 `MapState`。Phase 2 接存檔時：`MapManager` 開 `CaptureMapStates()/RestoreMapStates()`，`SaveManager` 把結果塞進 `character.json` 的 `mapStates`。屆時兩個系統在 `SaveManager` 會合，互不直接依賴。

---

## 7. 版本與遷移（schemaVersion）

遊戲會持續長新功能（儲藏箱、屬性、地圖狀態…），存檔結構會變。靠 `schemaVersion`：

- 程式有個 `CurrentSchemaVersion` 常數。
- 載入時若 `存檔.schemaVersion < Current` → 跑**遷移鏈**（v1→v2→v3…，每段只補該版新增欄位的預設值），遷移完再用。
- Newtonsoft 對**未知欄位預設忽略**，**缺欄位給型別預設**——所以「小幅加欄位」多半零遷移成本；只有「改語意/改結構」才需要寫遷移函式。
- 遷移成功後，下一次存檔就會寫成新版（自然升級）。

---

## 8. Steam Cloud 預留（現在不接 SDK，但別擋路）

上 Steam 後，**Steamworks「Auto-Cloud」**可在合作夥伴後台設定：指定每個 OS 的根路徑 + 檔案樣式，Steam 自動上傳/下載，**遊戲程式幾乎零改**。本設計已對齊它：

- 用 `persistentDataPath` 當根（Auto-Cloud 後台對應 Win 的 `%AppData%\LocalLow\…`、Mac 的 `~/Library/Application Support/…`）。
- 存檔集中在 `saves/`、用 `saves/**` 樣式一網打盡（含 `.json`/`.sig`/`.bak`）。
- 檔案小、文字為主 → 同步快、額度省。
- `settings.json` 是否上雲可自行決定（按鍵/音量這類「裝置偏好」常**不**上雲，避免跨機覆蓋）。

> 之後若改用 Steamworks 的 **ISteamRemoteStorage API**（程式主動讀寫雲端）也行，但 Auto-Cloud 對「純檔案存檔」最省事，建議優先。**衝突處理**（同一帳號兩台機器都玩過）：先靠 Steam 內建的衝突視窗；要更穩可用 `lastPlayedUtc` 做「較新者勝」的判斷，屬日後增強。

---

## 9. 存檔時機（什麼時候寫檔）

不要「每次物品變動就寫檔」（太頻繁）。改成**標記 dirty + 在安全檢查點落地**：

- **換地圖**：`MapManager.GoToMap` 是天然檢查點（[MAP_SYSTEM.md](MAP_SYSTEM.md)），順手存。
- **開/關儲藏箱**：搬完東西就存。
- **轉生 / 建立角色**：必存。
- **退出/暫停**：`OnApplicationQuit` 與 `OnApplicationPause(true)`（被 Steam 覆蓋層、Alt-Tab、關機觸發）都要存——這是防掉檔的最後一道。
- **定時自動存**：若 dirty，每 60–120 秒寫一次。
- **不要在戰鬥的每一幀存**。背包這種小資料序列化 <1ms，主執行緒寫沒問題；真要避免卡頓可把「序列化字串」算好後**背景緒寫檔**（但 `OnApplicationQuit` 那次要同步寫完才退）。

---

## 10. 新增的程式檔（建議，全程式建構、對齊專案風格）

| 檔案（建議路徑） | 角色 |
|---|---|
| `Assets/Scripts/Save/SavePaths.cs` | 集中算路徑（root、profiles、某角色資料夾/檔），唯一碰 `persistentDataPath` 的地方 |
| `Assets/Scripts/Save/SaveSystem.cs` | 低階檔案 IO：原子寫入、讀取、校驗碼、備份救援。**不認識遊戲內容**，可單元測試 |
| `Assets/Scripts/Save/CharacterSave.cs` | 統一存檔 DTO（§3 的結構），純 C# + Newtonsoft 標註 |
| `Assets/Scripts/Save/SaveDtos.cs` | `InventoryDTO` / `StorageDTO` / `GridSlotDTO` 等子 DTO |
| `Assets/Scripts/Save/ProfileRoster.cs` | `profiles.json` 的 roster 資料 + 讀寫 |
| `Assets/Scripts/Save/SaveManager.cs` | **大腦**（持久單例，仿 `MapManager`）：當前角色、載入/儲存、自動存計時、建立/刪除/轉生、退出鉤子、dirty 標記、各系統 capture/restore 的協調 |
| （擴充）`InventorySystem.cs` | 加 `CaptureState()` / `RestoreState()`（§6.1） |
| （Editor）`Project Tools` 選單 | 「Open Save Folder」「Wipe All Saves」等開發用工具（仿既有 `Project Tools` 慣例） |

> 全部走純程式建構、零 prefab/Inspector 接線，與 `VfxManager` / `LaserBeam` / `UIManager` 一致。`SaveManager` 在開場由 bootstrap 或 `RuntimeInitializeOnLoadMethod` 生出來。

---

## 11. 驗證計畫（實作時要做）

- **往返測試**：建假資料 → `Capture` → 序列化 → 反序列化 → `Restore` → 斷言與原資料一致（背包每格、裝備每欄、稀疏空格）。
- **損毀救援測試**：故意改壞 `character.json`（或刪 `.sig`）→ 確認退回 `.bak`；兩者都壞 → 確認走「標記損毀、不靜默清空」。
- **校驗測試**：手改 JSON 不改 `.sig` → 確認被偵測。
- **遷移測試**：餵一份 `schemaVersion` 較舊的檔 → 確認補上新欄位且不崩。
- **改版相容測試**：存檔含一個已從 `ItemTable` 移除的 `itemId` → 確認跳過 + log、其餘正常載入。
- **跨平台路徑**：在 Mac build 與 Windows build 各確認 `persistentDataPath` 落點正確、可寫入。
- **轉生測試**：轉生後新角色帶著繼承物品、世代 +1、舊角色檔仍在、活躍指向新角色。

---

## 12. 分期任務清單

**Phase 1（已完成程式，待實機驗證）— 物品本地存檔 + 多角色 + 轉生**
- [x] `SavePaths` / `SaveSystem`（原子寫入、HMAC 校驗碼、備份救援、roster/character 高階讀寫）。
- [x] `CharacterSave` + 子 DTO；`schemaVersion = 1`（含 `Migrate` 鉤子）。
- [x] `ProfileRoster`（`profiles.json` 讀寫、activeCharacterId、corrupt 標記）。
- [x] `InventoryDTO` + `InventorySystem.CaptureState/RestoreState`（+ 物品 ID 相容防呆、count 夾上限）。
- [x] `SaveManager`（載入/儲存、dirty、自動存、退出/暫停鉤子、建立/刪除/轉生/切換角色）+ `SaveBootstrap` 自動生成。
- [x] Editor `Project Tools → Save`：Open Save Folder / Print Save Path / Wipe All Saves。
- [ ] **實機驗證**（§11、§13.3）— 待在 Unity 跑一輪。
- [ ] 角色選擇/建立/轉生 **UI**（建在 [UI_SYSTEM.md](UI_SYSTEM.md) 框架上）— 目前只有程式 API，無玩家面向畫面（用 F5/F9 與選單測）。

**之後（結構已預留，不必重構）**
- [x] 倉庫 `StorageSystem` → `storages[]`（已接線；見 [STORAGE.md](STORAGE.md)）。
- [ ] 角色屬性系統 → `stats`。
- [ ] 接 [MAP_SYSTEM.md](MAP_SYSTEM.md) Phase 2：`mapStates` 永久化。
- [ ] 接 Steamworks Auto-Cloud（後台設定為主，程式幾乎零改）。
- [ ] 雲端衝突處理（lastPlayedUtc / Steam 衝突視窗）。

---

## 13. 實作狀態與接手指引（2026-06-23）

### 13.1 已新增/改動的檔案

| 檔案 | 內容 |
|---|---|
| `Assets/Scripts/Inventory/InventoryDTO.cs` | `GridSlotDTO` / `InventoryDTO`（放 `Dipan.Inventory`，讓存檔依賴背包、背包不依賴存檔） |
| `Assets/Scripts/Inventory/InventorySystem.cs` | **改**：加 `CaptureState()` / `RestoreState(dto)`（純資料、不碰檔案） |
| `Assets/Scripts/Save/SaveConstants.cs` | `CurrentSchemaVersion=1`、`DefaultTestCharacterName="test001"` |
| `Assets/Scripts/Save/CharacterSave.cs` | 統一角色存檔 DTO（inventory 已用；storages/stats/progress/mapStates 佔位） |
| `Assets/Scripts/Save/ProfileRoster.cs` | 角色名冊（`profiles.json`） |
| `Assets/Scripts/Save/SavePaths.cs` | 所有路徑（唯一碰 `persistentDataPath` 處） |
| `Assets/Scripts/Save/SaveSystem.cs` | 低階 IO：序列化 / HMAC 校驗 / 原子寫入 / 備份救援 / roster·character 高階讀寫 / `Migrate` |
| `Assets/Scripts/Save/SaveManager.cs` | 大腦：載入活躍角色、dirty/自動存、退出·暫停存、建立/刪除/轉生/切換、F5 存·F9 重載 |
| `Assets/Scripts/Save/SaveBootstrap.cs` | `RuntimeInitializeOnLoadMethod` 開場自動生 `SaveManager`（零接線） |
| `Assets/Editor/SaveTools.cs` | `Project Tools → Save` 選單（開資料夾 / 印路徑 / 清空） |

### 13.2 磁碟實際佈局（檔名以實作為準）

```
<persistentDataPath>/saves/
├─ profiles.json   + profiles.json.sig   (+ .bak / .bak.sig)
└─ char_<guid>/
   └─ character.json + character.json.sig (+ .bak / .bak.sig)
```
> 與 §2 概念圖一致，只是 sidecar 實際命名為 `*.json.sig` / `*.json.bak`（救援與校驗都對得上，功能無差）。

### 13.3 Unity 實機驗證步驟（接手者照做）

1. 開 Unity 等編譯，Console 無紅錯（新程式全在預設 `Assembly-CSharp`，無 asmdef 問題）。
2. **不需要手動接線**：`SaveBootstrap` 會在開場自動生 `[SaveManager]`（也可手動放一個到場景微調 Inspector 參數）。
3. 進 Play：Console 應出現 `[SaveManager] 建立新角色：test001`（首次）。
4. 既有 `InventoryLauncher`（按 B 開背包）在背包**空時**才塞 12 武器 + 雜物——首次會塞。
5. 按 **F5** 手動存檔（或直接結束 Play，`OnApplicationQuit` 會存）。Console 出現 `已存檔：test001`。
6. 再次進 Play：應出現 `[SaveManager] 載入角色：test001`，且背包**已有上次的物品**（`InventoryLauncher` 因 `HasAnyItem()` 為真而不再重塞）→ **存讀成功**。
7. 想看檔案：選單 `Project Tools → Save → Open Save Folder`。
8. 損毀測試：手改 `character.json` 內容但不改 `.sig` → 重進應看到「校驗碼不符」警告並從 `.bak` 還原。
9. 清空重來：`Project Tools → Save → Wipe All Saves`。

> **執行順序關鍵**：`SaveManager` 標了 `[DefaultExecutionOrder(-500)]`，確保它的 `Start`（載入存檔）早於 `InventoryLauncher` 的種子邏輯，否則重開會被測試物品蓋掉/重複。

### 13.4 轉生與多角色（程式已就緒，缺 UI）

- `SaveManager.Instance.Reincarnate(carryItemId, newName)`：存舊角色（保留檔）→ 建世代 +1 的新角色、繼承 `carryItemId` 一樣物品、設為活躍。
- `CreateCharacter(name)` / `SwitchCharacter(id)` / `DeleteCharacter(id)`、`Roster` 都已開放。
- **缺**：玩家面向的「建名輸入框 / 角色選擇清單 / 轉生選物品」UI（建在 [UI_SYSTEM.md](UI_SYSTEM.md) 上）。在 UI 做好前，可在程式或測試腳本直接呼叫上述 API 驗證。

### 13.5 接手新系統時要做的（一句話）

要把「儲藏箱 / 角色屬性 / 地圖狀態」也存起來：在對應系統開 `CaptureState()/RestoreState()`，然後在 `SaveManager.CaptureFromSystems()` 與 `ApplyToSystems()` 各加一行——**不必動 SaveSystem / 檔案層**。

---

*建立於 2026-06-23：定稿設計並完成 Phase 1 程式——`persistentDataPath` + 可讀 JSON + HMAC 校驗 + 原子寫入/備份；多角色 roster + 統一角色存檔（物品先做，屬性/地圖狀態預留）；轉生繼承；Steam Cloud 佈局預留。待 Unity 實機驗證；玩家面向角色/轉生 UI 為後續。*
