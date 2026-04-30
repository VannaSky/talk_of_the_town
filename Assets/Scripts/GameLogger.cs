using UnityEngine;

    public enum LogLevel
    {
        None = 0,
        Error = 1,
        Warning = 2,
        Event = 3,
        Info = 4,
        Verbose = 5,
    }

    public static class GameLog
    {
        // Default level if nothing sets it
        public static LogLevel GlobalLevel { get; set; } = LogLevel.Warning;

        public static void LogError(string category, string message, Object context = null)
        {
            if (GlobalLevel < LogLevel.Error) return;
            Debug.LogError(Format(category, message), context);
        }

        public static void LogWarning(string category, string message, Object context = null)
        {
            if (GlobalLevel < LogLevel.Warning) return;
            Debug.LogWarning(Format(category, message), context);
        }

        public static void LogEvent(string category, string message, Object context = null)
        {
            if (GlobalLevel < LogLevel.Event) return;
            Debug.Log(Format(category, message), context);
        }

        public static void LogInfo(string category, string message, Object context = null)
        {
            if (GlobalLevel < LogLevel.Info) return;
            Debug.Log(Format(category, message), context);
        }

        public static void LogVerbose(string category, string message, Object context = null)
        {
            if (GlobalLevel < LogLevel.Verbose) return;
            Debug.Log(Format(category, message), context);
        }

        static string Format(string category, string message)
        {
            return string.IsNullOrEmpty(category)
                ? message
                : $"[{category}] {message}";
        }
    }