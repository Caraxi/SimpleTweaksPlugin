using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Internal;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using static SimpleTweaksPlugin.Tweaks.UiAdjustments.Step;
using Addon = Dalamud.Game.Internal.Gui.Addon.Addon;

namespace SimpleTweaksPlugin
{
    public partial class UiAdjustmentsConfig
    {
        public ShiftTargetCastBarText.Config ShiftTargetCastBarText = new();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment
{
    public class ShiftTargetCastBarText : UiAdjustments.SubTweak
    {
        public class Config
        {
            public int Offset = 8;
            public bool EnableCastTime;
            public int CastTimeFontSize = 15;
            public int CastTimeOffsetX;
            public int CastTimeOffsetY;
        }

        public override string Name => "Reposition Target Castbar Text";
        public override string Description => "Moves the text on target castbars to make it easier to read";
        
        private readonly Vector2 buttonSize = new Vector2(26, 22);

        protected override DrawConfigDelegate DrawConfigTree => (ref bool changed) =>
        {
            var bSize = buttonSize * ImGui.GetIO().FontGlobalScale;
            ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
            if (ImGui.InputInt($"###{GetType().Name}_Offset",
                ref PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset))
            {
                if (PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset > MaxOffset)
                    PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset = MaxOffset;
                if (PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset < MinOffset)
                    PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset = MinOffset;
                changed = true;
            }

            ImGui.SameLine();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2));
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(char) FontAwesomeIcon.ArrowUp}", bSize))
            {
                PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset = 8;
                changed = true;
            }

            ImGui.PopFont();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Above progress bar");

            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}", bSize))
            {
                PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset = 24;
                changed = true;
            }

