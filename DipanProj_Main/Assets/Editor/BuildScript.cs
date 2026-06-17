using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Diagnostics;
using System.IO;

public class BuildScript
{
    // priority 0 = 最上、最優先；與下方檔案處理類(priority >= 20)差距 > 10 → 自動分隔線。
    [MenuItem("Project Tools/Build and Deploy", false, 0)]
    public static void BuildAndDeploy()
    {
        // 防呆：沒裝 Windows 模組就會建出「有 Managed、沒核心資料」的半成品 _Data，直接擋下並說清楚。
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
        {
            UnityEngine.Debug.LogError(
                $"❌ 這台 Unity（{UnityEngine.Application.unityVersion}）沒有可用的 Windows Build Support 模組，無法產生完整的 Windows 版。\n" +
                $"請到 Unity Hub → Installs → 對「{UnityEngine.Application.unityVersion}」Add Modules → 勾「Windows Build Support (Mono)」，裝完【完全關閉再重開】Unity 再試。");
            return;
        }

        // 打包前先把部署資料夾對齊遠端 main（無條件以遠端為準），確保之後 push 不會因落後而失敗。
        if (!UpdateDeployRepo())
        {
            UnityEngine.Debug.LogError("❌ 部署資料夾同步失敗，已中止打包（先把 DipanProj_Deploy 的 git 弄好再試）。");
            return;
        }

        UnityEngine.Debug.Log("🚀 [1/2] 正在 Unity 內部執行建置...");
        BuildReport report = BuildWindowsWithAutoCleanRetry(out bool dataOk);

        // 必須「成功 + 零錯誤 + _Data 真的含核心資料」三者皆滿足才部署，杜絕半成品。
        if (report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0 && dataOk)
        {
            UnityEngine.Debug.Log($"✅ 建置成功（輸出 {report.summary.totalSize} bytes）！正在呼叫後台同步與推送...");
            ExecuteDeployScript();
        }
        else
        {
            UnityEngine.Debug.LogError(
                $"❌ 建置未完整完成（result={report.summary.result}, errors={report.summary.totalErrors}, dataOk={dataOk}）。" +
                "請看上方各 BuildStep 的紅字找真正原因。已中止部署，不會推出半成品。");
        }
    }

    // 印出整個 BuildReport：每個步驟的錯誤/例外/警告 + 總結。找「為什麼資料沒寫進去」就靠這個。
    private static void DumpBuildReport(BuildReport report)
    {
        var s = report.summary;
        UnityEngine.Debug.Log(
            $"📋 Build 總結: result={s.result}, errors={s.totalErrors}, warnings={s.totalWarnings}, " +
            $"size={s.totalSize} bytes, time={s.totalTime}, output={s.outputPath}");

        int shown = 0;
        foreach (var step in report.steps)
        {
            if (step.messages == null) continue;
            foreach (var m in step.messages)
            {
                if (m.type == UnityEngine.LogType.Error || m.type == UnityEngine.LogType.Exception || m.type == UnityEngine.LogType.Assert)
                {
                    UnityEngine.Debug.LogError($"  ❗[BuildStep: {step.name}] {m.type}: {m.content}");
                    shown++;
                }
                else if (m.type == UnityEngine.LogType.Warning)
                {
                    UnityEngine.Debug.LogWarning($"  ⚠[BuildStep: {step.name}] {m.content}");
                    shown++;
                }
            }
        }
        if (shown == 0)
            UnityEngine.Debug.Log("📋 BuildReport 各步驟沒有 error/warning 訊息（若資料仍不完整，請看 ~/Library/Logs/Unity/Editor.log 的這次 build 區段）。");
    }

    // 直接檢查 _Data 是否含核心資料檔（globalgamemanagers 或 data.unity3d）。沒有 = 資料沒打進去 = 半成品。
    private static bool VerifyDataFolder(string dataDir)
    {
        bool hasCore = File.Exists(Path.Combine(dataDir, "globalgamemanagers"))
                       || File.Exists(Path.Combine(dataDir, "data.unity3d"));
        if (hasCore)
            UnityEngine.Debug.Log($"🔎 _Data 核心資料檔存在 ✅（{dataDir}）");
        else
            UnityEngine.Debug.LogError(
                $"🔎 _Data 缺核心資料檔 ❌（{dataDir} 沒有 globalgamemanagers / data.unity3d）。" +
                "代表 build 在『寫入資料』階段失敗——往上看 BuildStep 紅字。");
        return hasCore;
    }

    // 本機測試用：建 Mac 版直接在這台跑，驗證「專案與資料是否完整」(排除 Windows 模組變數)。
    // priority 1：緊接在 Build and Deploy(0) 下方，與分隔線下的檔案處理類(>=20)分開。
    [MenuItem("Project Tools/Build Mac Local", false, 1)]
    public static void BuildMacLocal()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
        {
            UnityEngine.Debug.LogError("❌ 沒有 Mac Build Support 模組，無法建 Mac 版。");
            return;
        }

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = new[] { "Assets/Scenes/SampleScene.unity" };
        options.locationPathName = "Builds/Mac_Test/DipanProject.app";
        options.target = BuildTarget.StandaloneOSX;
        options.options = BuildOptions.None;

