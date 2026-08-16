using System;

namespace testownikUE.Services;

public static class AppLog
{
    private static void Write(string level, string source, string message, Exception? exception = null)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        Console.WriteLine($"[{timestamp}] [{level}] [{source}] {message}");

        if (exception != null)
            Console.WriteLine(exception);
    }

    public static void Info(string source, string message) => Write("INFO", source, message);

    public static void Warn(string source, string message) => Write("WARN", source, message);

    public static void Error(string source, string message, Exception? exception = null)
        => Write("ERROR", source, message, exception);

    public static void Debug(string source, string message)
    {
#if DEBUG
        Write("DEBUG", source, message);
#endif
    }
}

