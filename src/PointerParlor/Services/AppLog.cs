namespace PointerParlor.Services;

/// <summary>A small append-only log in %LocalAppData%\PointerParlor\Logs, rolled at 1 MB. Never logs file contents.</summary>
public static class AppLog
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly Lock Gate = new();
    private static string? _path;

    public static void Initialize(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        _path = Path.Combine(logDirectory, "PointerParlor.log");
    }

    public static void Info(string message) => Write("INFO ", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        if (_path is null)
            return;
        lock (Gate)
        {
            try
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
                    File.Move(_path, _path + ".1", overwrite: true);
                File.AppendAllText(_path, $"{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ} {level} {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // Logging must never break the app.
            }
        }
    }
}
