using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PyExecutor.Settings;

internal static class PythonScriptRunner
{
    static PythonScriptRunner()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static readonly Encoding GbkEncoding = Encoding.GetEncoding("gbk");
    private static readonly Encoding ScriptEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly string WorkspaceDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassIsland", "HitokotoPythonComponent");

    public static async Task<PythonScriptResult> RunAsync(
        string scriptContent,
        string pythonExecutable,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scriptContent))
        {
            return PythonScriptResult.Failed("脚本内容为空", "请编写一个包含 main() 的脚本。");
        }

        Directory.CreateDirectory(WorkspaceDirectory);
        var scriptPath = Path.Combine(WorkspaceDirectory, $"runner_{Guid.NewGuid():N}.py");
        try
        {
            await File.WriteAllTextAsync(scriptPath, BuildRunnerScript(scriptContent), ScriptEncoding, cancellationToken);

            var interpreter = string.IsNullOrWhiteSpace(pythonExecutable)
                ? PythonComponentSettings.DefaultPythonExecutable
                : pythonExecutable;

            var psi = new ProcessStartInfo(interpreter, $"\"{scriptPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = GbkEncoding,
                StandardErrorEncoding = GbkEncoding,
                WorkingDirectory = WorkspaceDirectory
            };
            psi.Environment["PYTHONIOENCODING"] = "gbk";

            using var process = new Process { StartInfo = psi };
            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                return PythonScriptResult.Failed("无法启动 Python 解释器", ex.Message);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stdoutTask, stderrTask);

            var stdout = (await stdoutTask).TrimEnd('\r', '\n');
            var stderr = (await stderrTask).Trim();

            if (process.ExitCode == 0)
            {
                var message = string.IsNullOrEmpty(stdout) ? "(main() 返回了空字符串)" : stdout;
                return PythonScriptResult.FromSuccess(message);
            }

            var error = string.IsNullOrWhiteSpace(stderr)
                ? $"Python 退出码 {process.ExitCode}"
                : stderr;
            return PythonScriptResult.Failed("脚本执行失败", error);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PythonScriptResult.Failed("执行过程中出现异常", ex.Message);
        }
        finally
        {
            if (File.Exists(scriptPath))
            {
                try
                {
                    File.Delete(scriptPath);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }

    private static string BuildRunnerScript(string scriptContent)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# -*- coding: utf-8 -*-");
        builder.AppendLine("import sys");
        builder.AppendLine("import traceback");
        builder.AppendLine();
        builder.AppendLine("# --- user script begin ---");
        builder.AppendLine(scriptContent);
        builder.AppendLine("# --- user script end ---");
        builder.AppendLine();
        builder.AppendLine("def __classisland_run_main():");
        builder.AppendLine("    if 'main' not in globals():");
        builder.AppendLine("        raise SystemExit('main() 函数未定义')");
        builder.AppendLine("    result = main()");
        builder.AppendLine("    if result is None:");
        builder.AppendLine("        return ''");
        builder.AppendLine("    return str(result)");
        builder.AppendLine();
        builder.AppendLine("if __name__ == '__main__':");
        builder.AppendLine("    try:");
        builder.AppendLine("        sys.stdout.write(__classisland_run_main())");
        builder.AppendLine("    except SystemExit:");
        builder.AppendLine("        raise");
        builder.AppendLine("    except Exception:");
        builder.AppendLine("        traceback.print_exc()");
        builder.AppendLine("        sys.exit(1)");
        return builder.ToString();
    }
}

internal record PythonScriptResult(bool Success, string Message, string Details)
{
    public static PythonScriptResult FromSuccess(string message) => new(true, message, string.Empty);

    public static PythonScriptResult Failed(string message, string details) => new(false, message, details);
}
