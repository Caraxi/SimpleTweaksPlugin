using ImGuiNET;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.Interop;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public bool ShouldSerializeLimitTargetStatusEffects() => LimitTargetStatusEffects != null;
        public LimitTargetStatusEffects.Configs LimitTargetStatusEffects = null;
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class LimitTargetStatusEffects : UiAdjustments.SubTweak {
        public override string Name => "Target Status Adjustments";
        public override string Description => "Allows the filtering of specific status effects on your target as well as limiting the number of them.";
        public override IEnumerable<string> Tags => new[] {"Buffs", "Debuffs", "Limit", "Filter", "Effects"};
        protected override string Author => "Aireil";

        public class Configs : TweakConfig {
            public int NbStatusEffects = 30;
            public bool LimitOnlyInCombat;
            public bool FilterOnlyInCombat;
            public bool FilterPersonalStatus;
            public bool FilterTarget = true;
            public bool FilterFocusTarget = true;
            public readonly HashSet<ushort> FilteredStatusCustom = new();
        }

        public Configs Config { get; private set; }
        private bool isDirty;
        private static string statusSearch = string.Empty;
        private readonly ushort[] removedStatus = new ushort[60 * 2];
        private uint removedStatusIndex;
        private readonly HashSet<ushort> filteredStatus = new();
        private static Dictionary<ushort, Lumina.Excel.GeneratedSheets.Status> statusSheet;
        private readonly TargetSystem* targetSystem = TargetSystem.Instance();

        private delegate long UpdateTargetStatusDelegate(void* agentHud, void* numberArray, void* stringArray, StatusManager* statusManager, GameObject* target, void* isLocalPlayerAndRollPlaying);
        private HookWrapper<UpdateTargetStatusDelegate> updateTargetStatusHook;

        private delegate long UpdateFocusTargetDelegate(void* agentHud);
        private HookWrapper<UpdateFocusTargetDelegate> updateFocusTargetHook;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            statusSheet ??= Service.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>()?.ToDictionary(row => (ushort)row.RowId, row => row);

            ImGui.Text("Limiting:");
            ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
            if (ImGui.InputInt(LocString("NbStatusEffects", "Number of status effects displayed (default 30)##nbStatusEffectsDisplayed"), ref Config.NbStatusEffects, 1)) {
                hasChanged = true;
                Config.NbStatusEffects = Config.NbStatusEffects switch {
                    < 0 => 0,
                    > 30 => 30,
                    _ => Config.NbStatusEffects,
                };
            }

            hasChanged |= ImGui.Checkbox(LocString("LimitOnlyInCombat", "Only limit in combat##LimitOnlyInCombat"), ref Config.LimitOnlyInCombat);

            ImGui.Separator();
            ImGui.Text("Filtering:");
            ImGui.TextColored(ImGuiColors.DalamudGrey, "Note: Your status effects will never be filtered and the filtering is disabled in PvP and on you.");
            hasChanged |= ImGui.Checkbox(LocString("FilterOnlyInCombat", "Only filter in combat##FilterOnlyInCombat"), ref Config.FilterOnlyInCombat);
            hasChanged |= ImGui.Checkbox(LocString("FilterTarget", "Filter target##FilterTarget"), ref Config.FilterTarget);
            hasChanged |= ImGui.Checkbox(LocString("FilterFocusTarget", "Filter focus target##FilterFocusTarget"), ref Config.FilterFocusTarget);

            hasChanged |= ImGui.Checkbox(LocString("FilterPersonalStatus", "Filter all personal status of others on enemies (DoTs, self-buffs, etc...,)##FilterPersonalStatus"), ref Config.FilterPersonalStatus);
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("This should only display relevant status for you.\nIf you find one that should be filtered but is not, please report it\nby using the feedback button or by pinging Aireil in goat place Discord.");
            }

            ImGui.SameLine();
            if (ImGui.Button("Personal Status List##PersonalStatusList")) {
                ImGui.OpenPopup("PersonalStatusList");
            }

            DrawPersonalStatusListPopup();

            ImGui.Text("Custom filtered status (added to the setting above):");

            var ySize = 23.0f + (25.0f * Config.FilteredStatusCustom.Count);
            if (ySize > 125.0f) {
                ySize = 125.0f;
            }

            if (statusSheet != null && ImGui.BeginTable("##FilteredStatusCustom", 4, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY,
                    new Vector2(-1, ySize * ImGuiHelpers.GlobalScale))) {
                ImGui.TableSetupScrollFreeze(0, 1);

                ImGui.TableSetupColumn("Status ID");
                ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 25 * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 20 * ImGuiHelpers.GlobalScale);

                ImGui.TableHeadersRow();

                foreach (var statusId in Config.FilteredStatusCustom) {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(statusId.ToString());

                    ImGui.TableNextColumn();
                    var statusIconTex = Service.TextureProvider.GetFromGameIcon(statusSheet[statusId].Icon).GetWrapOrDefault();
                    if (statusIconTex != null) {
                        var scale = (25 * ImGuiHelpers.GlobalScale) / statusIconTex.Height;
                        ImGui.Image(statusIconTex.ImGuiHandle, new Vector2(statusIconTex.Width * scale, statusIconTex.Height * scale));
                    } else {
                        ImGui.Text("-");
                    }

                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(statusSheet[statusId].Name);
                    ImGui.TableNextColumn();
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString() + "##" + statusId))
                    {
                        Config.FilteredStatusCustom.Remove(statusId);
                        hasChanged = true;
                    }
                    ImGui.PopFont();
                }

                ImGui.EndTable();
            }

            if (statusSheet != null) {
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString())) {
                    ImGui.OpenPopup("AddCustomStatus");
                }
                ImGui.PopFont();

                hasChanged |= DrawStatusPopup();
            } else {
                ImGui.TextColored(ImGuiColors.DalamudRed, "Could not load status sheet, custom filtered status are unavailable.");
            }

            if (hasChanged) {
                UpdateFilteredStatus();
                UpdateTargetStatus(true);
                SaveConfig(Config);
            }
        };

        protected override void Enable() {
            Config = LoadConfig<Configs>() ?? PluginConfig.UiAdjustments.LimitTargetStatusEffects ?? new Configs();
            UpdateFilteredStatus();
            Common.FrameworkUpdate += FrameworkOnUpdate;

            updateTargetStatusHook = Common.Hook<UpdateTargetStatusDelegate>("E8 ?? ?? ?? ?? 4C 8B 44 24 ?? 4D 8B CE", UpdateTargetStatusDetour);
            updateFocusTargetHook = Common.Hook<UpdateFocusTargetDelegate>("40 55 53 57 48 8D 6C 24 90", UpdateFocusTargetDetour);

            this.updateFocusTargetHook?.Enable();
            this.updateTargetStatusHook?.Enable();

            base.Enable();
        }

        private void FilterStatus(StatusManager* statusManager, GameObject* target) {
            GameObject* localPlayer = null;
            this.removedStatusIndex = 0;

            for (ushort i = 0; i < statusManager->NumValidStatuses; i++) {
                var status = statusManager->Status.GetPointer(i);
                var statusId = status->StatusId;
                if (statusId == 0 || !filteredStatus.Contains(statusId)) {
                    continue;
                }

                if (localPlayer == null) {
                    localPlayer = GameObjectManager.Instance()->Objects.IndexSorted[0];
                    if (localPlayer == null
                        || localPlayer->EntityId == target->EntityId
                        || (Config.FilterOnlyInCombat && !((Character*)localPlayer)->InCombat)
                        || Service.ClientState.IsPvP) {
                        break;
                    }
                }

                if (status->SourceId == localPlayer->EntityId) {
                    continue;
                }

                removedStatus[this.removedStatusIndex++] = i;
                removedStatus[this.removedStatusIndex++] = statusId;
                status->StatusId = 0;
            }
        }

        private void RestoreStatus(StatusManager* statusManager) {
            for (var i = 0; i < this.removedStatusIndex; i += 2) {
                statusManager->Status.GetPointer(removedStatus[i])->StatusId = removedStatus[i + 1];
            }

            this.removedStatusIndex = 0;
        }

        private long UpdateTargetStatusDetour(void* agentHud, void* numberArray, void* stringArray, StatusManager* statusManager, GameObject* target, void* isLocalPlayerAndRolePlaying) {
            long ret = 0;
            try {
                if (Config.FilterTarget) {
                    FilterStatus(statusManager, target);

                    ret = updateTargetStatusHook.Original(agentHud, numberArray, stringArray, statusManager, target, isLocalPlayerAndRolePlaying);

                    RestoreStatus(statusManager);
                } else {
                    ret = updateTargetStatusHook.Original(agentHud, numberArray, stringArray, statusManager, target, isLocalPlayerAndRolePlaying);
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex, "Exception in UpdateTargetStatusDetour");
            }

            return ret;
        }

        private long UpdateFocusTargetDetour(void* agentHud) {
            long ret = 0;
            try {
                var focusTarget = this.targetSystem != null ? this.targetSystem->FocusTarget : null;
                var shouldFilter = Config.FilterFocusTarget && this.targetSystem != null && focusTarget != null && focusTarget->IsCharacter();

                if (shouldFilter) {
                    var focusTargetStatusManager = ((Character*)focusTarget)->GetStatusManager();
                    FilterStatus(focusTargetStatusManager, focusTarget);

                    ret = updateFocusTargetHook.Original(agentHud);

                    RestoreStatus(focusTargetStatusManager);
                } else {
                    ret = updateFocusTargetHook.Original(agentHud);
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex, "Exception in UpdateFocusTargetDetour");
            }

            return ret;
        }

        protected override void Disable() {
            SaveConfig(Config);
            PluginConfig.UiAdjustments.LimitTargetStatusEffects = null;
            updateTargetStatusHook?.Disable();
            updateFocusTargetHook?.Disable();
            Common.FrameworkUpdate -= FrameworkOnUpdate;
            UpdateTargetStatus(true);
            base.Disable();
        }

        private void FrameworkOnUpdate() {
            try {
                UpdateTargetStatus();
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        private void UpdateTargetStatus(bool reset = false) {
            var targetInfoUnitBase = Common.GetUnitBase("_TargetInfo");
            if (targetInfoUnitBase == null) return;
            if (targetInfoUnitBase->UldManager.NodeList == null || targetInfoUnitBase->UldManager.NodeListCount < 53) return;

            var targetInfoStatusUnitBase = Common.GetUnitBase("_TargetInfoBuffDebuff");
            if (targetInfoStatusUnitBase == null) return;
            if (targetInfoStatusUnitBase->UldManager.NodeList == null || targetInfoStatusUnitBase->UldManager.NodeListCount < 32) return;

            var isInCombat =
                Service.Condition[ConditionFlag.InCombat];

            if (reset || (Config.LimitOnlyInCombat && !isInCombat && isDirty)) {
                for (var i = 32; i >= 3; i--) {
                    targetInfoUnitBase->UldManager.NodeList[i]->Color.A = 255;
                }

                for (var i = 31; i >= 2; i--) {
                    targetInfoStatusUnitBase->UldManager.NodeList[i]->Color.A = 255;
                }

                isDirty = false;

                return;
            }

            if (Config.LimitOnlyInCombat && !isInCombat) return;

            isDirty = true;

            for (var i = 32 - Config.NbStatusEffects; i >= 3; i--) {
                targetInfoUnitBase->UldManager.NodeList[i]->Color.A = 0;
            }

            for (var i = 31 - Config.NbStatusEffects; i >= 2; i--) {
                targetInfoStatusUnitBase->UldManager.NodeList[i]->Color.A = 0;
            }
        }

        private void UpdateFilteredStatus() {
            filteredStatus.Clear();
            if (Config.FilterPersonalStatus) {
                filteredStatus.UnionWith(GetPersonalStatus());
            }

            filteredStatus.UnionWith(Config.FilteredStatusCustom);
        }

        private bool DrawStatusPopup() {
            ImGui.SetNextWindowSize(new Vector2(0, 200 * ImGuiHelpers.GlobalScale));

            if (!ImGui.BeginPopup("AddCustomStatus")) {
                return false;
            }

            ImGui.InputText("##StatusSearch", ref statusSearch, 64);

            var hasChanged = false;
            var isSearching = !string.IsNullOrEmpty(statusSearch);
            if (ImGui.BeginChild("##StatusList")) {
                foreach (var (id, row) in statusSheet) {
                    if (row.Name == string.Empty) {
                        continue;
                    }

                    var name = $"#{id} {row.Name.RawString}";
                    if (isSearching && !name.Contains(statusSearch, StringComparison.CurrentCultureIgnoreCase)) {
                        continue;
                    }

                    ImGui.PushID("AddCustomStatus" + id);

                    if (ImGui.Selectable(name, false, ImGuiSelectableFlags.DontClosePopups)) {
                        Config.FilteredStatusCustom.Add(id);
                        hasChanged = true;
                    }

                    ImGui.PopID();
                }

                ImGui.EndChild();
            }

            ImGui.EndPopup();

            return hasChanged;
        }

        private static void DrawPersonalStatusListPopup() {
            ImGui.SetNextWindowSize(new Vector2(0, 200 * ImGuiHelpers.GlobalScale));

            if (!ImGui.BeginPopup("PersonalStatusList")) {
                return;
            }

            if (statusSheet != null && ImGui.BeginTable("##PersonalStatusList", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY)) {
                ImGui.TableSetupScrollFreeze(0, 1);

                ImGui.TableSetupColumn("Status ID");
                ImGui.TableSetupColumn("Icon");
                ImGui.TableSetupColumn("Name");

                ImGui.TableHeadersRow();

                foreach (var statusId in GetPersonalStatus()) {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(statusId.ToString());

                    ImGui.TableNextColumn();
                    var statusIconTex = Service.TextureProvider.GetFromGameIcon(statusSheet[statusId].Icon).GetWrapOrDefault();
                    if (statusIconTex != null) {
                        var scale = (25 * ImGuiHelpers.GlobalScale) / statusIconTex.Height;
                        ImGui.Image(statusIconTex.ImGuiHandle, new Vector2(statusIconTex.Width * scale, statusIconTex.Height * scale));
                    } else {
                        ImGui.Text("-");
                    }

                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(statusSheet[statusId].Name);
                }

                ImGui.EndTable();
            }

            ImGui.EndPopup();
        }

        private static IEnumerable<ushort> GetPersonalStatus() {
            return new HashSet<ushort>() {
                248, // Circle of Scorn (PLD)
                725, // Goring Blade (PLD)
                2721, // Blade of Valor (PLD)

                1837, // Sonic Break (GNB)
                1838, // Bow Shock (GNB)

                143, // Aero (WHM)
                144, // Aero II (WHM)
                1871, // Dia (WHM)

                179, // Bio (SCH)
                189, // Bio II (SCH)
                1895, // Biolysis (SCH)

                838, // Combust (AST)
                843, // Combust II (AST)
                1881, // Combust III (AST)

                2614, // Eukrasian Dosis (SGE)
                2615, // Eukrasian Dosis II (SGE)
                2616, // Eukrasian Dosis III (SGE)

                246, // Demolish (MNK)

                118, // Chaos Thrust (DRG)
                2719, // Chaotic Spring (DRG)

                3254, // Trick Attack (NIN)

                1228, // Higanbana (SAM)

                2586, // Death's Design (RPR)

                124, // Venomous Bite (BRD)
                129, // Windbite (BRD)
                1200, // Caustic Bite (BRD)
                1201, // Stormbite (BRD)

                861, // Wildfire (MCH)
                1866, // Bioblaster (MCH)

                161, // Thunder (BLM)
                162, // Thunder II (BLM)
                163, // Thunder III (BLM)
                1210, // Thunder IV (BLM)

                1723, // Windburn (BLU)
                1714, // Bleeding (BLU)
                1736, // Dropsy (BLU)

                236, // Choco Beak (Chocobo)
            };
        }
    }
}
