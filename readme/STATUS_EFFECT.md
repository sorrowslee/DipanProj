# 狀態效果（buff／debuff／DoT／控制）

> 返回 [文件總覽](README.md)
>
> **狀態**（分支 `feat/status-effect`，2026-11-18 開工；分六階段，每段做完停下讓作者 Play 驗收，見 §9）：
> 第①～③階段（分類、框架核心、圖示與呈現）**已編譯、⏳ 未實測**；④～⑥ 未動工。
>
> 相關：傷害管線 [COMBAT.md](COMBAT.md)；效果分類與部位、全身等級相加 [EQUIPMENT.md](EQUIPMENT.md)；珠子 [GEM_SOCKET.md](GEM_SOCKET.md)；
> 頭目欄位 `Controllable` [MONSTER_SETUP.md](MONSTER_SETUP.md)；shader 特效 [DIPAN_VFX.md](DIPAN_VFX.md)。

---

## 0. 一句話

buff、debuff、DoT、HoT、控制**是同一個東西**：有時限、會疊加、掛在一個單位身上的「狀態」。差別只在 `StatusEffectTable.csv` 那一列怎麼填。
施加一律走 **`StatusSystem.Apply`**；誰身上有什麼在 **`StatusController`**（玩家與每隻怪物都掛，沒狀態時停用、零成本）。
**玩家能對怪上、怪也能對玩家上**（作者 2026-11-18 定：雙向）。

---

## 1. 分類與第一批效果

| 欄 | 值 | 意義 |
|---|---|---|
| `Kind` | Buff／Debuff | 狀態列放哪一列、外框顏色。**DoT 算 Debuff**（作者：buff 一列、debuff＋DoT 一列） |
| `Category` | Stat／Dot／Hot／Control | 數值修正／持續傷害／持續回復／控制 |
| `Attribute` | Outgoing／Incoming／MoveSpeed；Stun／Freeze | Stat 改哪個乘數；Control 是哪一種 |

第一批 11 種（作者 2026-11-18 同意；數值是初版，平衡大後期再調，真相是 `Assets/Data/StatusEffectTable.csv`）：

| ID | 名稱 | 類 | 疊加 | 秒 | 等級→強度 | 裝備上怎麼來（第④階段） |
|---|---|---|---|---|---|---|
| 1 | 燃燒 | Dot | 只留最強 | 4 | 每秒 25%＋10%/lv 命中傷害（固定值型 20＋10/lv） | 攻擊類：命中時施加 |
| 2 | 中毒 | Dot | 疊層（10 層） | 5 | 每層每秒 8%＋4%/lv（固定 10＋5/lv） | 攻擊類：命中時施加 |
| 3 | 減速 | Stat 移速 | 刷新 | 3 | −20%−5%/lv（lv13＝−80% 封頂） | 攻擊類：命中時施加 |
| 4 | 易傷 | Stat 受傷 | 刷新 | 5 | ＋10%＋5%/lv | 攻擊類：命中時施加 |
| 5 | 虛弱 | Stat 造成傷害 | 刷新 | 5 | −10%−5%/lv（lv17＝−90%） | 攻擊類：命中時施加 |
| 6 | 暈眩 | Control | 刷新 | 1 | **觸發機率** 10%＋5%/lv | 攻擊類：命中時施加（鎖移動＋攻擊，見 §2.8） |
| 7 | 冰凍 | Control | 刷新 | 1.5 | 同上 | 同上 |
| 8 | 增傷 | Stat 造成傷害 | 刷新 | 4 | ＋10%＋5%/lv | 攻擊類：**命中時**給自己（Nidhogg 用**發射時**） |
| 9 | 護體 | Stat 受傷 | 刷新 | 4 | −10%−5%/lv | 防禦類：**受擊時**給自己 |
| 10 | 疾行 | Stat 移速 | 刷新 | 3 | ＋15%＋5%/lv | 輔助類（鞋子）：**發射時**給自己 |
| 11 | 回春 | Hot | 刷新 | 5 | 每秒回最大血量 1%＋0.5%/lv | 防禦類：**受擊時**給自己 |

---

## 2. 運作規則（作者 2026-11-18 拍板）

### 2.1 等級代表什麼
- **有強度的效果，等級加強度**：DoT 每秒傷害、debuff／buff 的百分比、HoT 回血量。DoT 與 debuff **命中必定上身**，持續時間固定在表上。
- **沒有強度的效果，等級加機率**：暈眩、冰凍——命中必定觸發的話，高射速武器會把雜兵永遠鎖死。
- 等級曲線同 GemTable：`Lv1 ＋（等級−1）× PerLv`，超過 `MaxLv` 照 `MaxLv` 算。

