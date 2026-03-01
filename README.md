# 燃燈計畫 (Project Dipankara) - 專案開發上下文與架構文件

## 1. 專案概述 (Project Overview)
* **遊戲名稱**：燃燈計畫 (Project Dipankara)
* **發行平台**：Steam
* **遊戲類型**：2D 遊戲 (Top-down / 俯視角視角)
* **世界觀與故事設定**：
  * 核心概念啟發自小說《無限恐怖》。
  * 故事場景中存在一尊佛像（燃燈古佛），**佛像本身是守護者，真正的邪惡能量是從佛像背後的「隧道」散發出來的**。
* **開發狀態**：專案已正式啟動，目前正處於核心戰鬥系統 (Core Combat Loop) 與底層架構的建置階段。

---

## 2. 程式架構設計 (Architecture Design)
本專案採用高度解耦的「模組化」設計，將遊戲主體與通用系統分離，便於日後維護與擴充。

### 2.1 雙模組結構
1. **主遊戲模組 (DipanProj_Main)**：
   * 負責遊戲具體邏輯：玩家控制、怪物 AI、關卡流程、血量與傷害計算。
   * 依賴於彈道系統。
2. **獨立彈道系統 (Sorrows.Ballistics Package)**：
   * 純粹負責「子彈生成、物理飛行、碰撞偵測、特殊飛行行為」。
   * **絕對不涉及**任何遊戲內的「血量、傷害數值」計算。
   * 透過 `System.Action` 將碰撞事件回報給主遊戲。

### 2.2 物理圖層與標籤矩陣 (Physics & Layer Matrix)
* **Tags (標籤)**：
  * 玩家物件需標記為 `Player`（供 AI 索敵使用）。
* **Layers (圖層)**：
  * `Layer 3: Environment` (環境與牆壁)
  * `Layer 6: Player` (玩家)
  * `Layer 7: Enemy` (敵人)
* **碰撞偵測原則**：彈道系統發射時，由主遊戲動態傳入 `LayerMask`（例如 `Environment | Enemy`），決定子彈該與哪些圖層發生碰撞。

### 2.3 美術與資源架構 (Asset & Resource Architecture)
為了支援後續的場景擴充與 DLC 機制，避免主程式過度膨脹及重構困擾，美術資源一律放置於自定義的 `Assets/GameAssets/` 目錄下（避開 Unity 預設會強制打包的 `Resources` 資料夾），並採用「主資源包 + 獨立場景包」的架構：

1. **主資源包 (GameAssets/Main/)**：
   * 存放所有場景通用的基礎設施。
   * 包含：遊戲一開始的主選單、通用介面 (UI)、角色模型、通用裝備、通用道具 Icon。
2. **場景資源包 (GameAssets/Modules/)**：
   * 每個場景都是一個獨立的子目錄，完整打包該場景強烈綁定的資源。
   * 包含：該場景的主要大圖、給予玩家的專屬卡片大圖、專屬 Tilemap / 地上物、該場景的怪物 Skin 與飛行物外型。
3. **資源載入與擴充邏輯**：
   * **初始載入**：遊戲開啟時，預設擁有並載入「Main 主遊戲資源包」+「Modules/Tutorial 教學場景資源包」。
   * **動態下載**：後續玩家若在遊戲中購買或解鎖新場景（例如：大佛前主場景），才會觸發下載該特定場景的資源包，並讓對應的美術物件在遊戲中出現。

---

## 3. 核心系統實作細節 (Core Systems)

### 3.1 彈道系統 (Sorrows.Ballistics)
採用 Data-Driven (資料驅動) 與 Strategy Pattern (策略模式) 設計。

* **ProjectileDefinition (ScriptableObject)**：
  * 子彈的「配方資料」。
  * 包含基礎屬性：`Speed`, `LifeTime`, `PierceCount` (穿透次數)。
  * 包含行為屬性：反彈 (`HasBounce`, `MaxBounces`)、分裂 (`HasSplit`, `Timing`, `SplitCount`, `SpreadAngle`, `SubProjectileData`)。
