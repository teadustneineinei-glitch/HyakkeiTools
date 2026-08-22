using System.IO;

namespace Hyakkei.Core;

/// <summary>极简文件日志，写入程序目录 logs/。日志失败不影响主流程。</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "logs");

    public static void Info(string message) => WriteLine("INFO", message);

    public static void Error(string message, Exception? ex = null)
        => WriteLine("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void WriteLine(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                var file = Path.Combine(Dir, $"app-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 忽略日志写入失败
        }
    }
}