### 2.2 同一種再上身（`Stack` 欄）
| 值 | 規則 | 用在 |
|---|---|---|
| `Refresh` | 只有一個；時間重設、等級取高（DoT 每秒傷害也取高） | 大部分 |
| `Stack` | 每層各自計時、強度相加，最多 `MaxStack` 層；滿了換掉最快到期那層 | 中毒 |
| `Strongest` | 只留最強的；新的不比現在弱就整個換掉（含時間），較弱就忽略 | 燃燒 |
| `Extend` | 剩餘時間加上 Duration，最多 Duration×`MaxStack`；等級取高 | （預留） |

分裂／平行／連擊的**每一顆子彈命中都會施加**；疊層型靠 `MaxStack` 封頂。

### 2.3 DoT
- **全遊戲共用 0.5 秒時鐘**（`StatusSystem.TickInterval`）：同一隻身上同一跳的多種 DoT **加總成一個數字**（作者：之後會有大批怪物，省效能最重要）。數字小一號、顏色取傷害最大那種的代表色。
- **每秒傷害在施加當下決定**：
  - **百分比型**：命中那一下的**原始武器傷害**（含銳利，未過加成／減傷）× 表上百分比；
  - **固定值型**：表上 `FlatLv1／FlatPerLv`（已是 ×10 尺度）。
  - **哪一種由武器設定**（作者：不是寫死，兩種都要支援；配方欄在第④階段加）。怪物、地面、劇本、作弊面板施加的一律固定值型。
- 每跳走 `CombatSystem.Apply`（`DamageInfo.IsStatusTick=true`）：**照吃受擊方減傷、易傷、護體**；
  **不觸發無敵時間、不閃白、不擊退、不跳一般傷害數字、不播三階血統擊中特效、不問艾爾文的閃避**，也**不再吃施加者的攻擊加成**（施加當下已算進去）。
  不這樣做的話，每 0.5 秒一跳的燃燒會一直觸發無敵，把玩家的武器傷害擋掉（作者同意）。
- 地面特效的週期傷害**不是**狀態 DoT，行為不變（仍是一般命中＋`IsDot`）。

### 2.4 數值修正怎麼算
- 同一個乘數的所有狀態**相加**後套一次：造成傷害 `max(0, 1＋Σ)`、受到傷害 `max(0.1, 1＋Σ)`、移速 `clamp(1＋Σ, 0.2, 3)`。
- 跟裝備層（銳利、`CombatStats` 加成／減傷、怪物 `DamageReduction`）**相乘**，不相加：兩層各自好算，之後請 AI 做數值平衡也直觀。
- 乘數只在狀態增減時重算並快取；戰鬥中每一下命中只是讀欄位。
- 移速乘在**實際速度**，不改 `MoveSpeed`——`MoveSpeed` 同時是走路動畫的 1 倍速基準（PROBLEMS／`SetMoveSpeed` 註解），所以減速時走路動畫自然變慢。

### 2.5 頭目
- **頭目免疫所有行動控制**（作者：含減速）＝ MonsterData `Controllable` 填 0。免疫＝控制類，以及會讓移動變慢的數值修正。DoT、易傷、虛弱**照吃**。
- 2026-11-18 四隻頭目都已填 0：芙蓉（紅嫁衣女殭屍 13）、榕樹妖（14）、艾爾文、夏綠蒂（38～45 原本就是 0）。**以後新增頭目記得填 0**。
- 被免疫時頭上跳「免疫」（每隻最快每秒一次，機槍打頭目不會洗版）。
- ⚠ `Controllable` 的意思是「免疫行動控制」，**不是「打不到」**——索敵別拿它過濾（PROBLEMS **F48**）。

### 2.6 生命週期
- **換圖保留、直到時效結束**；**離開關卡全部清空**（作者定）。玩家跨圖不重生，所以狀態掛在玩家身上自然保留；清空點在 `RunProgress.BeginRun`／`EndRun`（過關、死亡、中途離開都會走）。
- 死亡時清空。**不存檔**（同 HP／MP）。
- 計時用遊戲時間：暫停面板開著時一起停。

### 2.7 不分施加者
單人遊戲：同一種狀態全部合併計算，只記「最後施加者」（DoT 一跳的陣營判斷用——兩族互打的演戲傷害 1/100 照樣成立）。

