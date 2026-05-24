namespace RogKeyMini.Logging;

using System.IO;

public sealed class LogService
{
    private readonly string _logPath;
    private readonly object _gate = new();

    public LogService()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(directory);
        _logPath = Path.Combine(directory, "rog-key-mini.log");
    }

    public string LogPath => _logPath;

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception is null
            ? message
            : $"{message}{Environment.NewLine}{exception}";

        Write("ERROR", fullMessage);
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";

        lock (_gate)
        {
            File.AppendAllText(_logPath, line);
        }
    }
}
