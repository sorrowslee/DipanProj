namespace DipanMapEditor.Data
{
    /// <summary>
    /// 一盞「獨立光源」——不綁任何地上物，直接放在地圖上的一個點。
    /// 用途：火炬、燈籠這類**已經畫在背景圖裡**的照明物，不需要為了發光把它們從背景拆成地上物，
    /// 只要把光源點放到火焰中心即可。座標為世界座標（同 SceneFxInstance）。
    ///
    /// 另一條路是地上物自己的「發光半徑」（見 ObjectInstance 的照明六欄），
    /// 那條適合「本身就是一個可撿/可破壞的物件」的燈（例：柴房地上的佛燈，撿走光就消失）。
    /// 兩者在遊戲端都會變成 LightSource，餵進同一份光源清單，沒有優劣之分。
    /// </summary>
    public class LightInstance
    {
        /// <summary>系統生成的短 id（保留給日後被觸發鏈參照用；目前沒有人讀）。</summary>
        public string id;

        /// <summary>好認的名字（例：「大廳左火炬」）。純粹給編輯器清單顯示，遊戲端不讀。</summary>
        public string name = "";

        /// <summary>世界座標（放在火焰/燈心的位置）。</summary>
        public float x, y;

        /// <summary>發光半徑（世界單位＝格）：照得到多遠。</summary>
        public float radius = 3f;
        /// <summary>亮度倍率：1＝標準；&lt;1 微光；&gt;1 刺眼。</summary>
        public float intensity = 1f;
        /// <summary>光色（6 碼 16 進位 RRGGBB，不含 #）。空＝預設暖橘。</summary>
        public string color = "";
        /// <summary>搖晃強度：0＝完全不動；1＝標準燭火；2＝狂亂火焰。</summary>
        public float flicker = 1f;
        /// <summary>搖晃速度倍率：小＝油燈慢晃；大＝營火急促跳動。</summary>
        public float flickerSpeed = 1f;
        /// <summary>邊緣柔和度＝內圈(全亮)佔外圈的比例 0~1。小＝柔邊；大＝邊緣硬。</summary>
        public float softness = 0.46f;
    }
}