### 2.8 被控制（暈眩、冰凍；第⑤階段，2026-11-18 已做 ⏳ 未實測）
- **鎖移動、鎖攻擊**（作者定）。玩家**也不能喝藥**（作者：先不行，之後看狀況調）——快捷鍵、背包右鍵都擋，跳「被控制時不能喝藥」（語言 12043），藥不會被扣。
- **冰凍**：動作**定格**在被凍的那一幀（`MonsterAnimator.Paused`／`PlayerAnimator.Paused`）。**暈眩**：正在揮的那一下當場收掉、站回 idle；怪物頭上是漩渦圖示（玩家看底部狀態列）。
- 怪物：在 `MonsterController.Update` 總入口攔（同骨牢的做法，不進各 Brain）——Brain 不想、速度每幀歸零（擊退也被吃掉）、**接觸傷害也停**（`EnemyContactDamage` 查攻擊者的 `StatusController`）。Brain 的各階段都有時間保底，解除後自己接得回去。
- 玩家：在 `PlayerController.Update` 攔；剛被控制那一幀收掉集氣、連擊補射、雷射／佛光、攻擊姿勢；被動武器（浮游等）也一起停。發射冷卻照走。
- **控制類沒有任何特例**（2026-11-18 晚作者定，原則：不要隱性規則，不然未來算難度會遺漏）：照表上的 `Stack`（＝Refresh）走——控制中再中同一種＝時間重設；**暈眩與冰凍可以同時存在、各自計時**，兩個都在時畫面以冰凍定格為準，冰凍先到期就變回暈眩（收招站著）。AI 原本自定的「控制中不疊、暈眩中被冰凍也不換」已拿掉。
- ~~解除後 2 秒免疫控制~~：**2026-11-18 晚作者拿掉**——「不需要隱性規則，不然未來在計算難度時會有遺漏」；玩家過強改由**機率％上限**控制（之後平衡時定）。這條是 AI 在第⑤階段自己加的、事前沒問作者。
- 頭目（`Controllable=0`）根本不會上身（跳「免疫」）。

---

## 3. 效果三分類與部位（作者 2026-11-18 定）

| 部位 | 能有的類別 | GemTable `Target` |
|---|---|---|
| 武器 | 攻擊 | `Recipe`／`Weapon` |
| 胸甲、手套 | 防禦（作者糾結過手套算防禦還是輔助，定為防禦） | `Player`（也可寫 `Defense`） |
| 鞋子 | 輔助 | `Support` |
| 護身符、戒指 | 三類都可以 | — |

生成與鑲嵌同一套規則（`ItemQuality.ClassAllows`／`CheckSocket`）。程式：`GemData.IsAttackClass／IsDefenseClass／IsSupportClass`、`EquipClass` 多了 `Boots`、`SocketBlock.WrongClassBoots`（語言表 4020）。珠身預設色：攻擊紅、防禦藍、輔助綠（藍、綠 lv1~3 已在 `gemBase/`，2026-11-18 程式換色）。

---

## 4. 施加來源與觸發時機（第④階段，2026-11-18 已做 ⏳ 未實測）

- **所有東西都要能拆成珠子**（專案核心做法）：狀態效果是 GemTable 的**純等級型特殊效果**（`Calc=Level`、`Attribute=Status`、`StatusID` 指到狀態表）。
  配方 `StatusLv` 填起點等級，裝備內建效果、珠子、血統 `Effects` 加等級，**全身等級相加**後的總等級＝給出去的狀態等級（再查狀態表的強度；暈眩／冰凍加的是機率）。
  所以「寫在武器上」跟「做成珠子」是同一件事，也會算進品質（特殊效果數）、進生成骰池。
- **觸發時機屬於效果本身**：同一個 buff 不同時機＝不同效果、不同珠子（例「命中增傷」「發射增傷」）。
- **給誰**：狀態表 `Kind=Buff` 給自己；`Debuff` 給「對方」（命中＝被打的；發射／擊殺沒有對方 ⇒ 不作用；受擊目前也拿不到打我的人）。
- **buff 防常駐**：GemTable `Chance`／`Cooldown`（每種效果各自計時，真的上身才開始冷卻）。DoT／減益留空＝必定上身。

### 4.1 第一批 12 種效果（GemTable 28–39、能力珠 ItemTable 428–439）

