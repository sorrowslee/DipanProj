# NPC 系統（放置、閑晃、對話、開介面）

> 返回 [文件總覽](README.md)｜對話內容見 [DRAMA.md](DRAMA.md)｜互動（按 F）機制見 [INTERACTION.md](INTERACTION.md)｜觸發鏈見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)｜怪物地基見 [MONSTER_SETUP.md](MONSTER_SETUP.md)、[ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)
>
> **狀態：✅ 第一波完成（2026-08-28）**：地圖編輯器「NPC」分頁放置、原地/來回走動、按 F 交談（DramaTable Type 1/2）、對話結束開介面（panelId）、接觸發鏈（next/setFlag）。護送任務與多方陣營為未來項目，地基已預留（見 §7）。

在地圖編輯器擺一個人物，設定他的行為（原地不動／沿路徑來回走）、對話（`dramaId`）、對話結束後開的介面（`panelId`），存檔 Sync 後遊戲裡就有一個會走動、可交談的 NPC。

**設計原則：NPC 建立在怪物那套地基上**（MonsterController／MonsterAnimator 程式逐格動畫／MonsterActuator A* 導航／BlobShadow／YSortByFeet 全部沿用），只是陣營為**中立（Neutral）**：不打人、不被打、不推擠。所以未來護送任務要的 HP／受擊反應天生就有。

---

## 1. 資料流（三層）

| 層 | 存哪 | 內容 |
|---|---|---|
| **種類**（這是「誰」） | `Assets/Data/NpcTable.csv` | 外觀資料夾、顯示名、縮放、動畫幀率、預設速度、HP（護送預留） |
| **擺放**（放在哪、做什麼） | `.dipanmap` 的 `npcs` 清單（編輯器「NPC」分頁） | 站位、行為、路徑點、速度覆寫、dramaId、panelId、next/setFlag |
| **對話內容** | `DramaTable.csv`（＋`DramaTalkTable.csv`） | 既有劇情系統，Type 1 大圖／Type 2 頭像對話都通用 |

### NpcTable.csv 欄位（依表頭取值，表尾加欄不用改程式）

| 欄位 | 說明 |
|---|---|
| `ID` | NPC 種類編號（編輯器「角色」下拉選這個） |
| `Name` | **程式鍵＝序列圖資料夾名**：圖放 `GameAssets/(Main|Modules/<關卡>)/Monsters/SequenceImage/<Name>/idle·walk/` |
| `DisplayName` | 顯示名稱（留空＝退回 Name） |
| `Scale` | 整體縮放（同怪物：transform 倍率，留空＝1） |
| `AnimFPS` | 逐格動畫幀率（留空＝8） |
| `Speed` | 走動速度（留空＝2；編輯器每個擺放可覆寫） |
| `HP` | 血量（留空＝50）。第一波 NPC 不會被打到，**護送任務預留** |

範例列：`1,Family_Father,示範村民,1,8,1.6,50`（ID 1，用 Main 的 Family_Father 序列圖）。

### 圖放哪（重要：沿用「角色序列圖庫」，不另開分類）

NPC 的圖與**怪物、劇情演出的演員**共用同一個圖庫：`Monsters/SequenceImage/<資料夾>/idle/`（必備）＋`/walk/`（要走動才放）。劇情演員早就這樣用（非怪物角色放這裡是既有慣例），所以：**三處同步工具、MonsterSpriteLibrary、編輯器預覽（PreviewSpriteLoader 直讀磁碟）全部零改動**。放新圖後照常跑 `Project Tools → Sync Map Assets`。

---

## 2. 編輯器「NPC」分頁

左側工具列新按鈕「NPC」。面板照場景特效/照明的模式：清單＋選取＋參數。

