using UnityEngine;

/// <summary>
/// 傳給 <see cref="IMonsterBrain"/> 的每幀決策脈絡。把 Brain 可能需要的東西打包成一包，
/// 未來 Brain 要更多能力（自身 HP／階段／武器／技能）只要往這裡加欄位，
/// 不必再改 <c>IMonsterBrain</c> 的簽名或每一個既有 Brain。
///
/// 這是「一隻強怪＝一個 Brain 模組」的共用地基：ChaseBrain 只讀 Actuator/Player，
/// boss 級 Brain（如 <see cref="RedBridalGownBrain"/>）可再讀 Self（拿 WeaponUser 施放技能）。
/// </summary>
public struct MonsterContext
{
    public MonsterController Self;      // 這隻怪的控制器（拿 HP／WeaponUser／狀態）
    public MonsterActuator Actuator;    // 移動器（MoveTowards／Stop）
    public MonsterSensor Sensor;        // 感知器（找玩家、可調 DetectionRange）
    public Transform Player;            // 感測範圍內的玩家；範圍外 = null
    public float DeltaTime;             // 這一幀的 Time.deltaTime（Brain 自管計時用）
}
