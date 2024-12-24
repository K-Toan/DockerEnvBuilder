namespace Logger;

public class ConsoleLogger : ILogger
{
    public void Log(string message)
    {
        Console.WriteLine($"[INFO] {DateTime.Now}: {message}");
    }

    public void LogError(string message, Exception ex = null)
    {
        Console.WriteLine($"[ERROR] {DateTime.Now}: {message}");
        if (ex != null)
        {
            Console.WriteLine($"[EXCEPTION] {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
