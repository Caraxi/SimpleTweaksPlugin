using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Alt + F4 Exit Game")]
[TweakDescription("Pressing Alt + F4 will cause the game to close safely.")]
[TweakAuthor("MidoriKami")]
public unsafe class ExitGame : Tweak {
    [FrameworkUpdate]
    private void FrameworkUpdate() {
        if (UIInputData.Instance()->GetKeyState((int) VirtualKey.F4).HasFlag(KeyStateFlags.Pressed) && ImGui.GetIO().KeyAlt) {
            UIModule.Instance()->ExecuteMainCommand(24);
        }
    }
}