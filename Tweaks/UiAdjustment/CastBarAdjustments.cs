using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class CastBarAdjustments : UiAdjustments.SubTweak {
    public override string Name => "Cast Bar Adjustments";
    public override string Description => "Allows hiding or moving specific parts of the castbar.";
    public override IEnumerable<string> Tags => new[] {"SlideCast", "Slide Cast"};

    public class Configs : TweakConfig {
        public bool RemoveCastingText;
        public bool RemoveIcon;
        public bool RemoveCounter;
        public bool RemoveName;
        public bool RemoveInterruptedText;

        public bool SlideCast;
        public int SlideCastAdjust = 500;
        public Vector4 SlideCastColor = new Vector4(0.8F, 0.3F, 0.3F, 1);
        public Vector4 SlideCastReadyColor = new Vector4(0.3F, 0.8F, 0.3F, 1);

        public Alignment AlignName = Alignment.Left;
        public Alignment AlignCounter = Alignment.Right;

        public int OffsetNamePosition = 0;
        public int OffsetCounterPosition = 0;
    }

    public Configs Config { get; private set; }


        
    private float configAlignmentX;
        
    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        hasChanged |= ImGui.Checkbox(LocString("Hide Casting", "Hide 'Casting' Text"), ref Config.RemoveCastingText);
        hasChanged |= ImGui.Checkbox(LocString("Hide Icon"), ref Config.RemoveIcon);
        hasChanged |= ImGui.Checkbox(LocString("Hide Interrupted Text"), ref Config.RemoveInterruptedText);
        hasChanged |= ImGui.Checkbox(LocString("Hide Countdown Text"), ref Config.RemoveCounter);
        if (Config.RemoveCastingText && !Config.RemoveCounter) {
            ImGui.SameLine();
            if (ImGui.GetCursorPosX() > configAlignmentX) configAlignmentX = ImGui.GetCursorPosX();
            ImGui.SetCursorPosX(configAlignmentX);
            hasChanged |= ImGuiExt.HorizontalAlignmentSelector(LocString("Align Countdown Text"), ref Config.AlignCounter);

            ImGui.SetCursorPosX(configAlignmentX);
            ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.InputInt(LocString("Offset") + "##offsetCounterPosition", ref Config.OffsetCounterPosition);
            if (Config.OffsetCounterPosition < -100) Config.OffsetCounterPosition = -100;
            if (Config.OffsetCounterPosition > 100) Config.OffsetCounterPosition = 100;

        }
        hasChanged |= ImGui.Checkbox(LocString("Hide Ability Name"), ref Config.RemoveName);
        if (!Config.RemoveName) {
            ImGui.SameLine();
            if (ImGui.GetCursorPosX() > configAlignmentX) configAlignmentX = ImGui.GetCursorPosX();
            ImGui.SetCursorPosX(configAlignmentX);
            hasChanged |= ImGuiExt.HorizontalAlignmentSelector(LocString("Align Ability Name"), ref Config.AlignName);
            ImGui.SetCursorPosX(configAlignmentX);
            ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.InputInt(LocString("Offset") + "##offsetNamePosition", ref Config.OffsetNamePosition);

            if (Config.OffsetNamePosition < -100) Config.OffsetNamePosition = -100;
            if (Config.OffsetNamePosition > 100) Config.OffsetNamePosition = 100;
        }

        hasChanged |= ImGui.Checkbox(LocString("Show SlideCast Marker"), ref Config.SlideCast);
        if (Config.SlideCast) {
            ImGui.Indent();
            ImGui.Indent();
            hasChanged |= ImGui.SliderInt(LocString("SlideCast Offset Time"), ref Config.SlideCastAdjust, 0, 1000);
            hasChanged |= ImGui.ColorEdit4(LocString("SlideCast Marker Colour"), ref Config.SlideCastColor);
            hasChanged |= ImGui.ColorEdit4(LocString("SlideCast Ready Colour"), ref Config.SlideCastReadyColor);
            ImGui.Unindent();
            ImGui.Unindent();
        }

        ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);


        if (hasChanged) {
            UpdateCastBar(true);
        }
    };

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.Framework.Update += FrameworkOnUpdate;
        base.Enable();
    }

    public override void Disable() {
        Service.Framework.Update -= FrameworkOnUpdate;
        UpdateCastBar(true);
        SaveConfig(Config);
        base.Disable();
    }

    private void FrameworkOnUpdate(Framework framework) {
        try {
            UpdateCastBar();
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }
        
    public void UpdateCastBar(bool reset = false) {
        var castBar = Common.GetUnitBase<AddonCastBar>();
        if (castBar == null) return;
        if (castBar->AtkUnitBase.UldManager.NodeList == null || castBar->AtkUnitBase.UldManager.NodeListCount < 12) return;

        var barNode = castBar->AtkUnitBase.UldManager.NodeList[3];
            
        var icon = (AtkComponentNode*) castBar->AtkUnitBase.UldManager.NodeList[7];
        var countdownText = (AtkTextNode*) castBar->AtkUnitBase.UldManager.NodeList[8];
        var castingText = (AtkTextNode*) castBar->AtkUnitBase.UldManager.NodeList[9];
        var skillNameText = (AtkTextNode*) castBar->AtkUnitBase.UldManager.NodeList[11];
        var progressBar = (AtkNineGridNode*) castBar->AtkUnitBase.UldManager.NodeList[5];
        var interruptedText = (AtkTextNode*) castBar->AtkUnitBase.UldManager.NodeList[12];
        var slideMarker = (AtkNineGridNode*) null;

        for (var i = 13; i < castBar->AtkUnitBase.UldManager.NodeListCount; i++) {
            if (castBar->AtkUnitBase.UldManager.NodeList[i]->NodeID == CustomNodes.SlideCastMarker) {
                slideMarker = (AtkNineGridNode*) castBar->AtkUnitBase.UldManager.NodeList[i];
                break;
            }
        }
            
        if (reset) {
            UiHelper.Show(icon);
            UiHelper.Show(countdownText);
            UiHelper.Show(castingText);
            UiHelper.Show(skillNameText);

            UiHelper.SetSize(skillNameText, 170, null);
            UiHelper.SetPosition(skillNameText, barNode->X + 4, null);

            UiHelper.SetSize(countdownText, 42, null);
            UiHelper.SetPosition(countdownText, 170, null);
            interruptedText->AtkResNode.SetScale(1, 1);

            if (slideMarker != null) {
                UiHelper.Hide(slideMarker);
            }

            countdownText->AlignmentFontType = 0x25;
            skillNameText->AlignmentFontType = 0x03;
                
            return;
        }

        if (Config.RemoveIcon) UiHelper.Hide(icon);
        if (Config.RemoveName) UiHelper.Hide(skillNameText);
        if (Config.RemoveCounter) UiHelper.Hide(countdownText);
        if (Config.RemoveCastingText) UiHelper.Hide(castingText);

        if (Config.RemoveCastingText && !Config.RemoveCounter) {
            countdownText->AlignmentFontType = (byte) (0x20 | (byte) Config.AlignCounter);
            UiHelper.SetSize(countdownText, barNode->Width - 8, null);
            UiHelper.SetPosition(countdownText, (barNode->X + 4) + Config.OffsetCounterPosition, null);
        } else {
            countdownText->AlignmentFontType = (byte)(0x20 | (byte)Alignment.Right);
            UiHelper.SetSize(countdownText, 42, null);
            UiHelper.SetPosition(countdownText, 170, null);
        }

        if (!Config.RemoveName) {
            skillNameText->AlignmentFontType = (byte) (0x00 | (byte) Config.AlignName);
            UiHelper.SetPosition(skillNameText, (barNode->X + 4) + Config.OffsetNamePosition, null);
            UiHelper.SetSize(skillNameText, barNode->Width - 8, null);
        }

        if (Config.RemoveInterruptedText) {
            interruptedText->AtkResNode.SetScale(0, 0);
        }

        if (Config.SlideCast) {
            if (slideMarker == null) {
                // Create Node
                UiHelper.ExpandNodeList((AtkUnitBase*)castBar, 1);
                slideMarker = UiHelper.CloneNode(progressBar);
                slideMarker->AtkResNode.NodeID = CustomNodes.SlideCastMarker;
                castBar->AtkUnitBase.UldManager.NodeList[6]->PrevSiblingNode = (AtkResNode*) slideMarker;
                slideMarker->AtkResNode.NextSiblingNode = castBar->AtkUnitBase.UldManager.NodeList[6];
                slideMarker->AtkResNode.ParentNode = castBar->AtkUnitBase.UldManager.NodeList[3];
                castBar->AtkUnitBase.UldManager.NodeList[castBar->AtkUnitBase.UldManager.NodeListCount++] = (AtkResNode*)slideMarker;
            }

            if (slideMarker != null) {
                var slidePer = ((float)(castBar->CastTime * 10) - Config.SlideCastAdjust) / (castBar->CastTime * 10);
                var pos = 160 * slidePer;
                UiHelper.Show(slideMarker);
                UiHelper.SetSize(slideMarker, 168 - (int)pos, 20);
                UiHelper.SetPosition(slideMarker, pos - 8, 0);
                var c = (slidePer * 100) >= castBar->CastPercent ? Config.SlideCastColor : Config.SlideCastReadyColor;
                slideMarker->AtkResNode.AddRed = (byte) (255 * c.X);
                slideMarker->AtkResNode.AddGreen = (byte) (255 * c.Y);
                slideMarker->AtkResNode.AddBlue = (byte) (255 * c.Z);
                slideMarker->AtkResNode.MultiplyRed = (byte) (255 * c.X);
                slideMarker->AtkResNode.MultiplyGreen = (byte) (255 * c.Y);
                slideMarker->AtkResNode.MultiplyBlue = (byte) (255 * c.Z);
                slideMarker->AtkResNode.Color.A = (byte) (255 * c.W);
                slideMarker->PartID = 0;
                slideMarker->AtkResNode.Flags_2 |= 1;
            }
        }
    }
}