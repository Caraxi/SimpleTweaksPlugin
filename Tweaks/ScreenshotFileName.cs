using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Hooking;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

public class ScreenshotFileName : Tweak {
    public override string Name => "Screenshot File Name";
    public override string Description => "Change the file name format for screenshots.";
    protected override string Author => "PunishedPineapple";

    private const int maxFormatStringLength = 512;
    private IntPtr dateFormatStringPtr = IntPtr.Zero;
    private AsmHook screenshotDateFormatHook = null;
    private string previousDateFormatString = "ffxiv_%Y-%m-%d_%H%M%S";

    public class Configs : TweakConfig {
        [TweakConfigOption("Date Format String")]
        public string DateFormatString = "ffxiv_%Y-%m-%d_%H%M%S";
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => ( ref bool _ ) => {
        if(Config != null) {
            ImGui.Text("Format: ");
            ImGui.SameLine();
            ImGui.InputText("###Date Format String input", ref Config.DateFormatString, maxFormatStringLength - 1);
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if(ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                foreach(var entry in validPlaceholderFormats)
                {
                    ImGui.Text($"%%{entry.Placeholder} - {entry.GetDescription.Invoke()}");
                }
                ImGui.EndTooltip();
            }

            //  Check for invalid placeholders and characters so that the average user has a harder time shooting themselves in the foot.
            var placeholderIndices = new List<int>();
            for(int i = 0; i < Config.DateFormatString.Length; ++i)
            {
                if(Config.DateFormatString[i] == '%') placeholderIndices.Add(i + 1);
            }
            bool foundInvalidPlaceholders = false;
            foreach(var placeholderIndex in placeholderIndices)
            {
                if( placeholderIndex >= Config.DateFormatString.Length ||
                    !validPlaceholderFormats.Any((PlaceholderFormat j) => { return Config.DateFormatString[placeholderIndex] == j.Placeholder; })) {
                    foundInvalidPlaceholders = true;
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFF0000FF);
                    ImGui.Text($"The format string contains invalid placeholders!");
                    ImGui.PopStyleColor();
                    break;
                }
            }
            List<char> invalidChars = new();
            foreach(char c in Path.GetInvalidFileNameChars()) {
                if(Config.DateFormatString.Replace("%", "").Contains(c)) invalidChars.Add(c);
            }
            if(invalidChars.Any())
            {
                ImGui.PushStyleColor(ImGuiCol.Text, 0xFF0000FF);
                string foundChars = "";
                foreach(char c in invalidChars) foundChars += c;
                ImGui.Text($"Invalid characters used: {foundChars}");
                ImGui.PopStyleColor();
            }

            if(!Config.DateFormatString.Equals(previousDateFormatString) && !foundInvalidPlaceholders && !invalidChars.Any()) {
                UpdateDateFormatString(Config.DateFormatString);
                previousDateFormatString = Config.DateFormatString;
            }

            ImGui.TextWrapped("Make sure that the format is fully qualified (contains full date and time, including seconds), or you may lose screenshots because of duplicate file names.");            
        }
    };

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        if(dateFormatStringPtr == IntPtr.Zero) {
            dateFormatStringPtr = Marshal.AllocHGlobal( maxFormatStringLength );
        }

        if(screenshotDateFormatHook == null) {
            IntPtr pLoadScreenshotDateFormat = Service.SigScanner.ScanText("48 8B 4B ?? 48 8B C7 4C 8B 81") - 5;
            string[] hookAsm = {
                "use64",
                $"mov r8, 0x{dateFormatStringPtr:X}"
            };

            if(pLoadScreenshotDateFormat != IntPtr.Zero) {
                screenshotDateFormatHook = new( pLoadScreenshotDateFormat, hookAsm, "HookName_LoadScreenshotDateFormatString", AsmHookBehaviour.ExecuteFirst );
            }
            else {
                SimpleLog.Error( "Error in ScreenshotFileName.Enable: Unable to locate signature(s)." );
            }
        }

        UpdateDateFormatString(Config.DateFormatString);
        screenshotDateFormatHook?.Enable();
        base.Enable();
    }

    public override void Disable() {
        SaveConfig(Config);
        screenshotDateFormatHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        screenshotDateFormatHook?.Disable();
        screenshotDateFormatHook?.Dispose();
        if(dateFormatStringPtr != IntPtr.Zero) Marshal.FreeHGlobal(dateFormatStringPtr);
        base.Dispose();
    }

    private void UpdateDateFormatString(string str) {
        //  Encode, trim, and null-terminate the string.
        List<byte> bytes = new( Encoding.UTF8.GetBytes( str ) );
        while(bytes.Count >= maxFormatStringLength - 1) bytes.RemoveAt(bytes.Count - 1);
        bytes.Add(0);

        //  Write it to unmanaged memory.
        if(dateFormatStringPtr != IntPtr.Zero) {
            Marshal.Copy(bytes.ToArray(), 0, dateFormatStringPtr, bytes.Count);
        }
    }

    //  Do it like this so it's easy to localize if desired.
    private struct PlaceholderFormat {
        public delegate string GetDescriptionDelegate();
        public char Placeholder;
        public GetDescriptionDelegate GetDescription;
    }

    //  Only using a subset of available formats to keep things easier to validate.
    private static readonly PlaceholderFormat[] validPlaceholderFormats = {
        new(){Placeholder = 'Y', GetDescription = ()=>{ return "Four-digit year"; }},
        new(){Placeholder = 'y', GetDescription = ()=>{ return "Two-digit year"; }},
        new(){Placeholder = 'B', GetDescription = ()=>{ return "Month name (i.e, October)"; }},
        new(){Placeholder = 'b', GetDescription = ()=>{ return "Abbreviated month name (i.e., Oct)"; }},
        new(){Placeholder = 'm', GetDescription = ()=>{ return "Two-digit month"; }},
        new(){Placeholder = 'd', GetDescription = ()=>{ return "Two-digit day of the month"; }},
        new(){Placeholder = 'j', GetDescription = ()=>{ return "Three-digit day of the year"; }},
        new(){Placeholder = 'A', GetDescription = ()=>{ return "Day name (i.e., Monday)"; }},
        new(){Placeholder = 'a', GetDescription = ()=>{ return "Abbreviated day name (i.e., Mon)"; }},
        new(){Placeholder = 'H', GetDescription = ()=>{ return "Two-digit hour (24-hour)"; }},
        new(){Placeholder = 'I', GetDescription = ()=>{ return "Two-digit hour (12-hour)"; }},
        new(){Placeholder = 'p', GetDescription = ()=>{ return "a.m. or p.m."; }},
        new(){Placeholder = 'M', GetDescription = ()=>{ return "Two-digit minute"; }},
        new(){Placeholder = 'S', GetDescription = ()=>{ return "Two-digit second"; }},
    };
}