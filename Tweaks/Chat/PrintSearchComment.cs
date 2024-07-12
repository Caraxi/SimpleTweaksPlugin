#nullable enable
using System;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Print Search Comment")]
[TweakDescription("Prints the Search Comment of people, that get inspected, into the chat.")]
[TweakAuthor("Infi")]
[TweakReleaseVersion("0.0.0.0")]
public class PrintSearchComment : ChatTweaks.SubTweak {
    [TweakHook]
    private HookWrapper<AgentInspect.Delegates.ReceiveSearchComment>? receiveSearchComment;

    public override unsafe void Setup()
    {
        base.Setup();

        receiveSearchComment ??= Common.Hook<AgentInspect.Delegates.ReceiveSearchComment>(AgentInspect.MemberFunctionPointers.ReceiveSearchComment, ReceiveSearchCommentDetour);
        receiveSearchComment?.Enable();
    }

    private unsafe void ReceiveSearchCommentDetour(AgentInspect* agent, uint entityId, byte* searchComment)
    {
        receiveSearchComment!.Original(agent, entityId, searchComment);

        try
        {
            var searchInfo = MemoryHelper.ReadSeStringNullTerminated((nint) searchComment);

            var obj = Service.Objects.SearchById(entityId);
            if (obj == null || !obj.IsValid()) {
                SimpleLog.Debug("Unable to find ObjectID.");
            }
            else if (searchInfo.Payloads.Count > 0 && obj is IPlayerCharacter { ObjectKind: ObjectKind.Player } character)
            {
                var builder = new SeStringBuilder();
                builder.AddUiForeground(45);
                builder.AddText("Search Info from <");
                builder.Add(new PlayerPayload(character.Name.TextValue, character.HomeWorld.Id));
                builder.AddText(">");
                builder.AddUiForegroundOff();
                builder.AddText("\u000D"); // carriage return creates a new line
                builder.Append(searchInfo);
                Service.Chat.Print(builder.BuiltString);
            }
        }
        catch (Exception ex) {
            SimpleLog.Error(ex, $"Error in {GetType().Name} hook");
        }
    }
}