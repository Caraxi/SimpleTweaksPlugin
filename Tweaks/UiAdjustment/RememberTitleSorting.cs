using System.Runtime.InteropServices;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class RememberTitleSorting : UiAdjustments.SubTweak {
    public override string Name => "Remember Title Sorting";
    public override string Description => "Remember the selected sorting option in the title selection menu.";

    private delegate void* ChangeSortOption(SomeAgent* agent, int sortOption);

    private HookWrapper<ChangeSortOption> changeSortOptionHook;

    public class Configs : TweakConfig {
        public int SelectedOption = 0;
    }

    public Configs Config { get; private set; }

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        changeSortOptionHook ??= Common.Hook<ChangeSortOption>("E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 83 7B 44 01", ChangeSortOptionDetour);
        changeSortOptionHook?.Enable();

        base.Enable();
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x48)]
    public struct SomeAgent {
        [FieldOffset(0x44)] public int SomeInt;
    }

    private void* ChangeSortOptionDetour(SomeAgent* agent, int sortOption) {
        try {
            if (agent->SomeInt != 0) {
                sortOption = Config.SelectedOption;
            } else {
                Config.SelectedOption = sortOption;
                PluginConfig.Save();
            }
        } catch {
            //
        }
        return changeSortOptionHook.Original(agent, sortOption);
    }

    public override void Disable() {
        changeSortOptionHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        changeSortOptionHook?.Dispose();
        base.Dispose();
    }
}