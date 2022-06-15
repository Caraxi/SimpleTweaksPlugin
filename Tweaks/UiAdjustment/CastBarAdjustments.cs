using System;
using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

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
        public bool ClassicSlideCast;
        public int ClassicSlideCastWidth = 3;
        public int ClassicSlideCastOverHeight = 0;

        public Alignment AlignName = Alignment.Left;
        public Alignment AlignCounter = Alignment.Right;

        public int OffsetNamePosition = 0;
        public int OffsetCounterPosition = 0;
    }

    public Configs Config { get; private set; }
    
    private float configAlignmentX;
    private delegate void CastBarOnUpdateDelegate(AddonCastBar* castBar, void* a2);
    private HookWrapper<CastBarOnUpdateDelegate> castBarOnUpdateHook;

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
            hasChanged |= ImGui.Checkbox(LocString("Classic Mode"), ref Config.ClassicSlideCast);
            if (Config.ClassicSlideCast) {
                ImGui.Indent();
                ImGui.Indent();
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.SliderInt(LocString("Width"), ref Config.ClassicSlideCastWidth, 1, 10);
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.SliderInt(LocString("Extra Height"), ref Config.ClassicSlideCastOverHeight, 0, 20);
                
                ImGui.Unindent();
                ImGui.Unindent();
            }
            hasChanged |= ImGui.SliderInt(LocString("SlideCast Offset Time"), ref Config.SlideCastAdjust, 0, 1000);
            hasChanged |= ImGui.ColorEdit4(LocString("SlideCast Marker Colour"), ref Config.SlideCastColor);
            hasChanged |= ImGui.ColorEdit4(LocString("SlideCast Ready Colour"), ref Config.SlideCastReadyColor);
            ImGui.Unindent();
            ImGui.Unindent();
        }

        ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);


        if (hasChanged) {
            UpdateCastBar(null, true);
        }
    };

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        castBarOnUpdateHook ??= Common.Hook<CastBarOnUpdateDelegate>("48 83 EC 38 48 8B 92", CastBarOnUpdateDetour);
        castBarOnUpdateHook.Enable();
        base.Enable();
    }

    public override void Disable() {
        castBarOnUpdateHook.Disable();
        UpdateCastBar(null, true);
        SaveConfig(Config);
        base.Disable();
    }

    private void CastBarOnUpdateDetour(AddonCastBar* castBar, void* a2) {
        castBarOnUpdateHook.Original(castBar, a2);
        
        try {
            UpdateCastBar(castBar);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
        
    }

    private void UpdateCastBar(AddonCastBar* castBar, bool reset = false) {
        if (castBar == null) {
            castBar = Common.GetUnitBase<AddonCastBar>();
            if (castBar == null) return;
        }
        
        if (castBar->AtkUnitBase.UldManager.NodeList == null || castBar->AtkUnitBase.UldManager.NodeListCount < 12) return;

        var barNode = castBar->AtkUnitBase.UldManager.NodeList[3];
            
        var icon = (AtkComponentNode*) castBar->AtkUnitBase.GetNodeById(8);
        var countdownText = castBar->AtkUnitBase.GetTextNodeById(7);
        var castingText = castBar->AtkUnitBase.GetTextNodeById(6);
        var skillNameText = castBar->AtkUnitBase.GetTextNodeById(4);
        var progressBar = (AtkNineGridNode*)castBar->AtkUnitBase.GetNodeById(11);
        var interruptedText = castBar->AtkUnitBase.GetTextNodeById(2);
        var slideMarker = (AtkNineGridNode*)null;
        var classicSlideMarker = (AtkImageNode*)null;

        for (var i = 0; i < castBar->AtkUnitBase.UldManager.NodeListCount; i++) {
            if (castBar->AtkUnitBase.UldManager.NodeList[i]->NodeID == CustomNodes.SlideCastMarker) {
                slideMarker = (AtkNineGridNode*) castBar->AtkUnitBase.UldManager.NodeList[i];
            }
            if (castBar->AtkUnitBase.UldManager.NodeList[i]->NodeID == CustomNodes.ClassicSlideCast) {
                classicSlideMarker = (AtkImageNode*) castBar->AtkUnitBase.UldManager.NodeList[i];
            }
        }
            
        if (reset) {
            icon->AtkResNode.ToggleVisibility(true);
            countdownText->AtkResNode.ToggleVisibility(true);
            castingText->AtkResNode.ToggleVisibility(true);
            skillNameText->AtkResNode.ToggleVisibility(true);

            UiHelper.SetSize(skillNameText, 170, null);
            UiHelper.SetPosition(skillNameText, barNode->X + 4, null);

            UiHelper.SetSize(countdownText, 42, null);
            UiHelper.SetPosition(countdownText, 170, null);
            interruptedText->AtkResNode.SetScale(1, 1);

            if (slideMarker != null) {
                slideMarker->AtkResNode.ToggleVisibility(false);
            }

            if (classicSlideMarker != null) {
                classicSlideMarker->AtkResNode.ToggleVisibility(false);
                if (classicSlideMarker->AtkResNode.PrevSiblingNode != null)
                    classicSlideMarker->AtkResNode.PrevSiblingNode->NextSiblingNode = classicSlideMarker->AtkResNode.NextSiblingNode;
                if (classicSlideMarker->AtkResNode.NextSiblingNode != null)
                    classicSlideMarker->AtkResNode.NextSiblingNode->PrevSiblingNode = classicSlideMarker->AtkResNode.PrevSiblingNode;
                castBar->AtkUnitBase.UldManager.UpdateDrawNodeList();

                IMemorySpace.Free(classicSlideMarker->PartsList->Parts->UldAsset, (ulong)sizeof(AtkUldPart));
                IMemorySpace.Free(classicSlideMarker->PartsList->Parts, (ulong)sizeof(AtkUldPart));
                IMemorySpace.Free(classicSlideMarker->PartsList, (ulong)sizeof(AtkUldPartsList));
                classicSlideMarker->AtkResNode.Destroy(true);
            }

            countdownText->AlignmentFontType = 0x25;
            skillNameText->AlignmentFontType = 0x03;
                
            return;
        }

        if (Config.RemoveIcon) icon->AtkResNode.ToggleVisibility(false);
        if (Config.RemoveName) skillNameText->AtkResNode.ToggleVisibility(false);
        if (Config.RemoveCounter) countdownText->AtkResNode.ToggleVisibility(false);
        if (Config.RemoveCastingText) castingText->AtkResNode.ToggleVisibility(false);

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

        if (Config.SlideCast && Config.ClassicSlideCast == false) {
            if (classicSlideMarker != null) classicSlideMarker->AtkResNode.ToggleVisibility(false);
            if (slideMarker == null) {
                // Create Node

                slideMarker = UiHelper.CloneNode(progressBar);
                slideMarker->AtkResNode.NodeID = CustomNodes.SlideCastMarker;
                castBar->AtkUnitBase.GetNodeById(10)->PrevSiblingNode = (AtkResNode*) slideMarker;
                slideMarker->AtkResNode.NextSiblingNode = castBar->AtkUnitBase.GetNodeById(10);
                slideMarker->AtkResNode.ParentNode = castBar->AtkUnitBase.GetNodeById(9);
                castBar->AtkUnitBase.UldManager.UpdateDrawNodeList();
            }

            if (slideMarker != null) {
                
                var slidePer = ((float)(castBar->CastTime * 10) - Config.SlideCastAdjust) / (castBar->CastTime * 10);
                var pos = 160 * slidePer;
                slideMarker->AtkResNode.ToggleVisibility(true);
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
            
        } else if (Config.SlideCast && Config.ClassicSlideCast) {
            if (slideMarker != null) slideMarker->AtkResNode.ToggleVisibility(false);
            if (classicSlideMarker == null) {
                if (progressBar == null) return;
                
                // Create Node
                    classicSlideMarker = IMemorySpace.GetUISpace()->Create<AtkImageNode>();
                    classicSlideMarker->AtkResNode.Type = NodeType.Image;
                    classicSlideMarker->AtkResNode.NodeID = CustomNodes.ClassicSlideCast;
                    classicSlideMarker->AtkResNode.Flags = (short)(NodeFlags.AnchorTop | NodeFlags.AnchorLeft);
                    classicSlideMarker->AtkResNode.DrawFlags = 0;
                    classicSlideMarker->WrapMode = 1;
                    classicSlideMarker->Flags = 0;

                    var partsList = (AtkUldPartsList*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPartsList), 8);
                    if (partsList == null) {
                        SimpleLog.Error("Failed to alloc memory for parts list.");
                        classicSlideMarker->AtkResNode.Destroy(true);
                        return;
                    }

                    partsList->Id = 0;
                    partsList->PartCount = 1;

                    var part = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPart), 8);
                    if (part == null) {
                        SimpleLog.Error("Failed to alloc memory for part.");
                        IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
                        classicSlideMarker->AtkResNode.Destroy(true);
                        return;
                    }

                    part->U = 30;
                    part->V = 30;
                    part->Width = 1;
                    part->Height = 12;

                    partsList->Parts = part;

                    var asset = (AtkUldAsset*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldAsset), 8);
                    if (asset == null) {
                        SimpleLog.Error("Failed to alloc memory for asset.");
                        IMemorySpace.Free(part, (ulong)sizeof(AtkUldPart));
                        IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
                        classicSlideMarker->AtkResNode.Destroy(true);
                        return;
                    }

                    asset->Id = 0;
                    asset->AtkTexture.Ctor();
                    part->UldAsset = asset;
                    classicSlideMarker->PartsList = partsList;

                    classicSlideMarker->LoadTexture("ui/uld/emjfacemask.tex");

                    classicSlideMarker->AtkResNode.ToggleVisibility(true);

                    classicSlideMarker->AtkResNode.SetWidth(1);
                    classicSlideMarker->AtkResNode.SetHeight(12);
                    classicSlideMarker->AtkResNode.SetPositionShort(100, 4);

                    
                    classicSlideMarker->AtkResNode.ParentNode = progressBar->AtkResNode.ParentNode;

                    var prev = progressBar->AtkResNode.PrevSiblingNode;
                    
                    progressBar->AtkResNode.PrevSiblingNode = (AtkResNode*)classicSlideMarker;
                    prev->NextSiblingNode = (AtkResNode*)classicSlideMarker;

                    classicSlideMarker->AtkResNode.PrevSiblingNode = prev;
                    classicSlideMarker->AtkResNode.NextSiblingNode = (AtkResNode*)progressBar;

                    castBar->AtkUnitBase.UldManager.UpdateDrawNodeList();
            }

            if (classicSlideMarker != null) {
                
                classicSlideMarker->AtkResNode.ToggleVisibility(true);
                
                var slidePer = ((float)(castBar->CastTime * 10) - Config.SlideCastAdjust) / (castBar->CastTime * 10);
                var pos = 160 * slidePer;

                
                classicSlideMarker->AtkResNode.SetWidth((ushort) Config.ClassicSlideCastWidth);
                classicSlideMarker->AtkResNode.SetHeight((ushort) (12 + Config.ClassicSlideCastOverHeight * 2));
                classicSlideMarker->AtkResNode.SetPositionFloat(pos, 4 - Config.ClassicSlideCastOverHeight);
                
                
                var c = (slidePer * 100) >= castBar->CastPercent ? Config.SlideCastColor : Config.SlideCastReadyColor;
                classicSlideMarker->AtkResNode.Color.R = (byte) (255 * c.X);
                classicSlideMarker->AtkResNode.Color.G = (byte) (255 * c.Y);
                classicSlideMarker->AtkResNode.Color.B = (byte) (255 * c.Z);

                classicSlideMarker->AtkResNode.Color.A = (byte) (255 * c.W);
                classicSlideMarker->AtkResNode.Flags_2 |= 1;
                
            }
        }
    }
}
