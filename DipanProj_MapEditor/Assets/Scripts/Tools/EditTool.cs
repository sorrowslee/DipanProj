namespace DipanMapEditor.Tools
{
    /// <summary>當前編輯工具。後續會再擴充 trigger。</summary>
    public enum EditTool
    {
        TilePaint,  // 畫地磚
        Erase,      // 擦地磚
        Object,     // 地上物：放置/選取/翻轉/縮放/移動
        Walkable,   // 可走/不可走筆刷
        Trigger,    // Trigger 區域：類型/區域/塗格/參數
        SceneFx,    // 場景特效：新增特效、放置起/終點、填 fxId 等參數
    }
}
