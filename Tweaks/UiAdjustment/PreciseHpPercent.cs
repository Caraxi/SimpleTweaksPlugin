using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Internal;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SimpleTweaksPlugin
{
    public partial class UiAdjustmentsConfig
    {
        public PreciseHpPercent.Configs PreciseHpPercent = new();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment
{
    public unsafe class PreciseHpPercent : UiAdjustments.SubTweak
    {
        public override string Name => "Precise HP%";
        protected override string Author => "maributt";
        public override string Description => "Enables floating point precision for HP%% displays.";

        public class Configs
        {
            public bool Main = true;
            public int TargetPrecision = 1;
            public bool ManualOffset;
            public int MainOffset = 46;

            public bool Focus = true;
            public int FocusPrecision = 1;

            public int RoundingMethod = 0;
        }

        private readonly Dictionary<string, string> roundingMethods = new()
        {
            { "rounding down", "Default behavior of the game.\nRounds HP%% down to the closest decimal point.\ne.g.: 41.67%% => 41.6%%, 41.65%% => 41.6%%, 41.64%% => 41.6%%\n           (at a precision of 1 decimal place)" },
            { "rounding to the nearest", "Rounds HP%% to the nearest decimal point.\ne.g.: 41.67%% => 41.7%%, 41.65%% => 41.7%%, 41.64%% => 41.6%%\n           (at a precision of 1 decimal place)" },
            { "rounding up", "Rounds HP%% up to the closest decimal point.\ne.g.: 41.67%% => 41.7%%, 41.65%% => 41.7%%, 41.64%% => 41.7%%\n           (at a precision of 1 decimal place)" }
        };

        public Configs Config => PluginConfig.UiAdjustments.PreciseHpPercent;

        public override void Enable()
        {
            PluginInterface.Framework.OnUpdateEvent += FrameworkUpdate;
            base.Enable();
        }

        public override void Disable()
        {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkUpdate;
            base.Disable();
            Update(true);
        }

        private void FrameworkUpdate(Framework framework)
        {
            try
            {
                Update();
            }
            catch (Exception ex)
            {
                SimpleLog.Error(ex);
            }
        }

        private void Update(bool reset = false)
        {
            var target = PluginInterface.ClientState.Targets.SoftTarget ?? PluginInterface.ClientState.Targets.CurrentTarget;
            if (target != null || reset)
            {
                if (target is not Chara || ((Chara)target).MaxHp == 0) reset = true;
                UpdateHpName("_TargetInfo", 36, 38, 39, 40, target, reset);
                UpdateHpName("_TargetInfoMainTarget", 5, 7, 8, 9, target, reset);
            }
            if (PluginInterface?.ClientState?.Targets?.FocusTarget != null || reset)
            {

                if (PluginInterface?.ClientState?.Targets?.FocusTarget is not Chara
                    || ((Chara)(PluginInterface?.ClientState?.Targets?.FocusTarget)).MaxHp == 0)
                    reset = true;
                var ui = (AtkUnitBase*)PluginInterface?.Framework.Gui.GetUiObjectByName("_FocusTargetInfo", 1);
                if (ui != null && (ui->IsVisible || reset))
                {
                    UpdateFocusTarget(ui, PluginInterface?.ClientState?.Targets?.FocusTarget, reset);
                }
            }
        }

        private void UpdateHpName(string uiObjectName, int gaugeBarIndex, int hpNodeIndex, int nameNodeIndex, int expectedUldNodelistLen, Actor target, bool reset)
        {
            var ui = (AtkUnitBase*)PluginInterface?.Framework.Gui.GetUiObjectByName(uiObjectName, 1);
            if (ui != null && (ui->IsVisible || reset))
            {
                if (!Config.Main) reset = true;
                if (ui->UldManager.NodeList == null || ui->UldManager.NodeListCount < expectedUldNodelistLen) return;
                var hpNode = (AtkTextNode*)ui->UldManager.NodeList[hpNodeIndex];
                var nameNode = (AtkTextNode*)ui->UldManager.NodeList[nameNodeIndex];
                var gaugeBar = (AtkComponentNode*)ui->UldManager.NodeList[gaugeBarIndex];
                hpNode->FontSize = (byte)(reset ? 14 : 0);
                nameNode->FontSize = (byte)(reset ? 14 : 0);
                UpdateHpPercent(gaugeBar, hpNode, target, PrecisePercentHPNodeID, reset);
                UpdateTargetName(gaugeBar, nameNode, target, TargetNameNodeID, reset);
            }
        }
        private void UpdateFocusTarget(AtkUnitBase* unitBase, Actor target, bool reset = false)
        {
            if (!Config.Focus) reset = true;
            if (unitBase == null || unitBase->UldManager.NodeList == null || unitBase->UldManager.NodeListCount < 11) return;
            var textNode = (AtkTextNode*)unitBase->UldManager.NodeList[10];
            var gaugeBar = (AtkComponentNode*)unitBase->UldManager.NodeList[2];
            textNode->FontSize = (byte)(reset ? 14 : 0);
            UpdateFocusTargetHp(gaugeBar, textNode, target, reset);
        }

        private const int PrecisePercentHPNodeID = 99999002;
        private const int TargetNameNodeID = 99999003;
        private const int FocusTargetNameHpNodeID = 99999001;

        private void UpdateFocusTargetHp(AtkComponentNode* unitBase, AtkTextNode* cloneTextNode, Actor target, bool reset = false)
        {
            var textNode = UpdateBase(unitBase, cloneTextNode, FocusTargetNameHpNodeID, target, 0, reset, false);
            if (textNode == null || reset || target is not Chara chara) return;

            var sTextNode = cloneTextNode->NodeText.GetString().Split('%');
            UiHelper.SetPosition(textNode, 2, -2);
            textNode->SetText(sTextNode.Length > 1 && chara.CurrentHp != chara.MaxHp ? FormatHp(chara, Config.FocusPrecision) + sTextNode[1] : cloneTextNode->NodeText.GetString());
        }
        private void UpdateHpPercent(AtkComponentNode* unitBase, AtkTextNode* originalNode, Actor target, uint NodeID, bool reset = false)
        {
            var textNode = UpdateBase(unitBase, originalNode, NodeID, target, 4, reset);
            if (target == null || reset || target is not Chara chara) return;

            UiHelper.SetPosition(textNode, 4, 0);
            textNode->SetText(chara.CurrentHp == chara.MaxHp ? originalNode->NodeText.GetString() : FormatHp(chara, Config.TargetPrecision));
        }
        private void UpdateTargetName(AtkComponentNode* unitBase, AtkTextNode* cloneTextNode, Actor target, uint NodeID, bool reset = false)
        {
            var textNode = UpdateBase(unitBase, cloneTextNode, NodeID, target, 4, reset);
            if (target == null || reset || target is not Chara chara) return;

            UiHelper.SetPosition(textNode, Config.ManualOffset ? Config.MainOffset : 46 + (Config.TargetPrecision == 0 ? 0 : 10 * Config.TargetPrecision), 0);
            textNode->SetText(cloneTextNode->NodeText.GetString());
        }

        private string FormatHp(Chara chara, int precision)
        {
            var hp = (chara.CurrentHp + 0.0f) / (chara.MaxHp + 0.0f) * 100;
            if (hp < 1 && precision == 0) precision += 1;
            switch (Config.RoundingMethod)
            {
                case 0:
                    hp = (float)RoundDown((decimal)hp, precision);
                    break;
                case 1:
                    hp = (float)Math.Round(hp, precision);
                    break;
                case 2:
                    hp = (float)RoundUp((decimal)hp, precision);
                    break;
            }
            var hpstr = hp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (hp < 10 && hp >= 1) hpstr = "0" + hpstr;
            var decimalplaces = hpstr.IndexOf(".", StringComparison.Ordinal) == -1 ? 0 : hpstr.Substring(hpstr.IndexOf(".", StringComparison.Ordinal) + 1).Length;
            if (hp == 0 && chara.CurrentHp != 0) hpstr += "." + "".PadRight(precision, '0') + "1";
            else if (decimalplaces < precision)
            {
                if (hp % 1 == 0) hpstr += ".";
                hpstr += "".PadRight(precision - decimalplaces, '0');
            }
            return hpstr + "%";
        }

        private AtkTextNode* UpdateBase(AtkComponentNode* gauge, AtkTextNode* cloneTextNode, uint NodeID, Actor target, int startSearchIndex, bool reset = false, bool setPosition = true)
        {
            if (gauge == null || (ushort)gauge->AtkResNode.Type < 1000) return null;
            AtkTextNode* textNode = null;

            for (var i = startSearchIndex; i < gauge->Component->UldManager.NodeListCount; i++)
            {
                var node = gauge->Component->UldManager.NodeList[i];
                if (node->NodeID == NodeID)
                {
                    textNode = (AtkTextNode*)node;
                    break;
                }
            }

            if (textNode == null && reset) return null; // Nothing to clean

            if (textNode == null)
            {
                textNode = UiHelper.CloneNode(cloneTextNode);
                textNode->AtkResNode.NodeID = NodeID;
                var newStrPtr = Common.Alloc(512);
                textNode->NodeText.StringPtr = (byte*)newStrPtr;
                textNode->NodeText.BufSize = 512;
                textNode->SetText("");
                UiHelper.ExpandNodeList(gauge, 1);
                gauge->Component->UldManager.NodeList[gauge->Component->UldManager.NodeListCount++] = (AtkResNode*)textNode;

                var nextNode = gauge->Component->UldManager.RootNode;
                while (nextNode->PrevSiblingNode != null)
                {
                    nextNode = nextNode->PrevSiblingNode;
                }
                textNode->AtkResNode.ParentNode = ((AtkResNode*)cloneTextNode)->ParentNode;
                textNode->AtkResNode.ChildNode = null;
                textNode->AtkResNode.PrevSiblingNode = null;
                textNode->AtkResNode.NextSiblingNode = nextNode;
                nextNode->PrevSiblingNode = (AtkResNode*)textNode;
                textNode->FontSize = 14;
            }

            if (reset)
            {
                UiHelper.Hide(textNode);
                return null;
            }

            UiHelper.SetSize(textNode, ((AtkResNode*)cloneTextNode)->Width, ((AtkResNode*)cloneTextNode)->Height);
            if (setPosition)
                UiHelper.SetPosition(textNode, ((AtkResNode*)cloneTextNode)->X, ((AtkResNode*)cloneTextNode)->Y);
            UiHelper.Show(textNode);
            textNode->EdgeColor = cloneTextNode->EdgeColor;
            textNode->TextColor = cloneTextNode->TextColor;
            if (target is Chara chara) return textNode;

            textNode->SetText("");
            cloneTextNode->FontSize = 14; // restore font size (without showing og as game handles display already)
            UiHelper.Hide(textNode);
            return null;
        }
        private decimal RoundDown(decimal i, double decimalPlaces)
        {
            var power = Convert.ToDecimal(Math.Pow(10, decimalPlaces));
            return Math.Floor(i * power) / power;
        }
        private decimal RoundUp(decimal i, double decimalPlaces)
        {
            var power = Convert.ToDecimal(Math.Pow(10, decimalPlaces));
            return Math.Ceiling(i * power) / power;
        }
        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) =>
        {
            hasChanged |= DrawTargetConfig(ref hasChanged, "Main Target  ", ref Config.Main, ref Config.TargetPrecision);
            if (Config.Main)
            {
                ImGui.SameLine();
                ImGui.Dummy(new Vector2(20, 0));
                ImGui.SameLine();
                hasChanged |= ImGui.Checkbox("Manually offset target's name?", ref Config.ManualOffset);
                if (Config.ManualOffset)
                {
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100);
                    hasChanged |= ImGui.InputInt("X Offset", ref Config.MainOffset, 1, 2);
                }
            }
            hasChanged |= DrawTargetConfig(ref hasChanged, "Focus Target", ref Config.Focus, ref Config.FocusPrecision);
            
            ImGui.Text("Calculate HP%% by"); ImGui.SameLine();
            var currentMethodStr = roundingMethods.Keys.ToArray()[Config.RoundingMethod];
            ImGui.SetNextItemWidth(ImGui.CalcTextSize(currentMethodStr).X + 40);
            hasChanged |= ImGui.Combo("##roundingMethod", ref Config.RoundingMethod, roundingMethods.Keys.ToArray(),
                roundingMethods.Keys.Count); ImGui.SameLine();
            ImGui.Text("number.");
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(roundingMethods[currentMethodStr]);
        };
        private bool DrawTargetConfig(ref bool hasChanged, string label, ref bool toggle, ref int precision)
        {
            hasChanged |= ImGui.Checkbox($"##{label}TargetPreciseHpEnabled", ref toggle);
            ImGui.SameLine();
            ImGui.Text($"{label} ");
            if (toggle)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(77f);
                if (ImGui.InputInt($"Decimal Places##{label}HpPrecision", ref precision, 1, 1))
                {
                    if (precision < 0) precision = 0;
                    if (precision > 6) precision = 6;
                    hasChanged = true;
                };
            }
            return hasChanged;
        }
    }

}