| GemID | 效果 | 時機 | 給 | 機率／冷卻 | 部位類別 | 珠子 |
|---|---|---|---|---|---|---|
| 28–34 | 燃燒、中毒、減速、易傷、虛弱、暈眩、冰凍 | 命中 | 敵人 | 必定（暈眩／冰凍的機率在狀態表） | 攻擊 | 焚燒、蝕毒、遲滯、破甲、衰勁、震魂、凝霜 |
| 35 | 命中增傷 | 命中 | 自己 | 20%／5 秒 | 攻擊 | 蓄勢 |
| 36 | 發射增傷 | 發射 | 自己 | 100%／無（每發刷新） | 攻擊 | 熾鋒 |
| 37 | 受擊護體 | 受擊 | 自己 | 20%／8 秒 | 防禦 | 金鐘 |
| 38 | 受擊回春 | 受擊 | 自己 | 20%／8 秒 | 防禦 | 回春 |
| 39 | 受擊疾行 | 受擊 | 自己 | 20%／8 秒 | 輔助 | 疾風 |

> 2026-11-18 晚 作者實測後改：鞋子的疾行改成**受擊**觸發（原本發射時）；受擊三種都加 **20% 機率**（冷卻維持 8 秒，疾行比照）。

珠子符號：GPT 金色浮雕（2 張表、2 次額度），`gems/gemIcons/gemIcon_<burn|poison|slow|vuln|weak|stun|freeze|might_hit|might_fire|guard|regen|haste>.png`。

### 4.2 四個觸發點（`StatusAttacker`）

| 時機 | 誰呼叫 | 規則 |
|---|---|---|
| 命中 OnHit | `CombatSystem.Apply` | 傷害**確實打進去**（目標呼叫 `CombatSystem.MarkHitLanded`；無敵時間、閃避、劇本同伴都不算）；**狀態 DoT 一跳不算**；DoT 百分比型用這一下的**原始武器傷害**（`DamageInfo.BaseAmount`） |
| 擊殺 OnKill | `CombatSystem.Apply` | 這一下讓怪物從活變死；**狀態 DoT 打死的也算**（施加者記在狀態上） |
| 發射 OnFire | 玩家 `Shoot`（真的射出去；連擊每發都算）、持續型武器開始放＋每秒扣魔各一次；怪物 `MonsterWeaponUser.TryUse` 成功 | |
| 受擊 OnHurt | 玩家 `TakeDamage` 確實扣到血 | 狀態 DoT 一跳不算 |

### 4.3 其他來源（「狀態清單」直接上身，DoT 一律固定值）

| 來源 | 欄位 | 說明 |
|---|---|---|
| 武器配方 | RecipeTable `StatusLv`（效果清單）、`StatusDmgCalc`（Percent／Flat，空＝Percent） | 所有模式都吃（召喚除外）。玩家：跟全身相加；怪物武器：配方就是總等級 |
| 血統 | BloodlineTable `Effects`（第 33 欄，效果清單） | 目前只有 **Nidhogg（72）＝`發射增傷:1`**；熔岩爆 `AttackVfxId=39` 不動 |
| 怪物攻擊 | MonsterData `ContactStatus`（索引 47，狀態清單） | 接觸、揮擊、咬擊、踐踏、自爆、武器——**這隻怪的所有攻擊**命中都給 |
| 地面特效 | GroundEffectTable `StatusFx`（狀態清單） | 每次傷害順便給 |
| 藥劑 | ItemTable `UseStatus`（第 21 欄，狀態清單） | 只有 `Category=Potion` 會喝；喝下給玩家 |
| 觸發鏈 | `applyStatus`／`clearStatus`（target＝player／monsters，status＝狀態清單；清除留空＝全部） | 立即接 next。見 TRIGGER_CHAIN |

**兩種清單**（`StatusSpec`，格式 `名稱:等級;名稱:等級`，等級省略＝1，全形「：；」也可以）：
- **效果清單**查 GemTable 的狀態效果列（有觸發時機）：配方 `StatusLv`、血統 `Effects`。
- **狀態清單**查 StatusEffectTable（名稱／英文 Key／ID 都認）：其餘來源。

### 4.4 攻擊者歸屬
怪物揮擊／踐踏／咬擊／自爆是另生的 `ImpactDamageArea`：`Spawn` 多了 `owner` 參數（6 個呼叫點都傳那隻怪），存在 `EnemyContactDamage.Owner`。
`StatusAttacker.ResolveAttacker` 把傷害來源換回攻擊者 ⇒ 怪物身上的增傷／虛弱對這些攻擊也有效了、`ContactStatus` 也能從揮擊給出去。

## 5. 呈現（第③階段，2026-11-18 已做 ⏳ 未實測）

**圖示**（作者看過測試圖後定：純符號、不畫器物）：
- 11 個全部放在**同一張貼圖** `Resources/UI/Icons/Status/status_atlas.png`（4×4 格、每格 128px）＋索引 `status_atlas_index.txt`，`StatusIcons.Get(def)` 切出 Sprite。
  為什麼不一個檔一張：怪物頭上的 SpriteRenderer **同貼圖才會自動合批**；專案沒開 Sprite Atlas 資產，所以自己合。
