using System;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Screenshot File Name")]
[TweakDescription("Change the file name format for screenshots.")]
[TweakAutoConfig]
[Changelog("1.10.2.0", "Added ability to use folders in screenshot path.")]
[Changelog("1.10.2.0", "Added ability to use character name and location in screenshot path.")]
public unsafe class ScreenshotFileName : Tweak {
    public class Configs : TweakConfig {
        public string DateFormatString = "ffxiv_%Y-%m-%d_%H%M%S";
    }

    [TweakConfig] public Configs Config { get; private set; }

    protected void DrawConfig() {
        ImGui.InputText("Format", ref Config.DateFormatString, 512);

        using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled))) {
            ImGui.TextUnformatted(GetScreenshotName());
        }

        if (!ImGui.CollapsingHeader("Placeholders")) return;
        if (ImGui.BeginTable("Placeholders", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings)) {
            ImGui.TableSetupColumn("Placeholder", ImGuiTableColumnFlags.WidthFixed, 80 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 140 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            foreach (var ph in ValidPlaceholderFormats) {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"%{ph.Placeholder}");
                if (ImGui.IsItemClicked()) ImGui.SetClipboardText($"%{ph.Placeholder}");
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to Copy");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{ph.GetValue()}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{ph.GetDescription()}");
            }

            ImGui.EndTable();
        }
    }

    private delegate char* GetPath(char* destination, byte* p);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 48 8B C7 49 8D 9E", DetourName = nameof(GetPathDetour))]
    private HookWrapper<GetPath> getPathHook;

    private string GetScreenshotName() {
        var str = $"{Config.DateFormatString}";
        foreach (var ph in ValidPlaceholderFormats) {
            if (str.Contains($"%{ph.Placeholder}")) str = str.Replace($"%{ph.Placeholder}", ph.GetValue());
        }

        return string.Join('/', str.Split('/', '\\').Select(s => string.Join('_', s.Split(Path.GetInvalidFileNameChars())))).Trim('/').Trim();
    }

    private char* GetPathDetour(char* destination, byte* p) {
        try {
            var pStr = MemoryHelper.ReadString((nint)p, 64);
            if (pStr.StartsWith("ffxiv_") && (pStr.EndsWith(".png") || pStr.EndsWith(".jpg") || pStr.EndsWith(".bmp"))) {
                var newName = $"{GetScreenshotName()}.{pStr.Split('.').Last()}";

                var bytes = Encoding.UTF8.GetBytes(newName);
                var b = stackalloc byte[bytes.Length + 1];
                for (var byteIndex = 0; byteIndex < bytes.Length; byteIndex++) b[byteIndex] = bytes[byteIndex];
                b[bytes.Length] = 0;
                var o = getPathHook.Original(destination, b);
                var str = string.Empty;
                var i = 0;
                while (o[i] != '\0') {
                    str += o[i++];
                }

                var fileInfo = new FileInfo(str);
                if (fileInfo.Exists) {
                    Service.Chat.PrintError($"Screenshot Already Exists: {str}");
                }

                fileInfo.Directory?.Create();

                return o;
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }

        return getPathHook.Original(destination, p);
    }

    private record PlaceholderFormat(string Placeholder, Func<string> GetDescription, Func<string> GetValue);

    private static readonly PlaceholderFormat[] ValidPlaceholderFormats = [
        new("Y", () => "Four-digit year", () => DateTime.Now.ToString("yyyy")),
        new("y", () => "Two-digit year", () => DateTime.Now.ToString("yy")),
        new("B", () => "Month name", () => DateTime.Now.ToString("MMMM")),
        new("b", () => "Abbreviated month name", () => DateTime.Now.ToString("MMM")),
        new("m", () => "Two-digit month", () => DateTime.Now.ToString("MM")),
        new("d", () => "Two-digit day of the month", () => DateTime.Now.ToString("dd")),
        new("j", () => "Three-digit day of the year", () => (DateTime.Now.DayOfYear - 1).ToString("000")),
        new("A", () => "Day name", () => DateTime.Now.ToString("dddd")),
        new("a", () => "Abbreviated day name", () => DateTime.Now.ToString("ddd")),
        new("H", () => "Two-digit hour (24-hour)", () => DateTime.Now.ToString("HH")),
        new("I", () => "Two-digit hour (12-hour)", () => DateTime.Now.ToString("hh")),
        new("p", () => "a.m. or p.m.", () => DateTime.Now.Hour < 12 ? "a.m." : "p.m."),
        new("M", () => "Two-digit minute", () => DateTime.Now.ToString("mm")),
        new("S", () => "Two-digit second", () => DateTime.Now.ToString("ss")),
        new("ChrName", () => "Current character name", () => UIState.Instance()->PlayerState.CharacterNameString),
        new("Location", () => "Current location name", () => Service.Data.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(Service.ClientState.TerritoryType)?.PlaceName.Value.Name.ExtractText() ?? "Unknown Location"),
    ];
}