- **＋ 新增 NPC**：生在畫面中心、進放置模式，點畫布定位。角色預設用 NpcTable 第一列，面板「角色」可換（下拉列出全表）。
- **畫布操作**：直接**拖曳** NPC（青圈把手）；選取後路徑點是綠色小方塊、也可直接拖。ESC 取消模式。
- **行為**：`原地`／`來回走動`。來回＝按「＋ 加路徑點（連續點畫布）」逐點點出路徑（自動切成來回模式），走法＝**站位→點1→點2…乒乓來回**（走到尾倒著走回站位）。「速度」（0＝用表）與「停留秒」（抵達每點停多久）可調。
- **對話與介面**：`對話id`（DramaTable 的 ID，空＝不對話）；`介面`＋`參數`（對話結束後開，**沒填對話＝按 F 直接開**，例如純商人）。
- **接鏈**：`接續`（本圖 trigger 名稱，可按「選」從清單挑）＋`寫旗標`（同觸發點的旗標登記表，輸入 id → 確認）。
- **消失旗標**（2026-08-28 加）：旗標成立＝這個 NPC 消失——進圖時已成立就不生（換圖回來也不會回來，直到旗標清掉）、關卡中途成立就即時退場。典型：三方陣營劇本的和平版 NPC 填「開戰旗」，開戰瞬間退場換戰鬥版怪物上場（見 [FACTION.md](FACTION.md)）。
- **NPC 角色的預覽**在**所有工具下都會顯示**（會播 idle 呼吸動畫；直讀主專案 GameAssets，不必同步）；把手與路徑線只在 NPC 工具下畫。主專案的 NpcTable.csv 改了按面板「重讀 NPC 表」。
- 資料存 `.dipanmap` 的 `npcs` 清單（獨立於三圖層；舊地圖缺欄＝空清單）。**編輯器 `Data/NpcInstance.cs` 與主遊戲 `MapModel.cs` 的 NpcInstance 是鏡像，改欄位要兩邊一起改。**

---

## 3. 遊戲端行為

- **生成**：`MapManager.PlaceAndSetup` → `MapLoader.SpawnNpcs()` → `NpcSpawner.Spawn`（在生怪之後）。換圖由 `ClearTransientGameplay` 一併清掉（非 PlayerAlly 都清）。
- **陣營＝Neutral、放 Ally 層**：玩家子彈打 Enemy 層→打不到 NPC；敵怪的接觸傷害與友軍找目標都查 `FactionRelations` →不打中立；`ContactDamage=0` → NPC 也不傷人。
- **走動**：`NpcBrain`（IMonsterBrain）驅動 `MonsterActuator.MoveTowards` → **A* 導航免費附贈**（路徑點之間有家具會繞）。
- **面向（2026-08-28 作者拍板）**：NPC 平時**完全不看玩家**——走路面向移動方向（`MonsterController.FaceMovement`）、原地/停留時保持原本朝向（`DetectionRange=0`，見 NpcSpawner）。**只有按 F 對話那一刻**由 `NpcAgent` 轉向玩家，**對話結束轉回對話前的朝向**、繼續未完路程。（最初版是「玩家走近 2.6 格就轉頭」，實測會和走路面向互搶造成左右抖動，已移除。）
- **交談**：靠近出現「按 F 鍵交談」提示（`InteractionManager` 統一管理；NPC 會移動，走 `NpcAgent.Active` 登記表動態比距離，同掉落物）。頭上有**對話泡泡標示**（`NpcTalkMarker`，純程式畫、零素材；走 Overlay 相機，暗場景也可見）。按 F → NPC 停下、面向玩家 → 依 `dramaId` 播對話（Type 1 大圖／Type 2 頭像對話；面板暫停遊戲）。
- **對話結束後**（面板關閉，走 `TriggerChain.CompleteAfterDramaAction`，延一幀）：① 有填 `panelId` 就開介面（`InteractionManager.OpenPanelById`，與祭壇 openPanel trigger **同一張對應表**——之後買賣/兌換介面做好，在那個 switch 加 case、NPC 與 trigger 同時受益）；② 跑鏈：寫 `setFlag`、`Activate(next)`。
- **一次性語意**：**對話可反覆聊**（隨時能再按 F）；**鏈（setFlag/next）每次進圖只跑第一次**（NPC 隨換圖重生＝「關卡單次」語意）。要跨圖/跨周目一次性，讓 next 指到的 trigger 自己用「條件旗標／重複規則」把門關上（Activate 會查它的條件）。

### panelId 對應表（唯一維護點：`InteractionManager.OpenPanelById`）

| panelId | 介面 | 參數 |
|---|---|---|
| `gacha` | 祭壇抽選 | 抽選池代號（GachaPoolTable 的 PoolId） |
| （未來）`shop`／`exchange` | 買賣／兌換 | 建議＝shopId，商品內容另開 ShopTable.csv 資料驅動 |

---

## 4. 執行單元

**主遊戲（`Assets/Scripts/Npc/`）**

