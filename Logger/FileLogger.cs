using System;
using System.IO;
using Logger;

namespace Logger;

public class FileLogger : ILogger
{
    private readonly string _logFilePath;

    public FileLogger(string logFilePath)
    {
        _logFilePath = logFilePath;

        // create file if not exist
        if (!File.Exists(_logFilePath))
        {
            File.Create(_logFilePath).Dispose();
        }
    }

    public void Log(string message)
    {
        WriteLog("INFO", message);
    }

    public void LogError(string message, Exception ex = null)
    {
        var errorMessage = ex == null
            ? message
            : $"{message}\nException: {ex.Message}\nStack Trace: {ex.StackTrace}";
        WriteLog("ERROR", errorMessage);
    }

    private void WriteLog(string logType, string message)
    {
        var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logType}] {message}";
        lock (_logFilePath) // ensure thread-safe
        {
            File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
        }
    }
}