using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;
using System.Timers;
using Timer = System.Timers.Timer;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Break Time Command")]
[TweakDescription("Adds a command to setup a break time and automatically starting a ready check when the break is over.")]
[TweakAuthor("Tischel")]
public unsafe class BreakTimeCommand : CommandTweak
{
    protected override string Command => "break";
    protected override string HelpMessage => "Starts break time";

    private Timer? timer = null;

    protected override void OnCommand(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            if (ShowCommandErrors) Service.Chat.PrintError($"/{Command} <minutes> (0 to cancel)");
            return;
        }

        if (!int.TryParse(args, out var time))
        {
            if (ShowCommandErrors) Service.Chat.PrintError($"\"{args}\" is not a valid time duration!");
            return;
        }

        // stop current timer, if any
        if (timer != null)
        {
            timer.Stop();
            timer.Dispose();
        }

        // 0 cancels the ready check
        if (time == 0)
        {
            Service.Chat.Print($"Break cancelled");
            return;
        }

        // break start message
        int currentMinute = DateTime.Now.Minute;
        int targetMinute = currentMinute + time;
        if (targetMinute >= 60)
        {
            targetMinute -= 60;
        }

        if (targetMinute > 59)
        {
            Service.Chat.Print($"Break started: be back in {time} minutes");
        }
        else
        {
            Service.Chat.Print($"Break started: be back at xx:{targetMinute:D2}");
        }

        // schedule timer
        timer = new Timer(time * 60 * 1000);
        timer.Elapsed += OnTimerFinished;
        timer.AutoReset = false;
        timer.Start();
    }

    private static void OnTimerFinished(Object? source, ElapsedEventArgs e)
    {
        // break over message
        Service.Chat.Print($"Break is over");

        // initiate ready check
        ChatHelper.SendMessage("/readycheck");
    }
}
