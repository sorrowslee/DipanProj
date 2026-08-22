using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DipanMapEditor.Data
{
    /// <summary>
    /// Trigger 類型定義集合，對應 triggerTypes.json（資料驅動、編輯器內可增刪）。
    /// 新增一種 trigger 筆刷 = 加一筆 TriggerTypeDef（含參數 schema）。
    /// 出生點（玩家/怪物）也是這裡的一種類型。
    /// </summary>
    public class TriggerTypeSet
    {
        public List<TriggerTypeDef> types = new List<TriggerTypeDef>();

        public TriggerTypeDef Find(string typeId) => types.Find(t => t.typeId == typeId);

        /// <summary>內建預設類型（首次無檔時生成）。</summary>
        public static TriggerTypeSet Defaults()
        {
            var set = new TriggerTypeSet();
            set.types.Add(new TriggerTypeDef
            {
                typeId = "teleport", displayName = "傳送點", color = "#33AAFF",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "entranceId",     type = ParamType.String },
                    new TriggerParam { key = "targetMapId",    type = ParamType.Int },
                    new TriggerParam { key = "targetEntrance", type = ParamType.String },
                    new TriggerParam { key = "showMarker",     type = ParamType.Bool, boolDefault = true, label = "使用傳送點外型" },
                    new TriggerParam { key = "linkedFx",       type = ParamType.String, label = "連動特效id" },   // 場景特效 id：停用時隱藏（綠幕）、解鎖時顯示
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                typeId = "pickup", displayName = "道具拾取點", color = "#FFCC33",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "itemId", type = ParamType.String },
                    new TriggerParam { key = "count",  type = ParamType.Int },   // 一次給幾個（留空 = 1）
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                typeId = "playerSpawn", displayName = "玩家出生點", color = "#33FF88",
                paramSchema = new List<TriggerParam>()
            });
            set.types.Add(new TriggerTypeDef
            {
                typeId = "monsterSpawn", displayName = "怪物出生點", color = "#FF5555",
                paramSchema = new List<TriggerParam>
                {
                    // 怪物 id：填單一 id（例 5），或填多個以 '|' 分隔（例 5|7|9）＝每隻各自從中隨機挑一種生。
                    new TriggerParam { key = "monsterId", type = ParamType.String, label = "怪物id('|'分隔)" },
                    // 死亡觸發旗標：這個出生點生的怪死亡時把此旗標設為 true（給觸發鏈 requireFlag 用，例：殺家人→killedFamily）。
                    // isFlagRef＝用旗標登記表選（輸入 id→確認），不手打。每個出生點各自設定；空＝不寫。
                    new TriggerParam { key = "deathFlag", type = ParamType.String, label = "死亡觸發旗標", isFlagRef = true },
                    // 重複產生：留空/0＝進圖生一次（原行為）；填秒數＝每隔這麼久生一波（一波＝這個區域每格各生一隻）。
                    // ⚠ 重複產生的怪「不記關卡進度」（殺光會再補），但照常掉寶；靠下面的同時存在上限節制。
                    new TriggerParam { key = "spawnInterval", type = ParamType.Float, label = "重複間隔秒" },
                    // 同時存在上限：這個出生點生的怪還活著幾隻 ≥ 上限時，那一波就少生/不生（死了才補）。留空＝10。
                    new TriggerParam { key = "maxAlive", type = ParamType.Int, label = "同時存在上限" },
                    // 「什麼時候才開始生」不另外開欄位——直接用下方的**通用條件欄位**：
                    //   條件旗標＝持續判定（旗標取消就暫停、恢復就繼續，配「開關(按F)」用這個）；
                    //   初始停用＋解鎖旗標＝一次性解鎖（要靠鏈 Activate，解鎖後不會再關）；
                    //   周目／道具／完成關卡數條件也一併有效。見 readme/TRIGGER_CHAIN.md §3.5。
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                typeId = "drama", displayName = "劇情觸發點", color = "#AA66FF",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "dramaId", type = ParamType.Int },   // 對應 DramaTable.csv 的 ID
                    // 可略過：頭像對話（DramaTable Type=2）播放時，右上角出現統一樣式的 Skip，按下＝略過整組對話、
                    // 直接接這個 trigger 的 next（沒有 next 就是關閉）。**只有一句的群組不會出現**（按 Skip 等於按下一句）。
                    // 預設允許；不想讓玩家跳過的關鍵劇情才取消勾選。
                    new TriggerParam { key = "canSkip", type = ParamType.Bool, label = "可略過(顯示Skip)", boolDefault = true },
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                typeId = "cutscene", displayName = "事件/過場觸發點", color = "#22CC99",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "cutsceneId",     type = ParamType.String },   // 哪段過場（目前任意值＝穿隧道）
                    new TriggerParam { key = "video",          type = ParamType.String },   // 過場後要播的影片（mp4，之後接）
                    new TriggerParam { key = "targetMapId",    type = ParamType.Int },      // 過場完傳送到的地圖
                    new TriggerParam { key = "targetEntrance", type = ParamType.String },   // 目標地圖的落點
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：被觸發鏈啟動時「直接給物品進背包」（不用按 F、不綁位置；格子畫在角落即可）。
                typeId = "giveItem", displayName = "給予物品(鏈動作)", color = "#FFE08A",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "itemId", type = ParamType.String },
                    new TriggerParam { key = "count",  type = ParamType.Int },   // 留空 = 1
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：被觸發鏈啟動時「直接傳送」（不用踩傳送點、不綁位置；格子畫在角落即可）。
                typeId = "teleportTo", displayName = "直接傳送(鏈動作)", color = "#7FD4FF",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "targetMapId",    type = ParamType.Int },
                    new TriggerParam { key = "targetEntrance", type = ParamType.String },
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 傳送門互動點：玩家靠近按 F → 開啟「放劇本」傳送門 UI；linkTeleport 指向按下開啟後要解鎖的傳送點。
                typeId = "portal", displayName = "傳送門互動", color = "#FF8844",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "linkTeleport", type = ParamType.String, label = "要開的傳送點名" },
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                typeId = "camZone", displayName = "鏡頭區", color = "#66CCFF",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "zoom",    type = ParamType.Float },   // 縮放倍率（>1 拉遠、<1 拉近、留空=1）
                    new TriggerParam { key = "offsetX", type = ParamType.Float },   // 鏡頭右(+)/左(-)位移（世界單位）
                    new TriggerParam { key = "offsetY", type = ParamType.Float },   // 鏡頭上(+)/下(-)位移（世界單位）
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：被觸發鏈（next）啟動時「飄鏡頭到自己這格中心＋壓黑幕、停留、再拉回」，表演完才接 next。
                // 格子畫在要對準的地方（例如傳送門正中間一格）。不用玩家踩，純靠鏈驅動。
                typeId = "cameraFocus", displayName = "鏡頭聚焦(鏈動作)", color = "#88DDFF",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "holdSeconds", type = ParamType.Float, label = "停留秒數" },   // 鏡頭到位後停留幾秒（留空=1.6）
                    new TriggerParam { key = "dim",         type = ParamType.String, label = "黑幕樣式",
                                       options = new [] { "中央留洞", "整片全黑", "無" } },
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：被觸發鏈（next）啟動時，在玩家頭上「左上／右上」各擺一張提示圖（左右位置固定、不用填 XY），
                // 指定張閃爍；到「收起時機」（玩家移動/攻擊/按任意鍵）自動收，收完接 next。做「只一次」用通用旗標欄
                // （完成寫旗標=永久:xxx、條件旗標=!永久:xxx）。圖放 Resources/UI/Common/，填檔名（不含副檔名）。
                typeId = "playerHint", displayName = "玩家提示(鏈動作)", color = "#FFD24D",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "leftImage",  type = ParamType.String, label = "左圖(檔名)" },   // 左上那張（例 Guide_Wasd）；留空＝不顯示
                    new TriggerParam { key = "rightImage", type = ParamType.String, label = "右圖(檔名)" },   // 右上那張（例 Guide_Press）；留空＝不顯示
                    new TriggerParam { key = "flashLeft",  type = ParamType.Bool,   label = "左圖閃爍", boolDefault = false },
                    new TriggerParam { key = "flashRight", type = ParamType.Bool,   label = "右圖閃爍", boolDefault = true },
                    new TriggerParam { key = "hideOn",     type = ParamType.String, label = "收起時機",
                                       options = new [] { "移動", "攻擊", "任意鍵" } },
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：被觸發鏈（next）啟動時，播這張圖在「劇情」分頁排好的那段演出，演完才接 next。
                // 用途：劇情演出原本只能「一進圖自動播」，而自動播每次進圖都會重播一次、沒有一次性機制。
                // 把該段演出的「一進圖自動播」關掉、改放這顆 trigger，就能用觸發鏈的
                // 條件旗標／重複規則（關卡單次·每次·每周目·永久）／周目上下限 來管這段劇情播不播、播幾次。
                // 不用玩家踩、純靠鏈驅動；格子畫在角落即可。cutsceneId 留空＝播第一段（目前一張圖只有一段）。
                typeId = "playCutscene", displayName = "播放劇情(鏈動作)", color = "#FFB0E0",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "cutsceneId", type = ParamType.String, label = "演出id(留空=第一段)" },
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：被觸發鏈（next）啟動時，就地播一次性全螢幕過場特效（依 effectId 分派，如 1=破幻術「幻境崩碎回歸現實」），
                // 暫停＋擋操作、播完才接 next（通常 next = 直接傳送(teleportTo) 把玩家傳去現實地圖，如榕樹妖）。
                // 不用玩家踩、純靠鏈驅動；格子畫在角落即可。effectId 旁的「螢幕特效表」按鈕可查/填可用的 id。
                typeId = "playScreenFx", displayName = "播放螢幕特效(鏈動作)", color = "#D98CFF",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "effectId", type = ParamType.Int,   label = "螢幕特效id", isScreenEffectRef = true },   // 1=破幻術；點旁邊「螢幕特效表」看清單
                    new TriggerParam { key = "duration", type = ParamType.Float, label = "特效秒數" },   // 覆寫該特效總長（留空＝該特效預設；破幻術預設 2.2）
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：被觸發鏈（next）啟動時，把 target 指定的「傳送點」隱藏封鎖或顯示解鎖（含外型/綠幕與踩踏功能）。
                // Boss 房封門用：onEnter→對話→togglePortal(target=房間傳送點, show=off) 進門封門；打贏後 Boss 死亡旗標鏈接 show=on 復原。
                // 不用玩家踩、純靠鏈驅動；格子畫在角落即可。target 填傳送點的「名稱」（在該傳送點的 名稱 欄設）。
                typeId = "togglePortal", displayName = "開關傳送點(鏈動作)", color = "#5AC8B0",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "target", type = ParamType.String, label = "傳送點名稱", isPortalList = true },   // 可多筆：按「＋」加欄；每欄填一個傳送點「名稱」
                    new TriggerParam { key = "show",   type = ParamType.Bool,   label = "顯示(否=隱藏)", boolDefault = false },   // 打勾=顯示解鎖；不打勾=隱藏封鎖（預設）
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：被觸發鏈（next）啟動時開「選擇劇本」面板（邪佛發牌）——擺出關卡卡片，玩家按「領取」拿走某張劇本
                // → 加進背包 → 關閉 → 接 next。取代原本「邪佛對話後直接 giveItem 給紅嫁衣劇本」那一步。
                // 初始關卡＝只填一張紅嫁衣：scriptIds="104"、specialIds="104"（特殊裂紋框）。
                // 日後多關＝scriptIds 填該輪要擺的多張劇本 id（'|' 分隔），業障回響的舊關 id 放進 specialIds。
                // 不用玩家踩、純靠鏈驅動；格子畫在角落即可（0 格也可）。
                typeId = "selectScript", displayName = "選擇劇本(鏈動作)", color = "#C0464C",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "scriptIds",  type = ParamType.String, label = "可領取劇本id('|'分隔)" },   // 例：104（初始紅嫁衣）；多關用 '|' 分隔
                    new TriggerParam { key = "specialIds", type = ParamType.String, label = "特殊框劇本id('|'分隔)" },   // 其中要用裂紋框的（業障回響/紅嫁衣），例：104
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 位置型：玩家走到這些格子上按 F 就開啟指定的 UI 面板。
                // 祭壇抽選＝在祭壇「前方可站的地板」畫幾格，panelId 填 gacha、arg 填抽選池代號（武器/裝備/血統/道具…）。
                // ⚠ 祭壇本身是地上物（walkable=false 會擋路），所以感應格一定要包含祭壇前方站得到的地板，
                //    否則玩家走不進感應範圍——與儲藏室藥水櫃同一個坑（見 readme/STOREROOM_POTION_TUTORIAL.md）。
                // 刻意做成通用的「開啟介面」而不是「祭壇」專用，之後商店/鐵匠/圖鑑都能共用同一種筆刷。
                typeId = "openPanel", displayName = "開啟介面(按F)", color = "#7ED957",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "panelId", type = ParamType.String, label = "面板", options = new[] { "gacha" } },
                    new TriggerParam { key = "arg",     type = ParamType.String, label = "參數(抽選池代號)" },   // gacha＝GachaPoolTable.csv 的 PoolId，例：weapon / armor / blood / item
                    new TriggerParam { key = "tipName", type = ParamType.String, label = "提示文字後綴(選填)" },  // 例填「抽選」→ 顯示「按 F 鍵抽選」；留空只顯示「按 F 鍵」
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 位置型：玩家走到這幾格附近按 F ＝ 切換一個旗標（開→關→開…），不開任何面板、不給任何東西。
                // 誰在看這個旗標由對方決定：怪物出生點的「條件旗標」、地上物的 appearFlag/disappearFlag、
                // 其他 trigger 的條件旗標…都行。第一次開啟時還會跑自己的「完成寫旗標／接續觸發」，所以也能當一般機關用。
                // ⚠ 開關本身的圖是**地上物**（拉桿/石碑/按鈕），感應格要畫在它前方玩家站得到的地板（同祭壇，見 PROBLEMS K1）。
                typeId = "switch", displayName = "開關(按F)", color = "#4DD9C0",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "toggleFlag", type = ParamType.String, label = "切換旗標", isFlagRef = true },
                    new TriggerParam { key = "tipOff",     type = ParamType.String, label = "未啟動提示" },   // 留空＝「開始」→ 顯示「按 F 鍵開始」
                    // 已啟動時的提示：**留空＝一次性開關**（按下去就收掉、不能再關）；有填＝可反覆切換（例「暫停」）。
                    new TriggerParam { key = "tipOn",      type = ParamType.String, label = "已啟動提示" },
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：把某個物品「永久」加進某個抽選池（跨輪迴保留），之後在對應祭壇就抽得到。
                // 典型：紅嫁衣 boss 死亡旗標 → watchFlag → next 接這顆（pool=blood、entry=302 幽靈血統藥劑）。
                // 不用玩家踩，格子畫在角落或用「＋ 手動新增空區域」建 0 格即可。重複觸發不會重複加。
                typeId = "unlockRoll", displayName = "解鎖抽選內容(鏈動作)", color = "#59C7B0",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "pool",  type = ParamType.String, label = "抽選池代號" },        // GachaPoolTable.csv 的 PoolId
                    new TriggerParam { key = "entry", type = ParamType.String, label = "物品id('|'分隔)" },   // 可一次解鎖多個
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：被觸發鏈（next）啟動時播「Boss 開戰資訊」表演——暫停遊戲 → 螢幕中央 Warning 特效 →
                // 左側滑入 boss 頭像（Talk 立繪）、右側滑入姓名牌匾＋顯示名，停留後淡出、接 next。
                // 顯示名/頭像填在主專案 MonsterData.csv 的 DisplayName / PortraitPath 欄，這裡只填怪物 ID（與怪物出生點同一個 ID）。
                // 不用玩家踩、純靠鏈驅動；格子畫在角落即可（0 格也可）。典型：boss 房 onEnter→對話→bossIntro→togglePortal 封門。
                typeId = "bossIntro", displayName = "Boss開戰資訊(鏈動作)", color = "#FF4D6A",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "monsterId", type = ParamType.Int, label = "怪物ID(MonsterData)" },
                    new TriggerParam { key = "warnVfxId", type = ParamType.Int, label = "警告特效id(空=14)" },
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 進場觸發：進入這張地圖、載入完全結束（含進場效果播完）後自動觸發一次，
                // 自己不做事、只當觸發鏈的起點（接續觸發 next 指向要做的事，例如純鏈節點的劇情觸發點）。
                // 不用塗格子：用「＋ 手動新增空區域」建立（0 格），從區域清單選取設參數即可。
                // 一次性用「重複規則＝每周目/永久」或「周目上限」控制；預設每次進這張圖都會觸發（重複規則＝關卡單次）。
                typeId = "onEnter", displayName = "進場觸發(自動)", color = "#FF66AA",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "delaySeconds", type = ParamType.Float, label = "延遲秒數" },   // 進場（效果播完）後再等幾秒才觸發（留空=0）
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 觀察旗標變動（自動）：監聽指定旗標，該旗標「首次成立(false→true)」時觸發自己的「接續觸發(next)」。
                // 本身不做事、只當「旗標驅動的鏈起點」（同進場觸發 onEnter，但改由旗標驅動）。用「＋手動新增空區域」建立（0 格）。
                // 典型：怪物出生點「死亡觸發旗標」= X → 本 trigger fireOnFlag=X → next 接對話/動畫/給獎勵…→ 最後接「過關(結算)」。
                // 注意：是「旗標翻成 true 的那一刻」才觸發；若要「進場時旗標已成立就觸發」請改用 進場觸發 + 條件旗標。
                typeId = "watchFlag", displayName = "觀察旗標變動", color = "#7FE0A0",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "fireOnFlag", type = ParamType.String, label = "監聽旗標(成立即觸發)", isFlagRef = true },
                }
            });
            set.types.Add(new TriggerTypeDef
            {
                // 動作型：被「接續觸發(next)」呼叫到就播「延時倒數（玩家可自由操作、上方顯示倒數）→ 卍字離場 → 通關結算 → 返回廣場」
                // 流程並記過關（見主專案 GameFlowManager.EndLevel）。**旗標偵測已抽到「觀察旗標變動(watchFlag)」**，這裡純鏈動作。
                // 典型：boss 死亡旗標 → watchFlag → next 接對話/動畫…→ 最後接本 trigger。也可「踩點過關」：畫格子讓玩家踩到即過關。
                // 玩家死亡走同一結算但標題自動改「殞命」（程式端，不需擺 trigger）。
                typeId = "clearLevel", displayName = "過關(結算)", color = "#FFC24D",
                paramSchema = new List<TriggerParam>
                {
                    new TriggerParam { key = "delaySeconds", type = ParamType.Float, label = "延時觸發(空=2秒)" },   // 倒數幾秒才進結算（玩家這段可自由操作/撿戰利品）；留空＝2 秒
                }
            });
            return set;
        }

        /// <summary>
        /// 觸發鏈通用欄位（**每種類型都有**，編輯器在類型參數下方統一顯示；遊戲端 TriggerChain 解讀）：
        ///   next          完成後啟動的 trigger（填同地圖另一個 trigger 的名稱或 id）
        ///   startDisabled 初始停用（等鏈解鎖才生效；配 enableFlag 可跨存檔記住已解鎖）
        ///   enableFlag    解鎖狀態旗標名（解鎖時寫 1；重進地圖旗標成立就自動啟用）
        ///   requireFlag   旗標成立才可觸發；前綴 "!" 表否定（例 "!killedFamily"）
        ///   setFlag       完成後寫 1 的旗標名
        ///   requireCycleMax 周目 ≤ 此值才成立（初始限定填 1；留空=不限）
        ///   requireCycleMin 周目 ≥ 此值才成立（老手限定用；留空=不限）
        ///   requireItem     背包道具條件：填 itemId=「須有此道具」；前綴 "!"（如 !104）=「須無此道具」；留空=不檢查
        ///   repeat          重複規則：關卡單次(預設)/每次/每周目/永久，見主專案 readme/TRIGGER_CHAIN.md
        /// 以上所有條件（含 requireFlag）以 AND 結算，全成立才觸發。旗標名可加 "永久:" 前綴＝跨輪迴保存。
        /// </summary>
        public static readonly List<TriggerParam> ChainParams = new List<TriggerParam>
        {
            // ── 條件（可不可以觸發）──
            new TriggerParam { key = "requireFlag",     type = ParamType.String, label = "條件旗標",   group = "條件", isFlagRef = true, flagNegatable = true },
            new TriggerParam { key = "requireCycleMax", type = ParamType.Int,    label = "周目上限",   group = "條件" },
            new TriggerParam { key = "requireCycleMin", type = ParamType.Int,    label = "周目下限",   group = "條件" },
            new TriggerParam { key = "requireItem",     type = ParamType.String, label = "道具條件",   group = "條件" },
            // 完成關卡數條件：與「地上物出現條件（完成 N 關）」是同一個概念、同一組範圍值，
            // 讓「祭壇的圖」與「祭壇的按 F 感應區」可以用一模一樣的條件，不會出現「圖沒出來但按得到」。
            new TriggerParam { key = "requireClearsMin",   type = ParamType.Int,    label = "最低完成關卡數", group = "條件" },
            new TriggerParam { key = "requireClearsMax",   type = ParamType.Int,    label = "最高完成關卡數", group = "條件" },
            new TriggerParam { key = "requireClearsScope", type = ParamType.String, label = "關卡數範圍",     group = "條件",
                               options = new [] { "cycle", "lifetime" } },
            // 條件不成立時要不要把棒子交給 next（預設維持原本行為＝整條鏈中止）。
            // 「初次限定的對話」卡在鏈中間時一定要選「跳過這顆繼續」，否則後面的發劇本會一起被吃掉。
            new TriggerParam { key = "onBlocked",          type = ParamType.String, label = "條件不成立時",   group = "條件",
                               options = new [] { "中止整條鏈", "跳過這顆繼續" } },
            // ── 一次性（會不會重複觸發）──
            new TriggerParam { key = "repeat",          type = ParamType.String, label = "重複規則",   group = "一次性",
                               options = new [] { "關卡單次", "每次", "每周目", "永久" } },
            // ── 流程（觸發後做什麼）──
            new TriggerParam { key = "next",            type = ParamType.String, label = "接續觸發",   group = "流程" },
            new TriggerParam { key = "setFlag",         type = ParamType.String, label = "完成寫旗標", group = "流程", isFlagRef = true },
            // ── 解鎖（位置型：等鏈解鎖）──
            new TriggerParam { key = "startDisabled",   type = ParamType.Bool,   label = "初始停用",   group = "解鎖" },
            new TriggerParam { key = "enableFlag",      type = ParamType.String, label = "解鎖旗標",   group = "解鎖", isFlagRef = true },
        };
    }

    public class TriggerTypeDef
    {
        public string typeId;
        public string displayName;
        public string color = "#FFFFFF";   // 編輯器上的塗刷顏色

        [JsonProperty("params")]
        public List<TriggerParam> paramSchema = new List<TriggerParam>();
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ParamType { String, Int, Float, Bool }

    public class TriggerParam
    {
        public string key;
        public ParamType type = ParamType.String;
        public string label;             // 面板顯示用（空＝顯示 key）
        public bool boolDefault = false; // Bool 參數新建區域時的預設勾選狀態
        public string group;             // 面板分組小節標題（同組連續顯示在同一節；空＝不分組）
        public string[] options;         // 有值時＝以「循環按鈕」在這幾個選項間切換（避免打錯字），取代文字輸入
        public bool isFlagRef;           // 此欄的值是「旗標名」→ 面板改用旗標登記表下拉選（不手打）
        public bool flagNegatable;       // 旗標欄可否定（條件旗標）：加「有/沒有」切換，沒有＝存成 "!名字"
        public bool isScreenEffectRef;   // 此欄的值是「螢幕特效 id」→ 面板加「螢幕特效表」按鈕開參照清單（可查/填 id）
        public bool isPortalList;        // 此欄是「可多筆」字串清單 → 面板渲染成多欄、按「＋」加一欄、「−」刪一欄；存成逗號分隔字串
    }
}
