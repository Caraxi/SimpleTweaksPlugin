using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Frozen;
using System.Collections.Generic;

using Dalamud;
using Dalamud.Utility;
using Dalamud.Game.Text;
using Dalamud.Game.Config;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets2;
using Dalamud.Game.Text.SeStringHandling;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Cutscene commands")]
[TweakDescription("Enable the use of certain commands in cutscenes")]
[TweakAuthor("Kurochi")]
[TweakAutoConfig]
[TweakReleaseVersion("1.9.4.0")]
[TweakCategory(TweakCategory.Command)]
public class CutsceneCommands : Tweak
{
    public class Configs : TweakConfig
    {
        [TweakConfigOption("Toggle Master Volume", 1)]
        public bool MasterVolume;

        [TweakConfigOption("Toggle Background Music", 2)]
        public bool BackgroundMusic;

        [TweakConfigOption("Toggle Sound Effects", 3)]
        public bool SoundEffects;

        [TweakConfigOption("Toggle Voice", 4)]
        public bool Voice;

        [TweakConfigOption("Toggle System Sounds", 5)]
        public bool System;

        [TweakConfigOption("Toggle Ambient Sounds", 6)]
        public bool Ambient;

        [TweakConfigOption("Toggle Speaker Volume", 7)]
        public bool ControllerSpeaker;

        [TweakConfigOption("Toggle Performance", 8)]
        public bool Performance;

        [TweakConfigOption("Toggle Mount Music", 9)]
        public bool MountBackgroundMusic;
    }

    public Configs Config { get; private set; } = null!;

    private const string EnMessageP1 = "The command “";
    private const string EnMessageP2 = "” is unavailable at this time.";

    private const string DeMessageP1 = "„";
    private const string DeMessageP2 = "“ wurde als Textkommando nicht richtig verwendet.";

    private const string FrMessageP1 = "La commande texte “";
    private const string FrMessageP2 = "” ne peut pas être utilisée de cette façon.";

    private const string JpMessage = "そのコマンドは現在使用できません。： ";

    private string loc1, loc2;
    private (bool, FrozenSet<string>, SystemConfigOption)[] commandArray;
    private FrozenSet<string> allCommands;

    protected override void Enable()
    {
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.Chat.CheckMessageHandled += OnChatMessage;
        Service.Framework.Update += PopulateLoc;
    }

    private void PopulateLoc(IFramework _framework)
    {
        if (Service.ClientState is not { ClientLanguage: { } clientLanguage })
        {
            return;
        }
        _ = Task.Run(() =>
        {
            var masterVolumeCommands = GetCommands(clientLanguage, 329);
            var bgmVolumeCommands = GetCommands(clientLanguage, 330);
            var soundFxVolumeCommands = GetCommands(clientLanguage, 331);
            var voiceVolumeCommands = GetCommands(clientLanguage, 332);
            var systemVolumeCommands = GetCommands(clientLanguage, 333);
            var ambientVolumeCommands = GetCommands(clientLanguage, 334);
            var controllerSpeakerVolumeCommands = GetCommands(clientLanguage, 338);
            var performanceVolumeCommands = GetCommands(clientLanguage, 341);
            var mountBgmVolumeCommands = GetCommands(clientLanguage, 340);
            allCommands = CreateFrozenSet<string>(masterVolumeCommands,
                bgmVolumeCommands,
                soundFxVolumeCommands,
                voiceVolumeCommands,
                systemVolumeCommands,
                ambientVolumeCommands,
                controllerSpeakerVolumeCommands,
                performanceVolumeCommands,
                mountBgmVolumeCommands);
            commandArray =
            [
                (Config.MasterVolume, masterVolumeCommands, SystemConfigOption.IsSndMaster),
                (Config.BackgroundMusic, bgmVolumeCommands, SystemConfigOption.IsSndBgm),
                (Config.SoundEffects, soundFxVolumeCommands, SystemConfigOption.IsSndSe),
                (Config.Voice, voiceVolumeCommands, SystemConfigOption.IsSndVoice),
                (Config.System, systemVolumeCommands, SystemConfigOption.IsSndSystem),
                (Config.Ambient, ambientVolumeCommands, SystemConfigOption.IsSndEnv),
                (Config.ControllerSpeaker, controllerSpeakerVolumeCommands, SystemConfigOption.IsSoundPad),
                (Config.Performance, performanceVolumeCommands, SystemConfigOption.IsSndPerform),
                (Config.MountBackgroundMusic, mountBgmVolumeCommands, SystemConfigOption.SoundChocobo), // MountBGM
            ];
        });
        Service.Framework.Update -= PopulateLoc;
        loc1 = clientLanguage switch
        {
            ClientLanguage.French => FrMessageP1,
            ClientLanguage.German => DeMessageP1,
            ClientLanguage.Japanese => JpMessage,
            ClientLanguage.English => EnMessageP1,
            _ => throw new ArgumentException($"Client Language: {clientLanguage} is unsupported"),
        };
        loc2 = clientLanguage switch
        {
            ClientLanguage.French => FrMessageP2,
            ClientLanguage.German => DeMessageP2,
            ClientLanguage.Japanese => string.Empty,
            ClientLanguage.English => EnMessageP2,
            _ => throw new ArgumentException($"Client Language: {clientLanguage} is unsupported"),
        };
    }

