# 特效庫武器化（Effects → Weapons）

> ⚠ **2026-08-26 RecipeTable 大改**：`IsXxx` 旗標已收成一欄 `Mode`、`BeamRange→Range`、`BlastRadius→AreaRadius`、拋物線飛行秒數獨立成 `FlightTime`、連鎖跳數獨立成 `ChainCount`、錐角／吸附半徑獨立成 `AimConeAngle`／`SnapRadius`。本文提到的欄名已同步更新；完整欄位與各模式吃哪些欄以 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) 為準。

> 返回 [文件總覽](README.md)｜特效庫操作見 [EFFECT_LIBRARY.md](EFFECT_LIBRARY.md)｜武器欄位見 [RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)

Super Pixel Projectiles Pack 4 的 A 組藍色雷射已製作為武器 29「鏡界折光」：origin／center／impact loop 沿反射折線動態平鋪，詳見 [PIXEL_REFLECT_LASER.md](PIXEL_REFLECT_LASER.md)。

## 資產審閱

- 已審閱 `DipanProj_MapEditor/Effects` 共 398 套動畫、2,421 組顏色／變體、約 48,117 張 PNG。
- 每套先看起始、前段、中段、後段、結尾關鍵幀，入選素材再確認完整動畫與顏色。
- 純換色不視為新機制；只將最符合《燃燈劫》的版本複製進主遊戲，不把整個 Effects 庫放進 Unity `Assets`。

## 最終保留武器

| 武器 ID | 名稱 | 素材 | 系統路線 | 行為 |
|---:|---|---|---|---|
| 21 | 血月鬼爪 | `fx2_claw`（紅） | `Mode=Melee` | 半徑 2.1、110° 近身扇形，傷害 8；同目標多 Collider 只結算一次 |
| 22 | 虛空吞口 | `scifx1_wormhole_loop`（紫） | `Mode=GroundCast` | 射程 9、半徑 2.4、持續 4 秒的定點吞噬場 |
| 24 | 九霄雷獄 | `lightning_spell_tileable_start/loop`＋`epic_explosion_B` | `Mode=SkyStrike`＋分段雷柱 | 點擊落雷；雷首在鏡頭上緣外，等寬 loop 鋪到落點，半徑 1.6 AOE＋大型爆炸 |
| 25 | 幽影突 | `fx1_dash`（白） | `Mode=Dash` | 突進 5 格、掃擊寬 1.2；遇牆／家具提前停，路徑傷害 7 |
| 27 | 冰封法陣 | `fanfx2_ice_spell`（藍） | `Mode=GroundCast` | 射程 9、半徑 2.5、完整 2.5 秒冰晶動畫與持續傷害 |
| 28 | 死字咒 | `spell_death_001`（紅） | `Mode=GroundCast` | 射程 10、半徑 2.2，「死」字完整顯現並在生成時造成傷害 12 |

## 第二批（2026-09-24，12 把，零程式改動）

飛行物全部從特效庫取、最近鄰放大 4 倍存 `Resources/Weapon/animation/<資料夾>/`；icon 是**代用圖**（`Resources/Weapon/single/`，左上角標「代用」），作者之後用同檔名覆蓋即可。「分圖」＝裝備 icon 與射出去的飛行物是不同張（同狂族十字弓）。

