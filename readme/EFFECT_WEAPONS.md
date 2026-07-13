# 特效庫武器化（Effects → Weapons）

> 返回 [文件總覽](README.md)｜特效庫操作見 [EFFECT_LIBRARY.md](EFFECT_LIBRARY.md)｜武器欄位見 [RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)

## 資產審閱

- 已審閱 `DipanProj_MapEditor/Effects` 共 398 套動畫、2,421 組顏色／變體、約 48,117 張 PNG。
- 每套先看起始、前段、中段、後段、結尾關鍵幀，入選素材再確認完整動畫與顏色。
- 純換色不視為新機制；只將最符合《燃燈劫》的版本複製進主遊戲，不把整個 Effects 庫放進 Unity `Assets`。

## 最終保留武器

| 武器 ID | 名稱 | 素材 | 系統路線 | 行為 |
|---:|---|---|---|---|
| 21 | 血月鬼爪 | `fx2_claw`（紅） | `IsMelee` | 半徑 2.1、110° 近身扇形，傷害 8；同目標多 Collider 只結算一次 |
| 22 | 虛空吞口 | `scifx1_wormhole_loop`（紫） | `IsGroundCast` | 射程 9、半徑 2.4、持續 4 秒的定點吞噬場 |
| 24 | 九霄雷獄 | `lightning_spell_tileable_start/loop`＋`epic_explosion_B` | `IsSkyStrike`＋分段雷柱 | 點擊落雷；雷首在鏡頭上緣外，等寬 loop 鋪到落點，半徑 1.6 AOE＋大型爆炸 |
| 25 | 幽影突 | `fx1_dash`（白） | `IsDash` | 突進 5 格、掃擊寬 1.2；遇牆／家具提前停，路徑傷害 7 |
| 27 | 冰封法陣 | `fanfx2_ice_spell`（藍） | `IsGroundCast` | 射程 9、半徑 2.5、完整 2.5 秒冰晶動畫與持續傷害 |
| 28 | 死字咒 | `spell_death_001`（紅） | `IsGroundCast` | 射程 10、半徑 2.2，「死」字完整顯現並在生成時造成傷害 12 |

## 新增的泛用模式

### `IsMelee`

- 以玩家為圓心、瞄準方向為軸。
- `BlastRadius`＝攻擊半徑，`MeleeAngle`＝扇形總角度。
- `HitEffectID` 只播一次方向性揮砍動畫；傷害依 GameObject 去重。

### `IsGroundCast`

- 在滑鼠位置生成 `GroundEffectID`。
- `BeamRange` 限制最遠施法距離。
- 半徑、持續時間、DOT 節拍與動畫走 GroundEffectTable；傷害由 WeaponTable 覆寫。

### `IsDash`

- `DashDistance`＝突進距離，`DashWidth`＝障礙探測／掃擊寬度。
- `CircleCast` 遇 Environment 提前停，不穿牆。
- `OverlapCapsuleAll` 覆蓋整段路徑，每個可傷害目標只結算一次。

### `UseSegmentedSkyStrike`

- 僅搭配 `IsSkyStrike`。
- `SegmentedLightningColumn` 以 start 作鏡頭外雷首，依落點距離動態鋪 N 節 tileable loop。
- loop 保持等寬一路延伸到地面，不使用會收細的 end；整柱播放 0.45 秒。
- 九霄雷獄的 `HitEffectID` 是地面爆炸，與雷柱本體分離。

## 對應資料

- WeaponTable／ItemTable：21、22、24、25、27、28。
- RecipeTable：34、35、37、38、40、41。
- VfxTable：22、23、26。
- GroundEffectTable：3、6、7。

## 素材匯入防呆

`GameEffectTextureImportSettings` 會把新放進 `Resources/Weapon`、`Resources/VfxEffects`、`Resources/GroundEffect` 的圖片自動設為 Single Sprite、PPU 100、Point、無 Mipmap、無壓縮。相關故障排除見 [PROBLEMS.md](PROBLEMS.md) C6、C7。