    private unsafe void OnChatMessage(
        XivChatType type,
        uint senderid,
        ref SeString sender,
        ref SeString message,
        ref bool isHandled)
    {
        if (type is not XivChatType.ErrorMessage || Service.ClientState is not { ClientLanguage: var clientLanguage })
        {
            return;
        }
        if (Common.LastCommand is null || !allCommands.Any(command => Common.LastCommand->ToString().Contains(command)))
        {
            return;
        }
        foreach (var (config, commands, option) in commandArray)
        {
            HandleCommands((config, commands, option), clientLanguage, message, ref isHandled, loc1, loc2);
        }
    }

    private static void HandleCommands(
        (bool config, IReadOnlySet<string> commands, SystemConfigOption option) configCommandOption,
        ClientLanguage clientLanguage,
        SeString message,
        ref bool isHandled,
        string LocMessage,
        string? LocMessage2 = null)
    {
        foreach (var command in configCommandOption.commands)
        {
            var error = LocMessage + command;
            if (!LocMessage2.IsNullOrWhitespace())
            {
                error += LocMessage2;
            }
            if (!configCommandOption.config || !error.Equals(message.TextValue))
            {
                continue;
            }
            Service.GameConfig.TryGet(configCommandOption.option, out bool optionFlag);
            Service.GameConfig.Set(configCommandOption.option, !optionFlag);
            isHandled = true;
            var chatMessage = ConstructChatMessage(optionFlag, configCommandOption.option, clientLanguage);
            if (chatMessage.IsNullOrWhitespace())
            {
                chatMessage = "There was an issue creating the chat message for: " + command + " with client language: " + clientLanguage;
            }
            Service.Chat.Print(new XivChatEntry
            {
                Message = SeString.Parse(Encoding.UTF8.GetBytes(chatMessage)),
                Type = XivChatType.Echo,
            });
            return;
        }
    }