- 名稱＝表上 `Icon` 欄。重產圖後：切圖放 `TempImage/status_icons/work/<key>_new.png`，改 `tools/build_atlas.py` 的 `ICONS` 對照再跑一次。
- 減速、虛弱、增傷、護體各有一個**備選**（`alt_*`），預覽在 `TempImage/status_icons/out/icons_preview.png`，要換就改 `ICONS` 那一行。

**玩家狀態列** `UI/StatusBarView.cs`（建在 `BottomHudPanel` 底下，HUD 隱藏時一起消失）：
- 畫布座標（1920×1080）離螢幕底 **270＝debuff＋DoT 列、318＝buff 列**（HUD 中央蓮花頂約在 244），各自由中間往兩側排；每格 40px、間距 6。常數在檔頭。
- 每格：極簡細邊外框（buff 暗金 `#B08A55`、debuff 深緋紅 `#9A241C`）＋深底＋圖示＋**已經過的時間蓋暗角**（Radial360）＋層數（＞1 才顯示）。
- 滑鼠移上去：`HoverTip` 一行「燃燒 Lv3：每秒受到 40 點傷害（剩餘 2.5 秒）」（`DescId` 的 `{0}`＝強度、`{1}`＝層數）。
- 只在狀態增減時重排；每幀只更新轉圈，而且只在身上有狀態時。

**身上**（`Combat/Status/StatusVisuals.cs`，第一次有狀態時才掛；**有東西要跟著動才開 LateUpdate**）：
- **同時有好幾種減益時只染一種、不混色**：DoT ＞ 控制 ＞ 數值減益，同一級取**先上身的那個**（例：先中毒再燒起來＝維持綠）。作者 2026-11-18 看過說可以（另提過輪流閃色、混色，沒採用）。
- **染色**：SpriteFlash shader 新增 `_StatusTint`／`_StatusTintAmount`，經 `HitReactionHandler.SetStatusTint` 寫入（那個 renderer 的 MPB 唯一寫入點）。單色化後乘狀態色、再墊 0.06 底亮度（近黑角色純乘色看不出來）。強度 0.35。**只看減益**：DoT ＞ 控制 ＞ 數值減益；增益不染（Nidhogg 一直在上增傷，不該一直是橘的）。
- **燃燒**：**不放火、只靠染色**（作者 2026-11-18 看過後定：玩家與怪物都不要身上的火）。
- **中毒**：從身上往上冒綠色小泡（共用 `VfxEmbers`，全場上限 400 顆）。傷害類型＝Poison 的 DoT 都會有。
- 其他狀態只靠染色＋圖示。暈眩、冰凍另有動作上的表現（冰凍定格、暈眩收招站著，§2.8），沒有另做身上特效。

**怪物頭上圖示**（`StatusVisuals`，只有怪物）：
- 世界空間 SpriteRenderer、**不用 Canvas**（每次變動要重建網格，怪多會卡）；每隻用到才建、之後重用。
- 頭頂上方 0.22、每個 0.34 單位、最多 4 個；減益排前面。層數用 TextMesh（＞1 才顯示）。sortingOrder 23900（角色之上、傷害數字 24000 之下）。
- 圖示根物件**不掛在怪物底下**（怪物的 Scale 會把圖示一起放大），位置每幀跟著 `FeetWorldPos＋VisibleBodyHeight`（PROBLEMS **G13**）；怪物銷毀時一起收。
- ⏳ 50～100 隻怪的實測放在第⑥階段。

---

## 6. 資料表 `Assets/Data/StatusEffectTable.csv`

一列一種狀態；表頭括號就是欄位說明。重點：

- `Lv1`／`PerLv` 用百分比寫（`10%`、`-20%`），不帶 % 就當小數。Stat 的負數＝減益。
- `FlatLv1`／`FlatPerLv` 只有 DoT 固定值型用。
- 名稱與說明走 LanguageTable **12001–12099**（名稱 12001～、說明 12021～、「免疫」12041、「剩餘 {0} 秒」12042、「被控制時不能喝藥」12043）。
- 掛載：MainScene `GemTableProvider.statusEffectCSV`（已掛）；編輯器裡找不到 provider 時直接讀資產。
- 其他表的狀態欄位（GemTable 的 `StatusID／Trigger／Chance／Cooldown`、RecipeTable、BloodlineTable、MonsterData、GroundEffectTable、ItemTable）見 §4。

---

