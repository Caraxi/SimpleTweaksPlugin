using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using SimpleTweaksPlugin.Debugging;
using Dalamud.Utility.Signatures;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Disable Click Targeting")]
[TweakDescription("Allows disabling of the target function on left and right mouse clicks.")]
[Changelog("1.10.1.0", "Added option to only disable targeting players.", Author = "OTCompa")]
[TweakAutoConfig]
public unsafe class DisableClickTargeting : Tweak {
    public class Configs : TweakConfig {
        public bool DisableRightClick = true;
        public bool DisableLeftClick;
        public bool OnlyDisableInCombat;
        public bool OnlyDisablePlayers;
        public bool UseNameFilter;
        public List<NameFilter> NameFilters = [];
    }

    public class NameFilter {
        public string Name = string.Empty;
        public bool DisableLeft;
        public bool DisableRight;
        public bool OnlyInCombat;
    }

    public Configs TweakConfig { get; private set; }

    private string nameFilterNew = string.Empty;

    protected void DrawConfig(ref bool hasChanged) {
        if (!TweakConfig.UseNameFilter) {
            hasChanged |= ImGui.Checkbox(LocString("SimpleDisableRightClick", "Disable Right Click Targeting"), ref TweakConfig.DisableRightClick);
            hasChanged |= ImGui.Checkbox(LocString("SimpleDisableLeftClick", "Disable Left Click Targeting"), ref TweakConfig.DisableLeftClick);

            ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.Checkbox(LocString("SimpleCombatOnly", "Only disable in combat"), ref TweakConfig.OnlyDisableInCombat);
            hasChanged |= ImGui.Checkbox(LocString("SimplePlayersOnly", "Only disable targeting players"), ref TweakConfig.OnlyDisablePlayers);
            ImGui.Dummy(new Vector2(10) * ImGui.GetIO().FontGlobalScale);
        }

        hasChanged |= ImGui.Checkbox(LocString("NameFiltering", "Enable Name Filtering"), ref TweakConfig.UseNameFilter);

        if (!(TweakConfig.DisableLeftClick || TweakConfig.DisableRightClick || TweakConfig.UseNameFilter)) {
            ImGui.Text(LocString("EverythingDisabled", "It is doing nothing if everything is disabled..."));
        }

        if (TweakConfig.UseNameFilter) {
            ImGui.Text(LocString("NameFiltersLabel", "Name Filters:"));
            ImGui.SameLine();
            ImGui.TextDisabled(LocString("NameFiltersHelp", "Per actor options for "));
            var i = 0;

            if (ImGui.BeginTable("nameFilterTable", 6)) {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 28 * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn(LocString("NameHeader", "\nName"));
                ImGui.TableSetupColumn(LocString("LeftHeader", "Disable\nLeft"), ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 50 * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn(LocString("RightHeader", "Disable\nRight"), ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 50 * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn(LocString("CombatHeader", "Only in\nCombat"), ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 50 * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn(LocString("PlayerHeader", "Only\nPlayers"), ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 50 * ImGui.GetIO().FontGlobalScale);

                ImGui.TableHeadersRow();
                NameFilter deleteNf = null;
                foreach (var nf in TweakConfig.NameFilters) {
                    ImGui.TableNextColumn();
                    if (ImGui.Button($"X##namefilter_delete_{++i}", new Vector2(-1, 24 * ImGui.GetIO().FontGlobalScale))) {
                        deleteNf = nf;
                    }

                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(LocString("RemoveTooltip", "Remove {0}").Format(nf.Name));
                    ImGui.TableNextColumn();
                    ImGui.Text(nf.Name);
                    ImGui.TableNextColumn();
                    hasChanged |= ImGui.Checkbox($"##nameFilter_disableLeft{i}", ref nf.DisableLeft);
                    ImGui.TableNextColumn();
                    hasChanged |= ImGui.Checkbox($"##nameFilter_disableRight{i}", ref nf.DisableRight);
                    ImGui.TableNextColumn();
                    hasChanged |= ImGui.Checkbox($"##nameFilter_onlyCombat{i}", ref nf.OnlyInCombat);
                    ImGui.TableNextColumn();
                }

                if (deleteNf != null) {
                    TweakConfig.NameFilters.Remove(deleteNf);
                    hasChanged = true;
                }

                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint($"##nameFilter_name{++i}", LocString("NamePlaceholder", "Name"), ref nameFilterNew, 30);

                ImGui.TableNextColumn();
                if (ImGui.Button(LocString("AddButton", "Add"))) {
                    if (TweakConfig.NameFilters.All(nf => nf.Name != nameFilterNew)) {
                        TweakConfig.NameFilters.Add(new NameFilter() { Name = nameFilterNew });
                        hasChanged = true;
                    }

                    nameFilterNew = string.Empty;
                }

                ImGui.TableNextColumn();
                var target = Service.Targets.SoftTarget ?? Service.Targets.Target;
                if (target != null) {
                    if (ImGui.Button("Target")) {
                        nameFilterNew = target.Name.TextValue;
                    }
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.Text(LocString("DefaultNameText", "Default (Unmatched Names)"));
                ImGui.TableNextColumn();
                hasChanged |= ImGui.Checkbox($"##nameFilter_disableLeft{i}", ref TweakConfig.DisableLeftClick);
                ImGui.TableNextColumn();
                hasChanged |= ImGui.Checkbox($"##nameFilter_disableRight{i}", ref TweakConfig.DisableRightClick);
                ImGui.TableNextColumn();
                hasChanged |= ImGui.Checkbox($"##nameFilter_onlyCombat{i}", ref TweakConfig.OnlyDisableInCombat);
                ImGui.TableNextColumn();
                hasChanged |= ImGui.Checkbox($"##nameFilter_onlyPlayers{i}", ref TweakConfig.OnlyDisablePlayers);

                ImGui.EndTable();
            }
        }
    }

    private delegate byte GetInputStatusDelegate(InputManager* a1, int a2);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 84 C0 44 8B C3", DetourName = nameof(GetInputStatusDetour))]
    private HookWrapper<GetInputStatusDelegate> getInputStatusHook;

    private delegate GameObject* GetMouseOverObjectDelegate(TargetSystem* a1, int a2, int a3, GameObjectArray* a4, Camera* camera);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 74 50 48 8B CB", DetourName = nameof(GetMouseOverObjectDetour))]
    private HookWrapper<GetMouseOverObjectDelegate> getMouseOverObjectHook;

    [Signature("E8 ?? ?? ?? ?? 84 C0 44 8B C3")]
    private GetInputStatusDelegate getInputStatus;

    private const int LeftMouse = 11;
    private const int RightMouse = 4;

    private bool NameFilterCheck(NameFilter nf, int key) => (nf.OnlyInCombat && !Service.Condition[ConditionFlag.InCombat]) || (key != LeftMouse && key != RightMouse) || (!nf.DisableLeft && key == LeftMouse) || (!nf.DisableRight && key == RightMouse);

    private bool NonNameFilterCheck(GameObject* actor, int key) => actor == null || (TweakConfig.OnlyDisableInCombat && !Service.Condition[ConditionFlag.InCombat]) || (key != LeftMouse && key != RightMouse) || (!TweakConfig.DisableLeftClick && key == LeftMouse) || (!TweakConfig.DisableRightClick && key == RightMouse) || (TweakConfig.OnlyDisablePlayers && actor->GetObjectKind() != ObjectKind.Pc);

    private byte GetInputStatusDetour(InputManager* a1, int a2) {
        using var _ = PerformanceMonitor.Run();
        var status = getInputStatusHook.Original(a1, a2);
        if (status == 0) return status;
        if (a2 != LeftMouse && a2 != RightMouse) return status;

        var actor = TargetSystem.Instance()->MouseOverNameplateTarget;
        if (actor == TargetSystem.Instance()->GetTargetObject()) return status;

        if (actor != null && TweakConfig.UseNameFilter) {
            var actorName = actor->NameString;
            var nf = TweakConfig.NameFilters.FirstOrDefault(a => a.Name == actorName, null);
            if (nf != null) return NameFilterCheck(nf, a2) ? status : (byte)0;
        }

        return NonNameFilterCheck(actor, a2) ? status : (byte)0;
    }

    private GameObject* GetMouseOverObjectDetour(TargetSystem* a1, int a2, int a3, GameObjectArray* a4, Camera* a5) {
        using var _ = PerformanceMonitor.Run();
        var actor = getMouseOverObjectHook.Original(a1, a2, a3, a4, a5);
        if (actor == null) return null;
        if (actor == TargetSystem.Instance()->GetTargetObject()) return actor;
        var pressed = 0;

        if (getInputStatusHook.Original(InputManager.Instance(), LeftMouse) != 0) pressed = LeftMouse;
        if (getInputStatusHook.Original(InputManager.Instance(), RightMouse) != 0) pressed = RightMouse;
        if (pressed == 0) return actor;

        if (TweakConfig.UseNameFilter) {
            var actorName = actor->NameString;
            var nf = TweakConfig.NameFilters.FirstOrDefault(a => a.Name == actorName, null);
            if (nf != null) return NameFilterCheck(nf, pressed) ? actor : null;
        }

        return NonNameFilterCheck(actor, pressed) ? actor : null;
    }
}
