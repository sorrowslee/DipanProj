# 程式架構設計 (Architecture Design)

> 返回 [文件總覽](README.md)

本專案採用高度解耦的「模組化」設計，將遊戲主體與通用系統分離，便於日後維護與擴充。

## 雙模組結構

1. **主遊戲模組 (DipanProj_Main)**：
   * 負責遊戲具體邏輯：玩家控制、怪物 AI、關卡流程、血量與傷害計算。
   * 依賴於彈道系統，但彈道系統不反向依賴主遊戲。
2. **獨立彈道系統 (Sorrows.Ballistics Package)**：
   * 純粹負責「子彈生成、物理飛行、碰撞偵測、特殊飛行行為」。
   * **絕對不涉及**任何遊戲內的「血量、傷害數值」計算。
   * 透過 `System.Action<BulletInstance, GameObject, RaycastHit2D>` 將碰撞事件回報給主遊戲。

## 解耦原則與邊界規範

* **LayerMask 不寫死**：彈道系統內部不寫死任何 Layer 編號。`PierceableLayers`（可穿透層）與 `NonBounceLayers`（不反彈層）一律由主遊戲在呼叫 `BallisticsEngine.Spawn` 時傳入。
* **傷害數值不進入彈道系統**：子彈命中時只透過 `OnBulletHitObject` 事件回報，傷害計算完全由主遊戲的 `HandleBulletHit` 處理。
* **玩家尋找不依賴具體類別**：怪物 AI 透過 `GameObject.FindGameObjectWithTag("Player")` 尋找玩家，不依賴 `PlayerController` 具體類別。

## 物理設定 (Physics Settings)

* **Tags（標籤）**：
  * 玩家物件標記為 `Player`（供怪物 AI 索敵使用）。
* **Layers（圖層）**：
  * `Layer 3: Environment`（環境與牆壁）
  * `Layer 6: Player`（玩家）
  * `Layer 7: Enemy`（敵人）
* **Rigidbody2D 設定**：玩家與怪物皆使用 **Dynamic** 模式，`GravityScale = 0`，`FreezeRotation Z`。
* **Layer Collision Matrix 設定**：
  * `Enemy vs Enemy`：**關閉**（怪物之間互相穿透，不互卡）
  * `Enemy vs Player`：**關閉**（怪物不推擠玩家，重疊後以損血機制處理）
  * `Enemy vs Environment`：**開啟**（怪物被牆壁阻擋）
  * `Player vs Environment`：**開啟**（玩家被牆壁阻擋）
* **子彈碰撞**：彈道系統使用 `Physics2D.CircleCast` 搭配 `LayerMask` 做偵測，完全不依賴 Unity 物理碰撞事件，不受 Layer Collision Matrix 影響。
* **雷射碰撞**：`queriesStartInColliders = false`（專案全域）。這會讓「從砲口出發的 cast 忽略重疊在起點的碰撞體」，雷射另以 `OverlapCircle` 補抓貼身怪——細節見 [LASER.md](LASER.md)。

## 美術與資源架構 (Asset & Resource Architecture)

美術資源放置於 `Assets/GameAssets/`（避開 Unity 強制打包的 `Resources` 資料夾），採用「主資源包 + 獨立場景包」架構：

1. **主資源包 (GameAssets/Main/)**：存放所有場景通用的基礎設施（主選單、通用 UI、角色模型、通用裝備、道具 Icon）。
2. **場景資源包 (GameAssets/Modules/)**：每個場景一個獨立子目錄，打包該場景專屬資源（場景大圖、專屬卡片、Tilemap、怪物 Skin、飛行物外觀）。
3. **載入邏輯**：
   * 初始載入：`Main` 主資源包 + `Modules/Tutorial` 教學場景資源包。
   * 動態下載：玩家解鎖新場景後才下載對應資源包。
