using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.UI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Alt + F4 Exit Game")]
[TweakDescription("Pressing Alt + F4 will cause the game to close safely.")]
[TweakAuthor("MidoriKami")]
public unsafe class ExitGame : Tweak {
    [FrameworkUpdate]
    private void FrameworkUpdate() {
        if (IsKeyDown(VirtualKey.MENU) && IsKeyPressed(VirtualKey.F4)) {
            UIModule.Instance()->ExecuteMainCommand(24);
        }
    }

    private bool IsKeyPressed(VirtualKey key)
        => UIInputData.Instance()->GetKeyState((int) key).HasFlag(KeyStateFlags.Pressed);

    private bool IsKeyDown(VirtualKey key)
        => UIInputData.Instance()->GetKeyState((int) key).HasFlag(KeyStateFlags.Down);
}