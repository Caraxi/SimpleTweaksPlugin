using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace SimpleTweaksPlugin; 

public sealed class SimpleLog {
    [PluginService] private static IPluginLog PluginLog { get; set; } = null;

#if DEBUG
    private static int _subStrIndex;
    public static void SetupBuildPath([CallerFilePath] string callerPath = "") {
        var p = Path.GetDirectoryName(callerPath);
        if (p != null) _subStrIndex = p.Length + 1;
    }

    private static string CleanCallerPath(string callerPath = "", string callerName = "", int lineNumber = -1) {
        if (string.IsNullOrEmpty(callerPath) && string.IsNullOrEmpty(callerName) && lineNumber == -1) return string.Empty;
        var sb = new StringBuilder();
        sb.Append('[');
        if (!string.IsNullOrWhiteSpace(callerPath) && callerPath.Length >= _subStrIndex) {
            sb.Append(callerPath.Substring(_subStrIndex));
            sb.Append("::");
        }

        sb.Append($"{callerName}:{lineNumber}");
        sb.Append("] ");
        return sb.ToString();
    }
    
    public static void Verbose(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.Verbose($"{CleanCallerPath(callerPath, callerName, lineNumber)}{m}");
    }

    public static void Debug(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.Debug($"{CleanCallerPath(callerPath, callerName, lineNumber)}{m}");
    }

    public static void Information(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.Information($"{CleanCallerPath(callerPath, callerName, lineNumber)}{message}");
    }

    public static void Fatal(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.Fatal($"{CleanCallerPath(callerPath, callerName, lineNumber)}{m}");
    }

    public static void Log(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.Information($"{CleanCallerPath(callerPath, callerName, lineNumber)}{m}");
    }
    
    public static void Warning(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.Warning($"{CleanCallerPath(callerPath, callerName, lineNumber)}{m}");
    }

    public static void Error(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.Error($"{CleanCallerPath(callerPath, callerName, lineNumber)}{m}");
    }

    public static void Error(Exception ex, string message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage($"{message}\n{ex}")) PluginLog.Error($"{CleanCallerPath(callerPath, callerName, lineNumber)}{m}");
    }
    
#else
        public static void Verbose(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach(var m in SplitMessage(message)) PluginLog.Verbose($"{m}");
        }

        public static void Debug(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.Debug($"{m}");
        }

        public static void Information(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.Information($"{m}");
        }

        public static void Fatal(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.Fatal($"{m}");
        }

        public static void Log(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.Information($"{m}");
        }

        public static void Warning(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.Warning($"{m}");
        }

        public static void Error(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.Error($"{m}");
        }

        public static void Error(Exception ex, object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage($"{message}\n{ex}")) PluginLog.Error($"{m}");
        }
        
        public static void Error(Exception ex, string message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage($"{message}\n{ex}")) PluginLog.Error($"{m}");
        }
#endif

    private static IEnumerable<string> SplitMessage(object message) {
        if (message is IList list) {
            return list.Cast<object>().Select((t, i) => $"{i}: {t}");
        }
        return $"{message}".Split('\n');
    }

}