## 7. 程式結構（`Assets/Scripts/Combat/Status/`）

| 檔 | 內容 |
|---|---|
| `StatusEffectData.cs` | enum（Kind／Category／Stack／Attribute）、`StatusEffectDef`、`StatusEffectDatabase`（懶載入、`LoadFromText`） |
| `StatusController.cs` | 單位身上的狀態：疊加、0.5 秒共用時鐘、到期、三個快取乘數、`GetViews`（UI／作弊面板）、`Changed`／`AnyChanged` 事件 |
| `StatusSystem.cs` | **唯一施加入口** `Apply`（算 DoT 每秒傷害、免疫字）、`ControllerOf`、`ClearPlayer` |
| `StatusAttacker.cs` | 觸發器（第④階段）：四個時機 `NotifyHit／Fire／Kill／Hurt`、機率＋冷卻、`ResolveAttacker`（傷害圈 → 那隻怪） |
| `StatusSpec.cs` | 清單語法 `名稱:等級;…`：`ParseEffects`（查 GemTable 狀態效果列）／`ParseStatuses`（查狀態表），結果快取 |
| `StatusIcons.cs` | 圖示貼圖＋索引 → Sprite（快取）；`White`（轉圈用） |
| `StatusVisuals.cs` | 染色、燃燒的火、中毒的泡、怪物頭上圖示 |
| `UI/StatusBarView.cs`（namespace Dipan.UI） | 玩家狀態列 |

接點（其他檔案改動）：
- `DamageInfo.IsStatusTick`、`CombatSystem.CurrentHitIsStatusTick`、`CombatSystem` 步驟 2b（狀態乘數）。
- `MonsterController.TakeDamage`／`PlayerController.TakeDamage`：`CurrentHitIsStatusTick` 時跳過受擊反應與一般數字。
- `MonsterController.Status`／`StatusMoveMultiplier`／`Heal()`；Start 掛 `StatusController`。`PlayerController.Status`；Start 掛。
- 移速：`PlayerController.FixedUpdate`、`MonsterActuator.SpeedMultiplier`（StatusController 寫入）、`FlyingBrain.FlyTowards`。
- `DamageNumberManager.ShowDot`／`ShowHeal`；`RunProgress.BeginRun／EndRun` → `StatusSystem.ClearPlayer`；`PlayModeStaticReset`。
- 第④階段：`GemData`（`StatusId`／`Trigger`／`Chance`／`Cooldown`、`IsStatusEffect`、`Describe`）；`RecipeEntry.StatusLv／StatusDmgFlat`＋`WeaponModeSpec` 兩個通用欄；
  `CombatSystem`（`MarkHitLanded`、命中／擊殺通知、步驟 2b 用真正的攻擊者）；`PlayerController`（`Shoot` 包一層 `ShootCore`、`DrainContinuousMana`、`TakeDamage`、`SyncStatusAttacker`、`StatusTriggers`）；
  `MonsterController`（`MarkHitLanded`、`ContactStatus`）、`MonsterWeaponUser`（配方 StatusLv、發射通知）；`EnemyContactDamage.Owner`、`ImpactDamageArea.Spawn(owner)`；
  `BloodlineDef.Effects`、`MonsterData.ContactStatus`、`GroundEffectData.StatusFx`、`ItemData.UseStatus`；`ItemUse`、`GroundEffectInstance`、`TriggerChain.ExecuteStatus`；地圖編輯器 `TriggerType.cs`＋`triggerTypes.json`；`tools/scripting/validate_map.py`。

---

## 8. 作弊面板驗收（L →「狀態」分頁）

