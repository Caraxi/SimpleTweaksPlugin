#nullable enable
using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Search Info Print")]
[TweakDescription("Prints the Search Info of people, that get inspected, into the chat.")]
[TweakAuthor("Infi")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class SearchInfoPrint : ChatTweaks.SubTweak {
    [TweakHook, Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 49 8B E8 8B DA", DetourName = nameof(SearchInfoDownloadedDetour))]
    private HookWrapper<SearchInfoDownloadedDelegate>? searchInfoDownloadedHook;
    private delegate byte SearchInfoDownloadedDelegate(nint data, nint a2, nint searchInfoPtr, nint a4);

    private byte SearchInfoDownloadedDetour(nint data, nint a2, nint searchInfoPtr, nint a4)
    {
        var result = searchInfoDownloadedHook!.Original(data, a2, searchInfoPtr, a4);
        try {
            // Updated: 4.5
            var actorId = *(uint*) (data + 48);
            var searchInfo = MemoryHelper.ReadSeStringNullTerminated(searchInfoPtr);

            var obj = Service.Objects.FirstOrDefault(o => o.ObjectId == actorId);
            if (obj == null) {
                SimpleLog.Debug("Unable to find ObjectID.");
            }
            else if (searchInfo.Payloads.Count > 0) {
                var character = (PlayerCharacter) obj;
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

        return result;
    }
}