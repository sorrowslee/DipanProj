using UnityEngine;
using Dipan.UI;
using Dipan.Localization;

namespace Dipan.Flow
{
    /// <summary>
    /// 劇本中離開的警告（作者拍板 2026-11-06）。
    ///
    /// 劇本在**傳送門按開啟那一刻就消耗了**（見 ScriptsPanel.OnStartPressed），所以在劇本中途離開
    /// （設定面板「返回廣場」／「離開遊戲」、視窗 X、Alt+F4）都要先跳警告：「劇本已消耗，離開後就無法再進入」。
    ///
    /// ‧ 設定面板的兩顆鈕：自己判斷 <see cref="InScript"/> 換文案（SettingsPanel）。
    /// ‧ 視窗 X／Alt+F4／任何 Application.Quit：靠 <see cref="Application.wantsToQuit"/> 攔下來改跳確認框，
    ///   玩家按確認才走 <see cref="QuitNow"/> 真的關。⚠ 只在打包版有效；Unity 編輯器按停止 Play 不會經過這裡。
    /// ‧ 過關、死亡不警告（過關是正常完成；死亡沒得選）。
    ///
    /// 字串走 LanguageTable（7001／7002）。
    /// </summary>
    public static class QuitGuard
    {
        /// <summary>LanguageTable：劇本中按「返回廣場」的確認文案。</summary>
        public const int ReturnHubWarningTextId = 7001;
        /// <summary>LanguageTable：劇本中「離開遊戲」（含視窗 X／Alt+F4）的確認文案。</summary>
        public const int QuitWarningTextId = 7002;

        static bool _allowQuit;

        /// <summary>
        /// 是否正在劇本中——劇本已消耗、離開就算放棄。兩種情況：
        ///   ‧ 已經進到關卡裡（RunProgress.RunActive）；
        ///   ‧ 在廣場按了開門、還沒走進傳送門（<see cref="ScriptsPanel.PortalOpenedFlag"/>）——劇本也已經扣掉了。
        /// </summary>
        public static bool InScript =>
            (RunProgress.Exists && RunProgress.Instance.RunActive)
            || TriggerChain.FlagTrue(ScriptsPanel.PortalOpenedFlag);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            _allowQuit = false;   // 編輯器關掉 Domain Reload 時靜態值會殘留，每次 Play 重設
            Application.wantsToQuit -= OnWantsToQuit;
            Application.wantsToQuit += OnWantsToQuit;
        }

        /// <summary>確認後真的關遊戲（放行 wantsToQuit）。設定面板的「離開遊戲」確認也走這支。</summary>
        public static void QuitNow()
        {
            _allowQuit = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;   // 編輯器內：停止 Play 方便測試
#else
            Application.Quit();
#endif
        }

        static bool OnWantsToQuit()
        {
            if (_allowQuit || !InScript) return true;
            var ui = UIManager.Instance;
            if (ui == null) return true;                        // 沒有 UI 可問 → 不擋，避免關不掉
            if (!ui.IsOpen<ConfirmPopup>())                     // 連按 X 不要疊好幾個彈窗
                ConfirmPopup.Show(Language.GetText(QuitWarningTextId), QuitNow);
            return false;
        }
    }
}