* **BallisticsEngine (靜態引擎)**：
  * 負責 `Spawn` 子彈實體，並在子彈初始化前預先綁定 `OnBulletHitObject` 事件，確保第 0 幀撞擊不漏接。
  * 提供 `Internal_SpawnSplit` 供分裂行為遞迴生成子彈，並繼承碰撞回報事件。
* **BulletInstance (子彈實體)**：
  * 使用 `Physics2D.Raycast` 進行連續碰撞偵測（避免穿牆）。
  * 內部維護 `HashSet<int> _hitObjects` 防止同一幀對同一目標造成重複傷害。
  * 處理 `PierceCount` 邏輯（穿透時不銷毀）。
* **IBulletBehavior (行為介面)**：
  * `BounceBehavior`：處理牆壁反彈（使用 `Vector2.Reflect`）。
  * `SplitBehavior`：處理撞擊、生成或消滅時的扇形分裂邏輯。

### 3.2 玩家控制器 (PlayerController)
* 掛載 `Rigidbody2D`，透過 `FixedUpdate` 控制 `_rb.velocity` 進行精準物理移動。
* 負責接收玩家輸入，並呼叫 `BallisticsEngine.Spawn` 發射子彈。
* **傷害串接**：發射時將自身的 `HandleBulletHit` 方法傳入彈道系統。當子彈觸發碰撞時，由該方法取得目標的 `MonsterController` 並執行 `TakeDamage`。

### 3.3 怪物 AI 系統 (Monster AI)
* **MonsterSensor**：利用 `GameObject.FindGameObjectWithTag("Player")` 獲取玩家位置，並計算距離是否在 `DetectionRange` 內。
* **Brain & Actuator**：負責判斷邏輯（如 `ChaseBrain` 追擊）與實際物理移動。
* **MonsterController**：
  * 管理怪物血量 (`MaxHealth`, `_currentHealth`)。
  * 提供 `TakeDamage(float amount)` 介面供子彈命中時呼叫，並處理死亡 `Die()` 銷毀邏輯。

---

## 4. 目前進度 (Current Progress)
* [x] 確立無限恐怖風格的 2D 世界觀與隧道設定。
* [x] 完成主遊戲與彈道系統的模組解耦。
* [x] 實作 ScriptableObject 驅動的子彈系統（支援反彈、扇形分裂、穿透）。
* [x] 解決子彈高頻率生成時的事件訂閱同步問題（Pre-subscribe 模式）。
* [x] 實作怪物基礎追擊 AI，並成功與彈道系統接軌，完成「射擊 -> 命中 -> 扣血 -> 死亡」的完整 Core Loop。
* [x] 規劃並建立「主資源包 + 場景模組包」的美術目錄架構，完成教學場景地磚的 Tilemap 基礎設定。

## 5. 待辦事項與未來規劃 (Next Steps & Roadmap)
1. **資料驅動的怪物生成系統 (Data-Driven Monster Spawner)**：
   * 建立 `MonsterData.cs` 與 CSV 讀取腳本。
   * 透過讀取 Excel/CSV 設定表（包含 ID, Name, HP, Speed, BrainType, PrefabPath），動態於場景中生成對應數值的怪物。
2. **AI 狀態機擴充**：
   * 根據 CSV 傳入的 `BrainType`，動態賦予怪物不同的行為模式（如巡邏 Patrol、逃跑 RunAway、遠程攻擊）。
3. **場景與關卡機制**：
   * 實作佛像與隧道入口的場景互動與邪惡能量的視覺/機制表現。
4. **武器與技能擴展**：
   * 透過擴展 `IBulletBehavior` 實作更多樣的彈道軌跡（如追蹤彈、蛇行彈、延遲爆炸）。