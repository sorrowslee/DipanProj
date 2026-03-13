using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Diagnostics;
using System.IO;

public class BuildScript
{
    [MenuItem("Project Tools/Build and Deploy")]
    public static void BuildAndDeploy()
    {
        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = new[] { "Assets/Scenes/SampleScene.unity" };
        options.locationPathName = "Builds/Windows_Test/DipanProject.exe";
        options.target = BuildTarget.StandaloneWindows64;
        options.options = BuildOptions.None;

        UnityEngine.Debug.Log("🚀 [1/2] 正在 Unity 內部執行建置...");
        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            UnityEngine.Debug.Log("✅ 建置成功！正在呼叫後台同步與推送...");
            ExecuteDeployScript();
        }
        else
        {
            UnityEngine.Debug.LogError("❌ 建置失敗，請檢查 Console 報錯。");
        }
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
                UnityEngine.Debug.Log("🎉 部署成功！");
            else
                UnityEngine.Debug.LogError("❌ 部署腳本執行失敗，請查看錯誤 Log。");
        }
    }
}