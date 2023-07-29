using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Zoomed Chat Customization")]
[TweakDescription("Allows customization of the size and position of the zoomed chat view.")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class ZoomedChatCustomization : ChatTweaks.SubTweak {

    private delegate void ZoomChat(AtkUnitBase* atkUnitBase);
    [Signature("E8 ?? ?? ?? ?? 48 8D 8E ?? ?? ?? ?? E8 ?? ?? ?? ?? 85 C0 0F 84 ?? ?? ?? ?? 48 8B CB", DetourName = nameof(ChatZoomedDetour))]
    private readonly Hook<ZoomChat> chatZoomedHook = null!;
    
    private delegate byte IsChatZoomed(AtkUnitBase* atkUnitBase);
    [Signature("E8 ?? ?? ?? ?? 48 8B CE 84 C0 74 07 E8 ?? ?? ?? ?? EB 7E")]
    private readonly IsChatZoomed isChatZoomed = null!;
    
    public class Configs : TweakConfig {
        public Vector2 Size = new(70f, 90f);
        public Vector2 Position = new(50f, 50f);
        public int InputSpacing = 35;
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        hasChanged |= ImGui.SliderFloat2("Size", ref Config.Size, 10, 100, "%.0f%%", ImGuiSliderFlags.AlwaysClamp);
        hasChanged |= ImGui.SliderFloat2("Position", ref Config.Position, 10, 100, "%.0f%%", ImGuiSliderFlags.AlwaysClamp);
        hasChanged |= ImGui.SliderInt("Input Spacing", ref Config.InputSpacing, 10, 100, "%d", ImGuiSliderFlags.AlwaysClamp);
        if (hasChanged) TryApply();
    };

    protected override void Enable() {
        SignatureHelper.Initialise(this);
        Config = LoadConfig<Configs>() ?? new Configs();
        chatZoomedHook?.Enable();
        TryApply();
        base.Enable();
    }

    private void TryApply() {
        var addon = Common.GetUnitBase("ChatLog");
        if (addon != null && isChatZoomed(addon) != 0) {
            Service.Framework.RunOnTick(ApplyCustomization);
            Service.Framework.RunOnTick(ApplyCustomization, delayTicks: 1);
        }
    }

    private void ChatZoomedDetour(AtkUnitBase* atkUnitBase) {
        chatZoomedHook.Original(atkUnitBase);
        Common.ReadString(atkUnitBase->Name);
        var chatLog = Common.GetUnitBase("ChatLog");
        if (chatLog != atkUnitBase) return;
        ApplyCustomization();
        Service.Framework.RunOnTick(ApplyCustomization);
    }
    
    private void ApplyCustomization() {
        var chatLog = Common.GetUnitBase("ChatLog");
        if (chatLog == null) return;
        var screenSize = ImGui.GetMainViewport().Size;
        var size = screenSize * Vector2.Clamp(Config.Size / 100f, new Vector2(0.1f), Vector2.One);
        var centerScreen = screenSize * Vector2.Clamp(Config.Position / 100f, Vector2.Zero, Vector2.One);
        var halfSize = size / 2;
        var position = centerScreen - halfSize;
        
        chatLog->SetSize((ushort)size.X, (ushort) size.Y);
        chatLog->SetPosition((short) position.X, (short) position.Y);

        foreach (var c in new[] { "ChatLogPanel_0", "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3" }) {
            var addon = Common.GetUnitBase(c);
            if (addon == null) continue;
            addon->SetSize((ushort)size.X, (ushort) (size.Y - Config.InputSpacing));
            addon->SetPosition((short) position.X, (short) position.Y);
        }
        
        var inputBox = chatLog->GetNodeById(5);
        if (inputBox == null) return;
        inputBox->SetWidth((ushort)(chatLog->RootNode->Width - 23));
    }

    protected override void Disable() {
        SaveConfig(Config);
        chatZoomedHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        chatZoomedHook?.Dispose();
        base.Dispose();
    }
}