        UnityEngine.Debug.Log("🚀 建 Mac 版（本機測試）...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0)
            UnityEngine.Debug.Log("✅ Mac 版建好：Builds/Mac_Test/DipanProject.app —— 直接雙擊跑跑看。能跑就代表專案/資料完整，問題只出在 Windows 模組。");
        else
            UnityEngine.Debug.LogError($"❌ Mac 版也建置失敗（result={report.summary.result}, errors={report.summary.totalErrors}）。代表問題不只在 Windows 模組，需再往專案挖。");
    }

    // 打包前：跑 update_deploy.sh 把 DipanProj_Deploy 無條件對齊遠端 main。成功回 true。
    private static bool UpdateDeployRepo()
    {
        string scriptPath = Path.Combine(UnityEngine.Application.dataPath, "../../update_deploy.sh");

        ProcessStartInfo startInfo = new ProcessStartInfo("/bin/bash");
        startInfo.Arguments = scriptPath;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;
        startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;   // 修正中文輸出變 ??? 的問題
        startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(output)) UnityEngine.Debug.Log($"🔄 Deploy 同步: {output}");
            if (!string.IsNullOrEmpty(error)) UnityEngine.Debug.Log($"ℹ️ Deploy 同步 (stderr): {error}");

            return process.ExitCode == 0;
        }
    }

    const string WinExe = "Builds/Windows_Test/DipanProject.exe";
    const string WinDataDir = "Builds/Windows_Test/DipanProject_Data";
    const string WinOutDir = "Builds/Windows_Test";

    // 先做正常(增量)打包；若偵測到 _Data 缺核心資料(常見於增量快取沿用了舊的不完整資料)，
    // 自動「清掉輸出 + CleanBuildCache」重建一次。回傳最後一次的 report，dataOk 表示資料是否完整。
    private static BuildReport BuildWindowsWithAutoCleanRetry(out bool dataOk)
    {
        BuildReport report = BuildWindows(false);
        DumpBuildReport(report);
        dataOk = VerifyDataFolder(WinDataDir);

        if (report.summary.result == BuildResult.Succeeded && !dataOk)
        {
            UnityEngine.Debug.LogWarning("⚠ 偵測到 _Data 不完整（多半是增量快取沿用了舊的壞資料）。自動改用 clean build 重建一次...");
            report = BuildWindows(true);
            DumpBuildReport(report);
            dataOk = VerifyDataFolder(WinDataDir);
            if (dataOk) UnityEngine.Debug.Log("✅ clean build 後資料已完整。");
        }
        return report;
    }

    private static BuildReport BuildWindows(bool clean)
    {
        if (clean)
        {
            try { if (Directory.Exists(WinOutDir)) Directory.Delete(WinOutDir, true); }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning($"清除舊輸出失敗（不影響後續）：{e.Message}"); }
        }

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = new[] { "Assets/Scenes/SampleScene.unity" };
        options.locationPathName = WinExe;
        options.target = BuildTarget.StandaloneWindows64;
        options.options = clean ? BuildOptions.CleanBuildCache : BuildOptions.None;
        return BuildPipeline.BuildPlayer(options);
    }

    private static void ExecuteDeployScript()
    {
        // 取得腳本所在的絕對路徑
        string scriptPath = Path.Combine(UnityEngine.Application.dataPath, "../../deploy_only.sh");

        ProcessStartInfo startInfo = new ProcessStartInfo("/bin/bash");
        startInfo.Arguments = scriptPath;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;  // 攔截標準輸出
        startInfo.RedirectStandardError = true;   // 攔截錯誤輸出
        startInfo.CreateNoWindow = true;
        startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;   // 修正中文輸出變 ??? 的問題
        startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

        using (Process process = Process.Start(startInfo))
        {
            // 讀取腳本回傳的文字
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // 🟢 修復：Git 進度通常輸出為 stderr，不應直接報錯
            if (!string.IsNullOrEmpty(output)) UnityEngine.Debug.Log($"📦 Script Output: {output}");
            if (!string.IsNullOrEmpty(error)) UnityEngine.Debug.Log($"ℹ️ Script Info (stderr): {error}");

            if (process.ExitCode == 0)
            {
                if (output.Contains("DEPLOY_RESULT=PUSHED"))
                    UnityEngine.Debug.Log("🎉 部署成功：已推送新版本到遠端 GitHub（測試機 git pull 即可取得）。");
                else if (output.Contains("DEPLOY_RESULT=NOCHANGE"))
                    UnityEngine.Debug.Log("✅ 部署完成：這次成品與遠端相同，無需推送（測試機已是最新，沒有新東西要拉）。");
                else
                    UnityEngine.Debug.Log("🎉 部署流程結束（exit 0）。");
            }
            else
                UnityEngine.Debug.LogError("❌ 部署腳本執行失敗，請查看上方訊息。");
        }
    }
}