    private static string ConstructChatMessage(
        bool optionFlag,
        SystemConfigOption option,
        ClientLanguage clientLanguage)
    {
        var message = string.Empty;
        switch (clientLanguage)
        {
            case ClientLanguage.French:
            {
                if (option is SystemConfigOption.SoundChocobo)
                {
                    message = "La musique à dos de monture a été";
                    message += optionFlag ? " désactivée." : " activée.";
                    break;
                }
                message = optionFlag ? "Vous avez activé " : "Vous avez désactivé ";
                message += option switch
                {
                    SystemConfigOption.IsSndMaster => "le volume général.",
                    SystemConfigOption.IsSndBgm => "la musique.",
                    SystemConfigOption.IsSndSe => "les effets sonores.",
                    SystemConfigOption.IsSndVoice => "les voix.",
                    SystemConfigOption.IsSndSystem => "les sons système.",
                    SystemConfigOption.IsSndEnv => "l'ambiance sonore.",
                    SystemConfigOption.IsSoundPad => "le haut-parleur pour les sons système.",
                    SystemConfigOption.IsSndPerform => "les Actions d'interprétation.",
                    _ => string.Empty,
                };
                if (message.Last() is not '.')
                {
                    return string.Empty;
                }
                break;
            }
            case ClientLanguage.German:
            {
                message = option switch
                {
                    SystemConfigOption.IsSndMaster => "Hauptlautstärke",
                    SystemConfigOption.IsSndBgm => "Hintergrundmusik",
                    SystemConfigOption.IsSndSe => "Soundeffekte",
                    SystemConfigOption.IsSndVoice => "Stimmen",
                    SystemConfigOption.IsSndSystem => "Systemtöne",
                    SystemConfigOption.IsSndEnv => "Umgebungsgeräusche",
                    SystemConfigOption.IsSoundPad => "Systemtöne über Lautsprecher",
                    SystemConfigOption.IsSndPerform => "Kompositionen",
                    SystemConfigOption.SoundChocobo => "Musik beim Reiten",
                    _ => message,
                };
                if (message.IsNullOrWhitespace())
                {
                    return string.Empty;
                }
                if (option is SystemConfigOption.SoundChocobo)
                {
                    message += optionFlag ? " stummgeschaltet" : " wieder eingeschaltet";
                    break;
                }
                message += optionFlag ? " wieder eingeschaltet" : " stummgeschaltet";
                break;
            }
            case ClientLanguage.Japanese:
            {
                message = option switch
                {
                    SystemConfigOption.IsSndMaster => "マスターボリューム",
                    SystemConfigOption.IsSndBgm => "BGM",
                    SystemConfigOption.IsSndSe => "効果音",
                    SystemConfigOption.IsSndVoice => "ボイス",
                    SystemConfigOption.IsSndSystem => "システム音",
                    SystemConfigOption.IsSndEnv => "環境音",
                    SystemConfigOption.IsSoundPad => "システム音のスピーカー出力",
                    SystemConfigOption.IsSndPerform => "楽器演奏",
                    SystemConfigOption.SoundChocobo => "マウント騎乗中のBGM再生を",
                    _ => message,
                };
                if (message.IsNullOrWhitespace())
                {
                    return string.Empty;
                }
                if (option is SystemConfigOption.SoundChocobo)
                {
                    message += optionFlag ? "無効にしました。" : "有効にしました。";
                    break;
                }
                message += optionFlag ? "のミュートを解除しました。" : "をミュートしました。";
                break;
            }
            case ClientLanguage.English:
            {
                message = option switch
                {
                    SystemConfigOption.IsSndMaster => "Master",
                    SystemConfigOption.IsSndBgm => "BGM",
                    SystemConfigOption.IsSndSe => "Sound effects",
                    SystemConfigOption.IsSndVoice => "Voice",
                    SystemConfigOption.IsSndSystem => "System sounds",
                    SystemConfigOption.IsSndEnv => "Ambient sounds",
                    SystemConfigOption.IsSoundPad => "System sounds speaker",
                    SystemConfigOption.IsSndPerform => "Performance",
                    SystemConfigOption.SoundChocobo => "Mount BGM",
                    _ => message,
                };
                if (message.IsNullOrWhitespace())
                {
                    return string.Empty;
                }
                if (option is SystemConfigOption.SoundChocobo)
                {
                    message += optionFlag ? " volume muted." : " volume unmuted";
                    break;
                }
                message += optionFlag ? " volume unmuted." : " volume muted.";
                break;
            }
        }
        return message;
    }

    protected override void Disable()
    {
        Service.Chat.CheckMessageHandled -= OnChatMessage;
        Service.Framework.Update -= PopulateLoc;
        SaveConfig(Config);
    }

    private static FrozenSet<string>? GetCommands(ClientLanguage? language, params int[] rows)
    {
        var sheet = Service.Data.GetExcelSheet<TextCommand>(language ?? ClientLanguage.English);
        if (sheet is null)
        {
            SimpleLog.Error("Failed to retrieve lumina sheet.");
            throw new NotSupportedException("Lumina unavailable.");
        }
        var commands = new List<string>();
        foreach (var row in sheet.Where(s => Array.Exists(rows, r => r == s.RowId)))
        {
            var mainCommand = row.Command.ToDalamudString().TextValue;
            var aliasCommand = row.Alias.ToDalamudString().TextValue;

            if (!mainCommand.IsNullOrWhitespace())
            {
                commands.Add(mainCommand);
            }
            if (!aliasCommand.IsNullOrWhitespace())
            {
                commands.Add(aliasCommand);
            }
        }
        return commands.ToFrozenSet();
    }

    public static FrozenSet<T> CreateFrozenSet<T>(params IEnumerable<T>[] collections)
        => collections.SelectMany(i => i).ToFrozenSet();
}