| 檔案 | 角色 |
|---|---|
| `NpcData.cs` / `NpcTableProvider.cs` / `NpcDatabase.cs` | NpcTable.csv 的資料類／場景 Provider（拖 CSV）／懶漢載入（依表頭取值） |
| `NpcBrain.cs` | 決策機：原地／乒乓巡邏（含每點停留）；`Talking` 時站住 |
| `NpcSpawner.cs` | 把 NpcInstance＋NpcData 組裝成場上 NPC（走 MonsterController 地基、Neutral、Ally 層） |
| `NpcAgent.cs` | 互動大腦：按 F → 對話 → 開介面 → 接鏈；`Active` 登記表 |
| `NpcTalkMarker.cs` | 頭上對話泡泡（程式畫、Overlay 層、自動跟隨） |

**動到的既有檔**：`MonsterFaction`（+Neutral）、`FactionRelations.cs`（**新**：敵我判定單一真相）、`EnemyContactDamage`／`MonsterController.FindNearestEnemy`（改查 FactionRelations）、`MonsterController`（+`FaceMovement`）、`MapModel`（+NpcInstance/npcs）、`MapLoader`（+SpawnNpcs）、`MapManager`（呼叫）、`TriggerChain`（+`CompleteAfterDramaAction`）、`InteractionManager`（NPC 目標＋`OpenPanelById` 抽出）、`PlayModeStaticReset`（+NpcDatabase）。

**編輯器（`DipanProj_MapEditor`）**：`Data/NpcInstance.cs`（鏡像）、`Tools/NpcController.cs`（放置/拖曳/路徑點）、`Core/NpcView.cs`（角色預覽）、`Core/NpcOverlay.cs`（把手/路徑線）、`Preview/NpcTableEditor.cs`（直讀主專案 NpcTable.csv）、`EditTool`/`EditorUI`/`EditorBootstrap`（分頁接線）。

---

## 5. 怎麼用（一條龍）

1. **放圖**：`GameAssets/(Main|Modules/<關卡>)/Monsters/SequenceImage/<角色名>/idle/`（＋`walk/`）。已有角色（如 Family_Father）可直接用。
2. **NpcTable.csv 加一列**（ID／Name=資料夾名／DisplayName…）。
3. **（一次性）Unity 接線**：GameManagers 上 Add Component → **`NpcTableProvider`**，把 `Assets/Data/NpcTable.csv` 拖進 **Npc CSV** 欄（同 DramaTableProvider 作法）。
4. **編輯器**：NPC 分頁 → 新增 → 放站位 → 設行為/路徑 → 填 dramaId（要先在 DramaTable/DramaTalkTable 寫好對話）→（選）panelId／next／setFlag → 存檔。
5. **同步**：`Project Tools → Sync Map Assets`。
6. **Play**：走近 NPC 看到泡泡＋「按 F 鍵交談」→ 按 F 對話 → 關閉後開介面／跑鏈。

---

## 6. 已知限制（第一波刻意不做）

- NPC **不會被任何攻擊打到**（血條、可死亡＝護送任務那一波）。
- 鏈的一次性只有「關卡單次」語意（見 §3）；沒有 per-NPC 的重複規則欄。
- 編輯器預覽的角色大小與遊戲**近似**而非完全一致（預覽沿用劇情演員管線；遊戲端另有 CharacterWorldHeight 正規化）。
- `speed`/`dwellSeconds` 以外沒有 per-擺放的 Scale/AnimFPS 覆寫（要不同大小＝ NpcTable 開兩列）。
- 對話中其他 NPC 也會被面板暫停凍住（全遊戲 PausesGame 慣例，非 bug）。
- **純開介面（沒填 dramaId）的 NPC** 按 F 後會轉向玩家、但介面關閉後**不會轉回**（介面面板沒有關閉回呼可掛；有對話的 NPC 才有「對話結束轉回」）。巡邏中的會在續走時自己轉回，原地的會一直看著玩家——真的介意再補。

## 7. 未來預留（設計時已挖好的插座）

- **護送任務**：`NpcBrain` 加 `escort` 模式（「玩家在半徑內才沿路徑前進」）＋頭上血條；HP/受擊反應地基已有（NpcTable 已有 HP 欄）。`behavior` 是字串欄，加值不動格式。
- **多方陣營** → ✅ **已完成第二波**（2026-08-28，見 [FACTION.md](FACTION.md)）：Werewolf/Vampire 部族、開戰/結盟鏈動作、演戲傷害、結盟切層。
- **買賣/兌換介面**：`OpenPanelById` 加 case ＋ ShopTable.csv；NPC 端欄位（panelId/panelArg）已就緒。
