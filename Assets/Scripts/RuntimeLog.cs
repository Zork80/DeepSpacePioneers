using System;
using System.IO;
using UnityEngine;

public static class RuntimeLog
{
    private static readonly object SyncRoot = new object();
    private static string logFilePath;
    private static bool initialized;

    public static string LogFilePath
    {
        get
        {
            if (string.IsNullOrEmpty(logFilePath))
            {
                string logsDirectory = Path.Combine(Application.dataPath, "..", "Logs");
                Directory.CreateDirectory(logsDirectory);
                logFilePath = Path.Combine(logsDirectory, "runtime-log.txt");
            }

            return logFilePath;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        Application.logMessageReceived += OnUnityLog;
        Application.quitting += OnApplicationQuit;

        WriteLine("=== Unity runtime log started ===");
        WriteLine("Log file: " + LogFilePath);
    }

    public static void WriteLine(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        lock (SyncRoot)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            File.AppendAllText(LogFilePath, timestamp + " | " + message + Environment.NewLine);
        }
    }

    private static void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        WriteLine("[" + type + "] " + condition);

        if (!string.IsNullOrEmpty(stackTrace))
        {
            WriteLine(stackTrace);
        }
    }

    private static void OnApplicationQuit()
    {
        WriteLine("=== Unity runtime log ended ===");
    }
}