| 武器 ID | 名稱 | 配方 | Mode | 飛行物素材 → 資料夾 | 代用 icon 檔名 | 分圖 | 命中／附加 |
|---:|---|---:|---|---|---|---|---|
| 36 | 血滴子 | 73 | Normal | `pj4_sawblade` **紅** → `bloodSawblade` | `weapon_xuedizi.png` | ✅ | 反彈 4、穿透 3；Vfx 51 血花 |
| 37 | 引魂幡 | 74 | Normal | `pj3_death_wave` 紫 → `soulBannerSkull` | `weapon_soulbanner.png` | ✅ | 單顆追蹤；Vfx 56 骷髏煙（7 的放大版）；`FlipYWhenLeft=1`（往左飛不倒立） |
| 38 | 餓鬼牙符 | 75 | Normal | `pj3_demon_bite_loop` 紅 → `hungryGhostFang` | `weapon_ghostfangtalisman.png` | ✅ | 短程（0.9 秒）；Vfx 52 `pj3_demon_bite_impact` 咬合；`FlipYWhenLeft=1`＋`HitEffectAlignBullet=1`（往哪飛就往哪咬） |
| 39 | 玄冰針匣 | 76 | Normal | `pj2_ice_spike` 藍 → `iceNeedle` | `weapon_iceneedlebox.png` | ✅ | 單發、穿透 2；Vfx 2 冰凍 |
| 40 | 柳葉飛刀 | 77 | Normal | `pj3_knife` 灰 → `willowKnife` | `weapon_willowknife.png` | ✅（icon 靜止刀，飛行物翻轉動畫） | 3 刀 30° 追蹤 |
| 41 | 飛蝗石 | 78 | Normal | `pj1_rock` 灰 → `locustStone` | `weapon_locuststone.png` | ✅ | 0.15 秒連射、反彈 2；Vfx 53 小碎塵 |
| 42 | 掌心雷 | 79 | Normal | `pj2_lightning_orb` 紫 → `palmThunderOrb` | `weapon_palmthunder.png` | ✅ | 命中迸發子武器 48；Vfx 54 `lightning_burst_002` |
| 48 | 掌心雷電弧（子武器，不進背包） | 80 | Normal | `pj1_electricity` 紫 → `thunderSpark` | — | — | 4 道 120° 快速追蹤 |
| 43 | 無間輪 | 81 | Normal | `pj4_dark_matter` 紫 → `avichiWheel` | `weapon_prayerwheel.png` | ✅ | 速度 4、無限穿透、**穿牆** |
| 44 | 落星羅盤 | 82 | Normal | `pj2_meteor` 紅 → `fallingMeteor` | `weapon_starcompass.png` | ✅ | Vfx 43 爆炸(小)＋GroundEffect 8 焚地（Any） |
| 45 | 屍毒葫蘆 | 83 | Parabolic | `pj1_blob` 綠 → `corpsePoisonBlob` | `weapon_poisongourd.png` | ✅ | 落地 AreaRadius 1.0＋GroundEffect **10 屍毒雲**（`fanfx2_poison` 綠，補邊成 128×220 讓接地點置中，見 PROBLEMS E42） |
| 46 | 霹靂火彈 | 84 | Parabolic | icon 本身（`Weapon/single/weapon_thunderbomb`） | `weapon_thunderbomb.png` | ❌ 同圖 | 落地 AreaRadius 1.8；Vfx 55 `wills X_plosion` style_a（64 幀隔幀取 32） |
| 47 | 八卦護身印 | 85 | Orbital | `pj3_magic_hex` 紫 → `baguaSeal` | `weapon_baguamirror.png` | ✅ | 4 枚環繞、半徑 1.6 |

- ⚠ **拋物線的落地殺傷半徑＝`AreaRadius × BulletScale`**，擊中特效只吃 VfxTable `Scale`、地面特效只吃 GroundEffectTable `Radius`，三者不連動；放大子彈圖時要把 `AreaRadius` 除回去（屍毒葫蘆 0.24 × 8.4 ≈ 2）。
- 對應資料：WeaponTable／ItemTable 36~47（48 只在 WeaponTable）、RecipeTable 73~85、VfxTable 51~56、GroundEffectTable 10。
- 第一輪盤點時看過、但**沒選**的：鬼頭大刀（`fx2_slash`）、焚身火環（`wills round_vortex`）、魔眼咒（`fanfx3_gaze`）——之後要做可直接從這三個開始。
- 刻意排除：科幻味太重（`pj4_scifi_bomb`、`pj1_toon_missile`、`pj4_scrap_metal`、`scifi_*`）、沒有對應機制（時停／減速／睡眠／吸血）、與現有武器重複（`scifx2_black_hole`≈虛空吞口、`fanfx3_death`≈死字咒）。

## 新增的泛用模式

### `Mode=Melee`

- 以玩家為圓心、瞄準方向為軸。
- `AreaRadius`＝攻擊半徑，`MeleeAngle`＝扇形總角度。
- `HitEffectID` 只播一次方向性揮砍動畫；傷害依 GameObject 去重。

### `Mode=GroundCast`

- 在滑鼠位置生成 `GroundEffectID`。
- `Range` 限制最遠施法距離。
- 半徑、持續時間、DOT 節拍與動畫走 GroundEffectTable；傷害由 WeaponTable 覆寫。

### `Mode=Dash`

- `DashDistance`＝突進距離，`DashWidth`＝障礙探測／掃擊寬度。
- `CircleCast` 遇 Environment 提前停，不穿牆。
- `OverlapCapsuleAll` 覆蓋整段路徑，每個可傷害目標只結算一次。

### `SegmentedColumn`

- 僅搭配 `Mode=SkyStrike`。
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
