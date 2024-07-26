using System.Runtime.InteropServices;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Remember Title Sorting")]
[TweakDescription("Remember the selected sorting option in the title selection menu.")]
public unsafe class RememberTitleSorting : UiAdjustments.SubTweak {
    private delegate void* ChangeSortOption(AgentCharacterTitle* agent, int sortOption);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 45 88 7E 08", DetourName = nameof(ChangeSortOptionDetour))]
    private HookWrapper<ChangeSortOption> changeSortOptionHook;

    public class Configs : TweakConfig {
        public int SelectedOption;
    }

    public Configs Config { get; private set; }

    [StructLayout(LayoutKind.Explicit, Size = 0x48)]
    public struct AgentCharacterTitle {
        [FieldOffset(0x00)] public AgentInterface AgentInterface;
        [FieldOffset(0x44)] public int SomeInt;
    }

    private void* ChangeSortOptionDetour(AgentCharacterTitle* agent, int sortOption) {
        try {
            Config ??= LoadConfig<Configs>() ?? new Configs();
            if (agent->SomeInt != 0) {
                sortOption = Config.SelectedOption;
            } else {
                Config.SelectedOption = sortOption;
                SaveConfig(Config);
            }
        } catch {
            //
        }

        return changeSortOptionHook.Original(agent, sortOption);
    }
}