- 「給玩家」「給附近怪物（玩家 8 格內所有敵對怪）」兩組按鈕；「等級」按鈕在 Lv1／3／5／10 之間切換；兩顆清除鈕。
- 下方文字列出玩家目前的狀態與三個乘數，第一行是**觸發效果**（全身相加後生效的狀態效果，例「發射增傷（增傷 Lv1）」）。
- 第⑤階段起暈眩、冰凍也有按鈕，**略過機率必中**。2026-11-18 晚修：控制類的特例（解除後免疫、控制中不再上）都已依作者拿掉，暈眩和冰凍可以同時開；燃燒（只留最強）按較低等級也照換。結果字改成中文（上身／原本就有，改成常駐／免疫／沒上身）。
- **按鈕是開關**（2026-11-18 晚，作者：暈眩 1 秒，關掉面板就結束了，連 tip 都來不及看）：按下＝**常駐**（`StatusController` 的 Pinned，時間無限、狀態列不轉圈、tip 顯示「常駐（測試用）」語言 12044），按鈕變綠、名字前加「●」；再按一次＝移除。玩家那組照玩家身上實際狀態亮燈（死亡、清除、離開關卡都會自己對上）；怪物那組記住開的時候上到哪些怪，關的時候連同現在附近的一起拿掉。
- **狀態測試裝備**（第⑥階段）：蓮華鱗甲（胸甲，ItemTable 504＝受擊護體）、青芽護手（手套，505＝受擊回春）、風翎行靴（鞋子，506＝受擊疾行），良品、內建效果寫死 Lv3、6 孔全開（規格 `fx=37@3;s=6` 等）。圖示 GPT 產（1 次額度），`UI/Icons/Equipment/armor_lotusChest`／`gloves_jadeSprout`／`boots_windWing`。**只有作弊面板拿得到**（沒進任何掉落／兌換／抽選池）。
- **效能壓測**（第⑥階段）：「壓測：生 50／100 隻」在玩家四周生三圈小怪（2～12 號輪流、血量 10 萬），下一幀每隻上燃燒＋中毒 3 層＋減速＋易傷＋虛弱，並自動打開右上角「效能顯示」（左上角是既有的 P 鍵效能面板 PerfHud，兩個可以同時開）（最近 1 秒平均／最差幀時間、**每幀 GC 配置 KB**、怪物數、有狀態的數量）。「清掉壓測怪」一鍵殺光。編輯器裡的數字偏差，準確請看 Profiler 或 Development Build。
  - **自動記錄**（2026-11-18 晚，作者：自己抄容易抄錯）：「開始記錄」／「開始記錄（掃射中）」→ 關掉面板玩 10 秒以上 → 打開面板按「結束記錄（寫檔）」。
    只記遊戲在跑的幀（面板開著＝暫停不算；關面板後頭 0.5 秒也不算），結束時**追加一列**到 `TempImage/perf_logs/perf_log.csv`（打包版在 persistentDataPath）：
    標籤（自動＝開始時的怪物數，掃射版加「＋掃射」）、秒數、幀數、平均／中位／P95／P99／最差 ms、超過 33／50 ms 的幀數、主執行緒平均 ms（編輯器與 Dev Build 才有）、
    GC 平均 KB／幀、GC 單幀最大、有 GC 的幀百分比、平均怪物數／有狀態數／被控制數、CPU、執行環境、解析度、VSync。AI 直接讀這個檔判斷，不用作者抄。
  - **怎麼量、怎麼判斷**：同一張圖、同一個站位，量四組各看 10 秒、記「平均／最差／GC」：Ⓐ 不生怪（基準）Ⓑ 生 50 Ⓒ 生 100 Ⓓ 生 100 並拿射速最快的武器掃。
    判斷：平均 ≤ 16.7 ms（60 FPS）合格、≤ 33 ms（30 FPS）勉強；最差偶爾跳到 2 倍可接受、常常跳＝有卡頓；**GC 每幀最好接近 0**，持續好幾 KB 代表每幀都在 new 東西、過一陣子會 GC 卡一下。
    重點看「Ⓒ 減 Ⓐ」的差（狀態系統＋100 隻怪多花多少），再跟 Ⓓ 比（命中觸發、傷害數字的成本）。要細看時開 Profiler（CPU Usage → Hierarchy，按 Total 排序）截前 15 列。
  - **2026-11-18 第一次實測**（作者 M2 Pro、Unity 編輯器、1865×932）：

    | 組 | 平均 ms | 中位 | P99 | 最差 | GC KB／幀 | 有狀態怪 |
    |---|---|---|---|---|---|---|
    | Ⓐ 0 隻 | 0.79 | 0.71 | 3.79 | 12.4 | 10.5 | 0 |
    | Ⓑ 50 隻 | 1.81 | 1.61 | 4.10 | 19.3 | 14.6 | 21 |
    | Ⓒ 100 隻 | 2.75 | 2.58 | 5.47 | 8.6 | 19.9 | 44 |
    | Ⓓ 100 隻＋掃射 | 3.14 | 2.93 | 5.29 | 10.3 | 20.0 | 42 |

    判讀：100 隻多花約 2 ms（每隻約 0.02 ms，含怪物本身的 AI／動畫，不只狀態），掃射命中＋觸發＋數字再多 0.4 ms；四組都沒有超過 33 ms 的幀。離 16.7 ms 預算很遠，**現階段不需要優化**，傷害數字物件池也先不做。
    GC：基準就有 10.5 KB／幀（含編輯器、量測用的 OnGUI 面板本身），100 隻多 ~9 KB／幀；有空再用 Profiler 的 GC Alloc 欄找來源，不急。
    ⚠ 狀態 4～5 秒就到期，10 秒的記錄裡平均只有四成的怪身上有狀態；要量「100 隻全程帶狀態」可以生怪後用怪物那組的常駐開關把狀態全打開再記。
