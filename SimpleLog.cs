using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Dalamud.Logging;

namespace SimpleTweaksPlugin; 

public static class SimpleLog {

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
        foreach (var m in SplitMessage(message)) PluginLog.LogVerbose($"{CleanCallerPath(callerPath, callerName, lineNumber)}{m}");
    }

    public static void Debug(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.LogDebug($"{CleanCallerPath(callerPath, callerName, lineNumber)}{m}");
    }

    public static void Information(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.LogInformation($"{CleanCallerPath(callerPath, callerName, lineNumber)}{message}");
    }

    public static void Fatal(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.LogFatal($"{CleanCallerPath(callerPath, callerName, lineNumber)}{m}");
    }

    public static void Log(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.Log($"{CleanCallerPath(callerPath, callerName, lineNumber)}{m}");
    }

    public static void Error(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.Error($"{CleanCallerPath(callerPath, callerName, lineNumber)}{m}");
    }
#else
        public static void Verbose(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach(var m in SplitMessage(message)) PluginLog.LogVerbose($"{m}");
        }

        public static void Debug(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.LogDebug($"{m}");
        }

        public static void Information(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.LogInformation($"{m}");
        }

        public static void Fatal(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.LogFatal($"{m}");
        }

        public static void Log(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.Log($"{m}");
        }

        public static void Error(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.Error($"{m}");
        }
#endif

    private static IEnumerable<string> SplitMessage(object message) {
        if (message is IList list) {
            return list.Cast<object>().Select((t, i) => $"{i}: {t}");
        }
        return $"{message}".Split('\n');
    }

}