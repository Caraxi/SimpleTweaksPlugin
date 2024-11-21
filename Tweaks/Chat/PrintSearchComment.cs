using System;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Print Search Comment")]
[TweakDescription("Prints the Search Comment of people, that get inspected, into the chat.")]
[TweakAuthor("Infi")]
[TweakReleaseVersion("1.10.3.0")]
public class PrintSearchComment : ChatTweaks.SubTweak {
    [TweakHook(typeof(AgentInspect), nameof(AgentInspect.ReceiveSearchComment), nameof(ReceiveSearchCommentDetour))]
    private HookWrapper<AgentInspect.Delegates.ReceiveSearchComment>? receiveSearchComment;

    private unsafe void ReceiveSearchCommentDetour(AgentInspect* agent, uint entityId, byte* searchComment) {
        receiveSearchComment!.Original(agent, entityId, searchComment);

        try {
            var searchInfo = SeString.Parse(searchComment);

            var obj = Service.Objects.SearchById(entityId);
            if (obj == null || !obj.IsValid()) {
                SimpleLog.Debug("Unable to find EntityID.");
            } else if (searchInfo.Payloads.Count > 0 && obj is IPlayerCharacter { ObjectKind: ObjectKind.Player } character) {
                var builder = new Lumina.Text.SeStringBuilder()
                    .PushColorType(45)
                    .Append("Search Info from <")
                    .PushLinkCharacter(character.Name.TextValue, character.HomeWorld.RowId)
                    .Append(character.Name.TextValue)
                    .PopLink()
                    .Append(">")
                    .PopColorType()
                    .Append("\r")
                    .Append(searchInfo)
                    .ToArray();

                Service.Chat.Print(SeString.Parse(builder));
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex, $"Error in {GetType().Name} hook");
        }
    }
}
