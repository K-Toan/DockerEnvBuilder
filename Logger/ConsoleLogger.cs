namespace Logger;

public class ConsoleLogger : ILogger
{
    public void Log(string message)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"[INFO] {DateTime.Now}: {message}");
    }

    public void LogWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARNING] {DateTime.Now}: {message}");
    }

    public void LogError(string message, Exception ex = null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {DateTime.Now}: {message}");
        if (ex != null)
        {
            Console.WriteLine($"[EXCEPTION] {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
