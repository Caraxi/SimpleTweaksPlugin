using System;
using System.Diagnostics;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class TimeUntilGpMax : UiAdjustments.SubTweak {
        public override string Name => "Time Until GP Max";
        public override string Description => "Shows a countdown when playing Gathering classes to estimate the time until their GP is capped.";
        
        private readonly Stopwatch lastGpChangeStopwatch = new();
        private readonly Stopwatch lastUpdate = new();
        private int lastGp = -100;
        private int gpPerTick = 5;
        private float timePerTick = 3f;
        public delegate void UpdateParamDelegate(uint a1, uint* a2, byte a3);
        private Hook<UpdateParamDelegate> updateParamHook;
        
        public override void Enable() {
            lastUpdate.Restart();
            updateParamHook ??= new Hook<UpdateParamDelegate>(Common.Scanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 83 3D ?? ?? ?? ?? ?? 41 0F B6 E8 48 8B DA 8B F1 0F 84 ?? ?? ?? ?? 48 89 7C 24"), new UpdateParamDelegate(UpdateParamDetour));
            updateParamHook.Enable();
            PluginInterface.Framework.OnUpdateEvent += FrameworkUpdate;
            base.Enable();
        }

        private void UpdateParamDetour(uint a1, uint* a2, byte a3) {
            updateParamHook.Original(a1, a2, a3);
            try {
                if (PluginInterface?.ClientState?.LocalPlayer == null) return;
                if (!lastGpChangeStopwatch.IsRunning) {
                    lastGpChangeStopwatch.Restart();
                } else {
                    if (PluginInterface.ClientState.LocalPlayer.CurrentGp > lastGp && lastGpChangeStopwatch.ElapsedMilliseconds > 1000 && lastGpChangeStopwatch.ElapsedMilliseconds < 4000) {
                        var diff = PluginInterface.ClientState.LocalPlayer.CurrentGp - lastGp;
                        if (diff < 20) {
                            gpPerTick = diff;
                            lastGp = PluginInterface.ClientState.LocalPlayer.CurrentGp;
                            lastGpChangeStopwatch.Restart();
                        }
                    }

                    if (PluginInterface.ClientState.LocalPlayer.CurrentGp != lastGp) {
                        lastGp = PluginInterface.ClientState.LocalPlayer.CurrentGp;
                        lastGpChangeStopwatch.Restart();
                    }
                }
            } catch (Exception ex) {
                Plugin.Error(this, ex, false, "Error in UpdateParamDetour");
            }
        }

        public override void Disable() {
            lastUpdate.Stop();
            updateParamHook?.Disable();
            PluginInterface.Framework.OnUpdateEvent -= FrameworkUpdate;
            Update(true);
            base.Disable();
        }

        public override void Dispose() {
            updateParamHook?.Disable();
            updateParamHook?.Dispose();
            base.Dispose();
        }

        private void FrameworkUpdate(Framework framework) {
            try {
                if (!lastUpdate.IsRunning) lastUpdate.Restart();
                if (lastUpdate.ElapsedMilliseconds < 1000) return;
                lastUpdate.Restart();
                Update();
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        private void Update(bool reset = false) {
            if (PluginInterface.ClientState?.LocalPlayer?.ClassJob?.GameData?.ClassJobCategory?.Row != 32) reset = true;
            var paramWidget = Common.GetUnitBase("_ParameterWidget");
            if (paramWidget == null) return;

            var gatheringWidget = Common.GetUnitBase("Gathering");
            if (gatheringWidget == null) gatheringWidget = Common.GetUnitBase("GatheringMasterpiece");
            
            AtkTextNode* textNode = null;
            for (var i = 0; i < paramWidget->UldManager.NodeListCount; i++) {
                var node = paramWidget->UldManager.NodeList[i];
                if (node->Type == NodeType.Text && node->NodeID == CountdownNodeId) {
                    textNode = (AtkTextNode*) node;
                    break;
                }
            }

            if (textNode == null && reset) return;

            if (textNode == null) {
                textNode = UiHelper.CloneNode((AtkTextNode*) paramWidget->UldManager.NodeList[3]);
                textNode->AtkResNode.NodeID = CountdownNodeId;
                var newStrPtr = Common.Alloc(512);
                textNode->NodeText.StringPtr = (byte*) newStrPtr;
                textNode->NodeText.BufSize = 512;
                UiHelper.SetText(textNode, "00:00");
                UiHelper.ExpandNodeList(paramWidget, 1);
                paramWidget->UldManager.NodeList[paramWidget->UldManager.NodeListCount++] = (AtkResNode*) textNode;
                
                textNode->AtkResNode.ParentNode = paramWidget->UldManager.NodeList[3]->ParentNode;
                textNode->AtkResNode.ChildNode = null;
                textNode->AtkResNode.PrevSiblingNode = null;
                textNode->AtkResNode.NextSiblingNode = paramWidget->UldManager.NodeList[3];
                paramWidget->UldManager.NodeList[3]->PrevSiblingNode = (AtkResNode*) textNode;
            }

            if (reset) {
                UiHelper.Hide(textNode);
                return;
            }
            
            
            if (PluginInterface.ClientState.LocalPlayer.MaxGp - PluginInterface.ClientState.LocalPlayer.CurrentGp > 0) {
                UiHelper.Show(textNode);
                UiHelper.SetPosition(textNode, 210, null);
                textNode->AlignmentFontType = 0x15;
                
                var gpPerSecond = gpPerTick / timePerTick;
                var secondsUntilFull = (PluginInterface.ClientState.LocalPlayer.MaxGp - PluginInterface.ClientState.LocalPlayer.CurrentGp) / gpPerSecond;

                if (gatheringWidget == null) {
                    secondsUntilFull += timePerTick - (float)lastGpChangeStopwatch.Elapsed.TotalSeconds;
                } else {
                    lastGpChangeStopwatch.Restart();
                }
            
                var minutesUntilFull = 0;
                while (secondsUntilFull >= 60) {
                    minutesUntilFull += 1;
                    secondsUntilFull -= 60;
                }
                UiHelper.SetText(textNode, $"{minutesUntilFull:00}:{(int)secondsUntilFull:00}");
            } else {
                UiHelper.Hide(textNode);
            }
        }
        
        private const int CountdownNodeId = 99990003;
    }
}
