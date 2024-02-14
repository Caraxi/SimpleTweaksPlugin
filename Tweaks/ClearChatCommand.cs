using System;
using System.Linq;
using Dalamud.Game.Text;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Clear Chat Command")]
[TweakDescription("Adds a command to clear the chat window.")]
[TweakAuthor("Dante")]
[TweakReleaseVersion(UnreleasedVersion)]
[TweakCategory(TweakCategory.Command)]
public class ClearChatCommand : CommandTweak {
    protected override string Command => "clearchat";
    protected override string HelpMessage => "Clears the chat window using the Echo channel and the specified number of lines to clear.";

    private class Config : TweakConfig {
        public int ClearLines = 10;
    }

    private Config TweakConfig { get; set; } = null!;

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {
        if (ImGui.InputInt("Lines to Clear", ref TweakConfig.ClearLines)) {
            if (TweakConfig.ClearLines < 1) TweakConfig.ClearLines = 1;
            SaveConfig(TweakConfig);
        }

        ImGui.Separator();
        ImGui.Text($"/{Command}");
    };

    protected override void Enable() {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        base.Enable();
    }

    protected override void Disable() {
        SaveConfig(TweakConfig);
        base.Disable();
    }

    protected override void OnCommand(string _) {
        Service.Chat.Print(new XivChatEntry() {
            Message = new string('\n', TweakConfig.ClearLines),
            Type = XivChatType.Echo
        });
        
        Service.Chat.Print(new XivChatEntry() {
            Message = string.Empty,
            Type = XivChatType.Echo
        });
    }
}
