namespace DipanMapEditor.Tools
{
    /// <summary>
    /// 當前編輯工具。
    /// 2026-08-10 移除 TilePaint／Erase：本專案的地面一律用整張背景圖 ＋ 地上物，不走地磚路線
    /// （全部 .dipanmap 的 tiles 數量實測為 0，Tiles/ 原始素材夾也是空的）。
    /// </summary>
    public enum EditTool
    {
        Object,     // 地上物：放置/選取/翻轉/縮放/移動
        Walkable,   // 可走/不可走筆刷
        Trigger,    // Trigger 區域：類型/區域/塗格/參數
        SceneFx,    // 場景特效：新增特效、放置起/終點、填 fxId 等參數
        Light,      // 照明：不綁地上物的獨立光源（火炬/燈籠已畫在背景圖時用），放位置＋調半徑/亮度/光色/搖晃
        Cutscene,   // 劇情演出：演員走位/說話/漫畫/運鏡的過場編排
        EffectPreview, // 特效預覽器：瀏覽/輪播 StreamingAssets/Effects 底下整理好的特效（不編輯地圖）
    }
}
