# 角色影子 (Blob Shadow)

> 返回 [文件總覽](README.md)｜角色見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)

俯視角的「腳下橢圓影子」：在角色腳下放一個半透明深色橢圓，畫在角色之下、地面之上，每幀跟著角色走。**只要出現在遊戲中的角色（玩家／怪物）都會有影子。**

為什麼用 blob shadow 而不是即時光照投影：本專案是 **Built-in 算繪管線、沒有 2D 燈光**（見 [ATMOSPHERE.md](ATMOSPHERE.md)），blob shadow 是俯視角最常見、最省、最穩的做法，零光照依賴。

## 做法

* 元件 `Assets/Scripts/BlobShadow.cs`：掛在角色上即可。玩家由 `PlayerController.Start`、怪物由 `MonsterController.Start` 各自 `AddComponent<BlobShadow>()`（已接好）。
* 影子是**獨立 GameObject**（不是角色的子物件）——避免被角色的 `flipX` 翻轉或 `localScale` 縮放二次影響；每幀 `LateUpdate` 把影子移到角色腳下。角色銷毀時 `OnDestroy` 自動清掉影子。
* 影子圖是**程序生成的柔邊圓**（中心實、邊緣淡的 alpha 貼圖，白色靠 `SpriteRenderer.color` 染成黑半透明），整個遊戲**共用一張**（static 快取）。零 prefab、零美術。
* 大小依角色 sprite 的世界寬度自動算（`bounds.size.x × WidthFactor`），縱向壓扁成橢圓。排序設在角色 `sortingOrder` 之下一階（畫在角色腳下、地面之上）。

## 可調參數（`BlobShadow` Inspector / 程式預設）

| 欄位 | 預設 | 說明 |
|---|---|---|
| `ShadowColor` | 黑 alpha 0.45 | 影子顏色與濃淡 |
| `WidthFactor` | 0.85 | 影子寬 = 角色世界寬 × 此值 |
| `HeightRatio` | 0.4 | 影子高 / 寬（越小越扁、俯視感越強） |
| `VerticalOffset` | 0 | 腳底再往下(正)/上(負)微調 |
| `SortingOrderBelow` | 1 | 比角色 sortingOrder 低幾階 |

> 想讓影子更淡/更大/更扁，調上面這幾個值即可（改 `BlobShadow.cs` 上方預設，或在 Inspector 對個別角色調）。

## 給新角色加影子

任何之後出現的角色（NPC、Boss…），在它的初始化加一行即可：

```csharp
if (GetComponent<BlobShadow>() == null) gameObject.AddComponent<BlobShadow>();
```

## 限制 / 之後可加

* 影子大小在 `Start` 算一次（用站立幀的 sprite 寬度）；若角色動畫尺寸變化很大，影子不會跟著縮放。需要的話可改成每幀更新。
* 目前是固定橢圓；之後若要「跳躍時影子變小、離地拉開」之類，可在 `BlobShadow` 依角色狀態調 scale / 位置。
* 大型可破壞地上物（家具）目前**沒有**影子（只角色有）；要的話也可掛 `BlobShadow`。

---

*建立於 2026-06-25：腳下橢圓 blob shadow（程序生成柔邊圓、共用快取、獨立物件跟隨、依角色寬度自動縮放），玩家與怪物自動掛上。*
