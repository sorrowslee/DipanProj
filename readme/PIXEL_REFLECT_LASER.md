# Pack 4 像素反射雷射

## 素材研究

來源為 `DipanProj_MapEditor/Effects/Super Pixel Projectiles Pack 4`。雷射素材分成 A／B 兩種造型，每種都有藍、綠、橙、紅、紫、黃六色，並各自拆成：

- `origin_start / origin_loop / origin_end`：砲口端。
- `center_start / center_loop / center_end`：可延伸的光束中心。
- `impact_start / impact_loop / impact_end`：撞擊端。

目前選用 A 組藍色 loop。A 組中心是乾淨的白熱實心光柱，長距離平鋪和多次折射時仍容易辨識；B 組中心有較密的碎裂能量紋，連續折線較容易顯得雜亂。

遊戲內資源整理為：

```text
Resources/VfxEffects/PixelLaserA_Blue/
├── Origin/Origin_01～08
├── Center/Center_01～08
└── Impact/Impact_01～08
```

三組均為 8 幀、20 FPS。中心段使用 `SpriteRenderer.DrawMode=Tiled` 沿每段折線重複平鋪，因此長度增加不會把像素紋理拉糊。特效匯入器已統一使用 `SpriteMeshType.FullRect`，這是 Unity Tiled Sprite 正常工作的必要條件。

## 武器：鏡界折光

- Weapon／Item ID：29
- Recipe ID：42
- 每跳傷害：2
- 傷害節拍：0.2 秒
- 每秒魔力：2
- `PixelBeamSet=A_Blue`
- `BeamRange=-1`：無限延伸語意
- `BounceTarget=Environment, MaxBounces=3`：由配方精確控制，最多反射 3 次
- `PierceCount=-1`：穿過沿途敵人，讓整條折線都能造成傷害

按住攻擊鍵時，雷射會朝滑鼠方向射出，沿牆面法線反射並繼續延伸。放開後整條雷射消失。這是持續型 `IsLaser` 武器，因此依集氣互斥規則不能開啟 `集氣模式`。

## 實作分工

`LaserBeam` 仍負責 ray-march 路徑、牆面 Raycast／`Vector2.Reflect`、敵人粗線命中、DOT 節拍回報及反射折線 `Points`。

`PixelLaserBeamVisual` 只讀取 `Points` 並負責外觀：第一點放 origin loop、每兩點間平鋪 center loop、最後一點放 impact loop；中間每個反射點另放 72% impact 火花，遮住折角接縫並強化撞牆感。碰撞與傷害不依賴 SpriteRenderer，因此視覺調整不會改變命中結果。

## 射程與反射欄位

`BeamRange=-1` 只代表射程延伸到實用安全距離（內部 200 世界單位），不影響反射次數。反射完全服從 RecipeTable：`BounceTarget=None` 不反射；`BounceTarget=Environment` 才會在牆面反射；`MaxBounces=N` 就最多反射 N 次。「鏡界折光」目前明確設定為 3 次，不再對反射次數使用隱藏上限或 `-1` 特例。

它與武器 5「雷射追蹤光束」走同一條既有雷射生成路徑，因此也完整支援 `SpreadCount`／`SpreadAngle` 多道扇形、`HomingTurnSpeed` 曲線追蹤、`PierceCount` 穿透、`DotInterval` 傷害節拍，以及 `BounceTarget`／`MaxBounces` 反射。像素元件只替換渲染，不接管任何一項配方行為。

## 資料驅動欄位

`WeaponTable.csv` 新增 `PixelBeamSet`：留空沿用原本 shader mesh 雷射；`A_Blue` 則關閉 shader mesh，掛上 Pack 4 A 組藍色像素渲染器。未來增加顏色或 B 組時，只需整理相同的 Origin／Center／Impact 路徑，並在 `PixelLaserBeamVisual.ResolveRoot` 增加素材組名稱，碰撞與反射程式不必重寫。