            ImGui.PopFont();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Original Position");


            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(char) FontAwesomeIcon.ArrowDown}", bSize))
            {
                PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset = 32;
                changed = true;
            }

            ImGui.PopFont();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Below progress bar");
            ImGui.PopStyleVar();
            ImGui.SameLine();
            ImGui.Text("Ability name vertical offset");
            
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Checkbox($"###{GetType().Name}_EnableCastTime",
                ref PluginConfig.UiAdjustments.ShiftTargetCastBarText.EnableCastTime)) changed = true;
            ImGui.PopFont();
            ImGui.SameLine();
            ImGui.Text("Show cast timer");

            ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
            if (ImGui.InputInt($"###{GetType().Name}_CastTimeFontSize",
                ref PluginConfig.UiAdjustments.ShiftTargetCastBarText.CastTimeFontSize))
            {
                if (PluginConfig.UiAdjustments.ShiftTargetCastBarText.CastTimeFontSize <= 0)
                    PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset = 1;
                changed = true;
            }

            ImGui.SameLine();
            ImGui.Text("Cast Timer font size");

            ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
            if (ImGui.InputInt($"###{GetType().Name}_CastTimeOffsetX",
                ref PluginConfig.UiAdjustments.ShiftTargetCastBarText.CastTimeOffsetX)) changed = true;
            ImGui.SameLine();
            ImGui.Text("Cast Timer X offset");

            ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
            if (ImGui.InputInt($"###{GetType().Name}_CastTimeOffsetY",
                ref PluginConfig.UiAdjustments.ShiftTargetCastBarText.CastTimeOffsetY)) changed = true;
            ImGui.SameLine();
            ImGui.Text("Cast Timer Y offset");
        };

        public void OnFrameworkUpdate(Framework framework)
        {
            try
            {
                HandleBars(framework);
            }
            catch (Exception ex)
            {
                Plugin.Error(this, ex);
            }
        }

        private void HandleBars(Framework framework, bool reset = false)
        {
            var focusTargetInfo = framework.Gui.GetAddonByName("_FocusTargetInfo", 1);
            if (focusTargetInfo != null && (focusTargetInfo.Visible || reset))
                HandleFocusTargetInfo(focusTargetInfo, reset);

            var seperatedCastBar = framework.Gui.GetAddonByName("_TargetInfoCastBar", 1);
            if (seperatedCastBar != null && (seperatedCastBar.Visible || reset))
            {
                HandleSeperatedCastBar(seperatedCastBar, reset);
                if (!reset) return;
            }

            var mainTargetInfo = framework.Gui.GetAddonByName("_TargetInfo", 1);
            if (mainTargetInfo != null && (mainTargetInfo.Visible || reset))
                HandleMainTargetInfo(mainTargetInfo, reset);
        }

        private unsafe void HandleSeperatedCastBar(Addon addon, bool reset = false)
        {
            var addonStruct = (AtkUnitBase*) addon.Address;
            if (addonStruct->RootNode == null) return;
            var rootNode = addonStruct->RootNode;
            if (rootNode->ChildNode == null) return;
            var child = rootNode->ChildNode;
            DoShift(child, reset);
            if (!PluginConfig.UiAdjustments.ShiftTargetCastBarText.EnableCastTime)
                return;
            var textNode = (AtkTextNode*)  GetNodeById(addonStruct,4);
            AddCastTimeTextNode(addonStruct, textNode, textNode->AtkResNode.IsVisible);
        }

        private unsafe void HandleMainTargetInfo(Addon addon, bool reset = false)
        {
            var addonStruct = (AtkUnitBase*) addon.Address;
            if (addonStruct->RootNode == null) return;

            var child = GetNodeById(addonStruct, 10);
            if (child == null) return;
            DoShift(child, reset);

            if (!PluginConfig.UiAdjustments.ShiftTargetCastBarText.EnableCastTime)
                return;
            var textNode = (AtkTextNode*) GetNodeById(addonStruct,12);
            AddCastTimeTextNode(addonStruct, textNode, textNode->AtkResNode.IsVisible);
        }

        private unsafe void HandleFocusTargetInfo(Addon addon, bool reset = false)
        {
            var addonStruct = (AtkUnitBase*) addon.Address;
            var child = GetNodeById(addonStruct, 3);
            if (child == null) return;
            DoShift(child, reset);
        }

        private const int MinOffset = 0;
        private const int MaxOffset = 48;

        private unsafe void DoShift(AtkResNode* node, bool reset = false)
        {
            if (node == null) return;
            if (node->ChildCount < 5) return; // Should have 5 children
            var skillTextNode = UiAdjustments.GetResNodeByPath(node, Child, Previous, Previous, Previous);
            if (skillTextNode == null) return;
            var p = PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset;
            if (p < MinOffset) p = MinOffset;
            if (p > MaxOffset) p = MaxOffset;
            Marshal.WriteInt16(new IntPtr(skillTextNode), 0x92, reset ? (short) 24 : (short) p);
        }

        private const int TargetCastNodeId = 99990002;

        private unsafe void AddCastTimeTextNode(AtkUnitBase* unit, AtkTextNode* cloneTextNode, bool visible = false)
        {
            var textNode = (AtkTextNode*)GetNodeById(unit, TargetCastNodeId);
            
            if (textNode == null)
            {
                textNode = UiHelper.CloneNode(cloneTextNode);
                textNode->AtkResNode.NodeID = TargetCastNodeId;
                var newStrPtr = Common.Alloc(512);
                textNode->NodeText.StringPtr = (byte*) newStrPtr;
                textNode->NodeText.BufSize = 512;
                UiHelper.SetText(textNode, "");
                UiHelper.ExpandNodeList(unit, 1);
                unit->UldManager.NodeList[unit->UldManager.NodeListCount++] = (AtkResNode*) textNode;

                var nextNode = (AtkTextNode*)GetNodeById(unit, cloneTextNode->AtkResNode.NodeID-1);

                textNode->AtkResNode.ParentNode = nextNode->AtkResNode.ParentNode;
                textNode->AtkResNode.ChildNode = null;
                textNode->AtkResNode.NextSiblingNode = (AtkResNode*) nextNode;
                textNode->AtkResNode.PrevSiblingNode = null;
                nextNode->AtkResNode.PrevSiblingNode = (AtkResNode*) textNode;
                nextNode->AtkResNode.ParentNode->ChildCount += 1;
            }

            if (!visible)
            {
                UiHelper.Hide(textNode);
            }
            else
            {
                textNode->AlignmentFontType = 0x27;
                UiHelper.SetPosition(textNode, PluginConfig.UiAdjustments.ShiftTargetCastBarText.CastTimeOffsetX,
                    PluginConfig.UiAdjustments.ShiftTargetCastBarText.CastTimeOffsetY);
                UiHelper.SetSize(textNode, cloneTextNode->AtkResNode.Width, cloneTextNode->AtkResNode.Height);
                textNode->FontSize = (byte) PluginConfig.UiAdjustments.ShiftTargetCastBarText.CastTimeFontSize;
                UiHelper.SetText(textNode, GetTargetCastTime().ToString("00.00"));
                UiHelper.Show(textNode);
            }

        }


        private float GetTargetCastTime()
        {
            if (PluginInterface.ClientState.LocalPlayer == null ||
                PluginInterface.ClientState.Targets.CurrentTarget == null)
                return 0;
            var target = PluginInterface.ClientState.Targets.CurrentTarget;
            if (target is Chara)
            {
                var castTime =
                    Marshal.PtrToStructure<float>(target.Address +
                                                  Dalamud.Game.ClientState.Structs.ActorOffsets.CurrentCastTime);
                var totalCastTime =
                    Marshal.PtrToStructure<float>(target.Address +
                                                  Dalamud.Game.ClientState.Structs.ActorOffsets.TotalCastTime);
                return totalCastTime - castTime;
            }

            return 0;
        }

        
        private static unsafe AtkResNode* GetNodeById(AtkUnitBase* compBase, uint id)
        {
            if (compBase == null) return null;
            if ((compBase->UldManager.Flags1 & 1) == 0 || id <= 0) return null;
            var count = compBase->UldManager.NodeListCount;
            for (var i = 0; i < count; i++)
            {
                
                var node = compBase->UldManager.NodeList[i];
                //SimpleLog.Information(i+"@"+node->NodeID);
                if (node->NodeID == id) return node;
            }
            return null;
        }

        public override void Enable()
        {
            if (Enabled) return;
            PluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
            Enabled = true;
        }

        public override void Disable()
        {
            if (!Enabled) return;
            PluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            SimpleLog.Debug($"[{GetType().Name}] Reset");
            HandleBars(PluginInterface.Framework, true);
            Enabled = false;
        }

        public override void Dispose()
        {
            PluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            Enabled = false;
            Ready = false;
        }
    }
}