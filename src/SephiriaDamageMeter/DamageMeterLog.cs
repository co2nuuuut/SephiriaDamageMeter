using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SephiriaDamageMeter
{
    internal static class DamageMeterLog
    {
        private const string Prefix = "[SephiriaDamageMeter] ";
        private const long MaxLogBytes = 2 * 1024 * 1024;

        private static string _logPath;
        private static readonly object _gate = new object();
        private static readonly Dictionary<string, float> _lastThrottled = new Dictionary<string, float>();

        public static string ModDirectory { get; private set; }

        public static void Initialize()
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                ModDirectory = string.IsNullOrEmpty(assemblyPath)
                    ? Path.Combine(Application.dataPath, "..", "AddOns", "SephiriaDamageMeter")
                    : Path.GetDirectoryName(assemblyPath);
                if (string.IsNullOrEmpty(ModDirectory))
                {
                    ModDirectory = Path.Combine(Application.dataPath, "..", "AddOns", "SephiriaDamageMeter");
                }
                _logPath = Path.Combine(ModDirectory, "damagemeter.log");
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length > MaxLogBytes)
                {
                    File.Delete(_logPath);
                }
            }
            catch
            {
                _logPath = null;
            }
        }

        public static void Info(string message)
        {
            Debug.Log(Prefix + message);
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(Prefix + message);
            Write("WARN", message);
        }

        public static void Error(string message)
        {
            Debug.LogError(Prefix + message);
            Write("ERROR", message);
        }

        public static void ErrorThrottled(string key, string message, float seconds)
        {
            float now = Time.realtimeSinceStartup;
            float last;
            if (_lastThrottled.TryGetValue(key, out last) && now - last < seconds)
            {
                return;
            }
            _lastThrottled[key] = now;
            Error(message);
        }

        private static void Write(string level, string message)
        {
            if (string.IsNullOrEmpty(_logPath))
            {
                return;
            }
            try
            {
                lock (_gate)
                {
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message + Environment.NewLine;
                    File.AppendAllText(_logPath, line);
                }
            }
            catch
            {
                // Logging must never break gameplay.
            }
        }
    }
}
