using System;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Shell;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using Lumina.Text;
using SeString = Dalamud.Game.Text.SeStringHandling.SeString;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Hide Duplicate Chats")]
[TweakAuthor("Abyeon")]
[TweakDescription("Suppresses duplicate chat messages and adds a counter to the previous chat.")]
[TweakReleaseVersion("1.0.0.0")]
public unsafe class HideDuplicateChats : ChatTweaks.SubTweak
{
    [TweakHook(typeof(RaptureLogModule), nameof(RaptureLogModule.PrintMessage), nameof(PrintMessageDetour))]
    private HookWrapper<RaptureLogModule.Delegates.PrintMessage> printMessageHook = null!;
    
    [TweakHook(typeof(RaptureLogModule), nameof(RaptureLogModule.FormatLogMessage), nameof(FormatLogDetour))]
    private HookWrapper<RaptureLogModule.Delegates.FormatLogMessage> formatLogHook = null!;

    protected override void Enable()
    {
        ReloadChat();
        base.Enable();
    }
    
    private static void ReloadChat()
    {
        var raptureLogModule = RaptureLogModule.Instance();
        for (var i = 0; i < 4; i++)
        {
            raptureLogModule->ChatTabIsPendingReload[i] = true;
        }
    }
    
    private LogMessage? lastMessage;
    private uint dupes;
    
    private uint PrintMessageDetour(RaptureLogModule* thisPtr, ushort logKindId, Utf8String* sender, Utf8String* message, int timestamp, bool silent)
    {
        var shouldReload = false;
        
        try
        {
            var time = Framework.Instance()->UtcTime;
            var msg = new LogMessage(logKindId, sender, message, time.Timestamp);
            
            if (lastMessage != null && lastMessage.Sender.Encode().SequenceEqual(msg.Sender.Encode()) && lastMessage.Message.Encode().SequenceEqual(msg.Message.Encode()))
            {
                ++dupes;
                shouldReload = true;
                return 0;
            }
            
            if (dupes > 1) shouldReload = true;
            
            dupes = 1;
            lastMessage = msg;
        }
        catch (Exception e)
        {
            SimpleLog.Error(e.ToString());
        } finally
        {
            if (shouldReload) ReloadChat();
        }

        return printMessageHook!.Original(thisPtr, logKindId, sender, message, timestamp, silent);
    }
    
    private uint FormatLogDetour(RaptureLogModule* thisPtr, uint logKindId, Utf8String* sender, Utf8String* message, int* timestamp, void* a6, Utf8String* a7, int chatTabIndex)
    {
        try
        {
            var msg = new LogMessage(logKindId, sender, message, *timestamp);
            using var newMsg = new Utf8String();

            if (lastMessage == null)
            {
                return formatLogHook!.Original(thisPtr, logKindId, sender, message, timestamp, a6, a7, chatTabIndex);
            }
            
            if (lastMessage.Equals(msg) && dupes > 1)
            {
                var sb = new SeStringBuilder()
                    .Append(message->ToString())
                    .PushColorType(4)
                    .Append(" (x" + dupes + ")")
                    .PopColorType();
            
                newMsg.SetString(sb.GetViewAsSpan());
                return formatLogHook!.Original(thisPtr, logKindId, sender, &newMsg, timestamp, a6, a7, chatTabIndex);
            }
        }
        catch (Exception e)
        {
            SimpleLog.Error(e.ToString());
        }
        
        return formatLogHook!.Original(thisPtr, logKindId, sender, message, timestamp, a6, a7, chatTabIndex);
    }
    
    private class LogMessage(uint logKindId, Utf8String* sender, Utf8String* message, int timestamp)
    {
        private readonly uint logKindId = logKindId;
        public readonly SeString Sender = SeString.Parse(sender->AsSpan());
        public readonly SeString Message = SeString.Parse(message->AsSpan());
        private readonly int timestamp = timestamp;

        public bool Equals(LogMessage other)
        {
            var distance = Math.Abs(other.timestamp - timestamp);
            
            return logKindId == other.logKindId && 
                   Sender.Encode().SequenceEqual(other.Sender.Encode()) && 
                   Message.Encode().SequenceEqual(other.Message.Encode()) &&
                   distance <= 2;
        }
    }
    
    protected override void Disable()
    {
        ReloadChat();
    }
}