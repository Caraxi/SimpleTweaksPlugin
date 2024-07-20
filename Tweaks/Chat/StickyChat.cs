#nullable enable
using System;
using System.Text.RegularExpressions;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Component.Shell;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Sticky Chat")]
[TweakDescription("Sets the chat channel when you use temporary chat messages.\nExample: \"/p hello!\" will set the chat channel to Party")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion("1.8.2.0")]
public unsafe partial class StickyChat : ChatTweaks.SubTweak {
    [TweakHook(typeof(ShellCommandModule), nameof(ShellCommandModule.ExecuteCommandInner), nameof(OnExecuteCommand))]
    private readonly HookWrapper<ShellCommandModule.Delegates.ExecuteCommandInner>? executeCommandHook;

    [GeneratedRegex("^\\/cwl[1-8] .+")]
    private static partial Regex CrossWorldLinkshellShort();
    
    [GeneratedRegex("^\\/cwlinkshell[1-8] .+")]
    private static partial Regex CrossWorldLinkshellLong();
    
    [GeneratedRegex("^\\/l[1-8] .+")]
    private static partial Regex LinkshellShort();
    
    [GeneratedRegex("^\\/linkshell[1-8] .+")]
    private static partial Regex LinkshellLong();
    
    private void OnExecuteCommand(ShellCommandModule* commandModule, Utf8String* command, UIModule* uiModule) {
        try {
            var inputString = command->ToString();

            switch (inputString) {
                case not null when inputString.StartsWith("/party "):
                case not null when inputString.StartsWith("/p "):
                    RaptureShellModule.Instance()->ChatType = 2;
                    RaptureShellModule.Instance()->CurrentChannel.SetString("/party");
                    break;
                
                case not null when inputString.StartsWith("/say "):
                case not null when inputString.StartsWith("/s "):
                    RaptureShellModule.Instance()->ChatType = 1;
                    RaptureShellModule.Instance()->CurrentChannel.SetString("/say");
                    break;
                
                case not null when inputString.StartsWith("/alliance "):
                case not null when inputString.StartsWith("/a "):
                    RaptureShellModule.Instance()->ChatType = 3;
                    RaptureShellModule.Instance()->CurrentChannel.SetString("/alliance");
                    break;
                
                case not null when inputString.StartsWith("/freecompany "):
                case not null when inputString.StartsWith("/fc "):
                    RaptureShellModule.Instance()->ChatType = 6;
                    RaptureShellModule.Instance()->CurrentChannel.SetString("/freecompany");
                    break;
                
                case not null when inputString.StartsWith("/novice "):
                case not null when inputString.StartsWith("/beginner "):
                case not null when inputString.StartsWith("/n "):
                    RaptureShellModule.Instance()->ChatType = 8;
                    RaptureShellModule.Instance()->CurrentChannel.SetString("/novice");
                    break;
                
                case not null when inputString.StartsWith("/yell "):
                case not null when inputString.StartsWith("/y "):
                    RaptureShellModule.Instance()->ChatType = 4;
                    RaptureShellModule.Instance()->CurrentChannel.SetString("/yell");
                    break;

                case not null when CrossWorldLinkshellLong().IsMatch(inputString) && inputString.Length > 12: {
                    if (int.TryParse(inputString[12..13], out var result)) {
                        RaptureShellModule.Instance()->ChatType = result + 8;
                        RaptureShellModule.Instance()->CurrentChannel.SetString($"/cwl{result}");
                    }
                    break;
                }

                case not null when CrossWorldLinkshellShort().IsMatch(inputString) && inputString.Length > 4: {
                    if (int.TryParse(inputString[4..5], out var result)) {
                        RaptureShellModule.Instance()->ChatType = result + 8;
                        RaptureShellModule.Instance()->CurrentChannel.SetString($"/cwl{result}");
                    }
                    break;
                }
                    
                case not null when LinkshellLong().IsMatch(inputString) && inputString.Length > 10: {
                    if (int.TryParse(inputString[10..11], out var result)) {
                        RaptureShellModule.Instance()->ChatType = result + 18;
                        RaptureShellModule.Instance()->CurrentChannel.SetString($"/linkshell{result}");
                    }
                    break;
                }
                
                case not null when LinkshellShort().IsMatch(inputString) && inputString.Length > 2: {
                    if (int.TryParse(inputString[2..3], out var result)) {
                        RaptureShellModule.Instance()->ChatType = result + 18;
                        RaptureShellModule.Instance()->CurrentChannel.SetString($"/linkshell{result}");
                    }
                    break;
                }
            }
        }
        catch (Exception e) {
            SimpleLog.Error(e, "Something went wrong in StickyChat, let MidoriKami know!");
        }

        executeCommandHook!.Original(commandModule, command, uiModule);
    }
}