- 「狀態珠各給一顆（Lv3）」：12 顆狀態珠（第④階段）。⚠「鑲嵌」分頁的「每種能力珠各給一顆」也會一起給這 12 顆（它掃全部珠子），背包可能塞不下。
- DoT 用固定值型。
- 面板開著時遊戲暫停，**關掉面板才看得到效果**。

---

## 9. 實作階段

| 階段 | 內容 | 狀態 |
|---|---|---|
| ① | 開分支、本文件、三類分類、四隻頭目 `Controllable` 補 0（順手修浮游武器打不到頭目，F48） | ✅ 已編譯 ⏳ 未實測 |
| ② | 狀態表、`StatusController`、乘數接 `CombatSystem`、狀態 DoT 不觸發受擊反應、DoT 數字合併、移速、生命週期、作弊面板 | ✅ 已編譯 ⏳ 未實測 |
| ③ | GPT 產 icon（11 個＋4 個備選，2 次額度；先前測試 2 次）、藍／綠珠身（程式換色，0 額度）、玩家狀態列、怪物頭上 icon、染色、中毒綠泡（燃燒只染色） | ✅ 已編譯 ⏳ 未實測 |
| ④ | 配方 `StatusLv`／`StatusDmgCalc`、GemTable 12 種狀態效果＋12 顆珠子（GPT 金色符號 2 次額度）、四種觸發時機（機率＋冷卻）、怪物攻擊／怪物武器／地面／藥劑／觸發鏈施加、血統 `Effects`＋Nidhogg、傷害圈追回攻擊者 | ✅ 已編譯（含地圖編輯器）⏳ 未實測 |
| ⑤ | 暈眩、冰凍行為：鎖移動＋攻擊（怪物含接觸傷害）、冰凍定格／暈眩收招、玩家不能喝藥（解除後 2 秒免疫已依作者拿掉） | ✅ 已編譯 ⏳ 未實測 |
| ⑥ | 三件測試裝備（504～506，GPT icon 1 次額度，只放作弊面板）、壓測工具（生 50／100 隻＋效能顯示）、效能修正（刷新不重算、DoT 數字預算、攻擊者只解析一次） | ✅ 已編譯 ⏳ **實測要作者在 Unity 跑** |

---

## 10. 已知限制

- ~~怪物揮擊等傷害圈沒記施加者~~：第④階段已補（§4.4）。
- **受擊時的減益效果沒有對象**：`TakeDamage` 拿不到打我的人，第一批受擊效果都是 buff 所以沒影響；以後要做「受擊時讓攻擊者燃燒」得把攻擊者帶進來。
- **被動武器（浮游、應龍水珠）命中也會觸發玩家的命中效果**（傷害來源都是玩家本人），百分比型 DoT 用被動武器那一下的傷害；配方 `StatusLv` 只算目前武器欄那把。
- 怪物 `ContactStatus` 對**這隻怪的所有攻擊**都生效（接觸、揮擊、武器分不出來）。
- 地面特效 `StatusFx`、觸發鏈、藥劑給的狀態沒有施加者（DoT 打死不算任何人的擊殺）。
- `PounceBrain` 撲擊時被減速，撲出的距離會變短（截止時間沒乘減速）。`LeapSlamBrain` 的跳躍是演出、不吃減速。
- 被控制的玩家頭上沒有漩渦／冰的圖示（只有底部狀態列有）；怪物有。
- **效能（第⑥階段）**：
  ① 同一個狀態被刷新、層數與等級都沒變（高射速武器的常態）⇒ 不重算乘數、不重畫圖示、不發事件（狀態列的轉圈每幀自己讀剩餘時間）。
  ② 狀態 DoT／回春的數字有**全場預算**：每 0.5 秒最多 24 個（`StatusSystem.TickNumberBudget`），**畫面外的怪不跳**，玩家自己的一定跳；傷害照算。
  ③ `CombatSystem.Apply` 的攻擊者只解析一次。
  ④ ⚠ **傷害數字系統本身沒有物件池**（每個數字新建 2 個 GameObject＋TextMesh，播完 Destroy）——大量怪物時武器命中的數字也會是負擔。這是原有系統，沒動；壓測若卡在這裡再改成物件池。
- 突進（Dash）衝到一半被控制，那一段衝刺不會被打斷。
