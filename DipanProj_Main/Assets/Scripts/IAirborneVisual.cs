/// <summary>
/// 「這個角色現在視覺上離地多高」的來源（世界單位，0 ＝ 站在地上）。
///
/// <para>為什麼需要這個介面：俯視角 2D 的 Y 軸同時是「畫面高度」與「地面深度」，
/// 要讓角色看起來跳起來，只能把 <c>transform.position.y</c> 往上推——但所有讀 transform 的東西
/// 都會以為牠「往畫面上方走了一步」：
/// <list type="bullet">
///   <item><see cref="BlobShadow"/>：影子跟著飛到半空中（影子應該留在地上，並隨高度縮小變淡）</item>
///   <item><see cref="YSortByFeet"/>：排序基準跟著上移 ⇒ 跳到空中時會被前方的地上物蓋住</item>
/// </list>
/// 所以「抬高了多少」必須報給它們，讓它們把那段高度扣回去。</para>
///
/// <para><b>沒有實作這個介面的角色行為完全不變</b>（兩邊都是「取不到就當 0」），
/// 所以玩家與既有怪物一個像素都不會動。</para>
///
/// 目前唯一的實作是 <see cref="MonsterController"/>（由 <see cref="LeapSlamBrain"/> 在跳躍期間寫入）。
/// 見 readme/BOSS_MODULE.md §9。
/// </summary>
public interface IAirborneVisual
{
    /// <summary>目前視覺離地高度（世界單位）；0 ＝ 在地面上。</summary>
    float AirborneHeight { get; }
}
