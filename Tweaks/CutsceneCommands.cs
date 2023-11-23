using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dalamud;
using Dalamud.Utility;
using Dalamud.Game.Text;
using Dalamud.Game.Config;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using SimpleTweaksPlugin.Utility;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Cutscene commands")]
[TweakDescription("Enable the use of certain commands in cutscenes")]
[TweakAuthor("Kurochi")]
[TweakAutoConfig]
[TweakReleaseVersion(UnreleasedVersion)]
public class CutsceneCommands : Tweak
{
    public class Configs : TweakConfig
    {
        [TweakConfigOption("Toggle Master Volume", 1)]
        public bool MasterVolume;
        [TweakConfigOption("Toggle Background Music", 2)]
        public bool BackgroundMusic;
        [TweakConfigOption("Toggle Voice", 3)]
        public bool Voice;
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

    private readonly List<string> masterVolumeCommands = new()
    {
        "/mastervolume",
        "/lautstärke",
        "/vgénéral",
    };

    private readonly List<string> bgmVolumeCommands = new()
    {
        "/bgm",
        "/musik",
        "/vmusique",
    };

    private readonly List<string> voiceVolumeCommands = new()
    {
        "/voice",
        "/stimmen",
        "/vvoix",
    };

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
        Service.Framework.Update -= PopulateLoc;
    }

    private unsafe void OnChatMessage(XivChatType type, uint senderid, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (type is not XivChatType.ErrorMessage || Service.ClientState is not { ClientLanguage: { } clientLanguage })
        {
            return;
        }
        if (Common.LastCommand is null || !ListArrayHasElement(Common.LastCommand->ToString(), masterVolumeCommands, bgmVolumeCommands, voiceVolumeCommands))
        {
            return;
        }
        var configCommandArray = new[]
        {
            (Config.MasterVolume, masterVolumeCommands, SystemConfigOption.IsSndMaster),
            (Config.BackgroundMusic, bgmVolumeCommands, SystemConfigOption.IsSndBgm),
            (Config.Voice, voiceVolumeCommands, SystemConfigOption.IsSndVoice),
        };
        foreach (var (config, commands, option) in configCommandArray)
        {
            HandleCommands((config, commands, option), clientLanguage, message, ref isHandled, loc1, loc2);
        }
    }

    private static void HandleCommands((bool config, IReadOnlyList<string> commands, SystemConfigOption option) configCommandOption,
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

    private static string ConstructChatMessage(bool optionFlag, SystemConfigOption option, ClientLanguage clientLanguage)
    {
        var message = string.Empty;
        switch (clientLanguage)
        {
            case ClientLanguage.French:
            {
                message = optionFlag ? "Vous avez activé " : "Vous avez désactivé ";
                message += option switch
                {
                    SystemConfigOption.IsSndMaster => "le volume général.",
                    SystemConfigOption.IsSndBgm => "la musique.",
                    SystemConfigOption.IsSndVoice => "les voix.",
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
                    SystemConfigOption.IsSndVoice => "Stimmen",
                    _ => message,
                };
                if (message.IsNullOrWhitespace())
                {
                    return string.Empty;
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
                    SystemConfigOption.IsSndVoice => "ボイス",
                    _ => message,
                };
                if (message.IsNullOrWhitespace())
                {
                    return string.Empty;
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
                    SystemConfigOption.IsSndVoice => "Voice",
                    _ => message,
                };
                if (message.IsNullOrWhitespace())
                {
                    return string.Empty;
                }
                message += optionFlag ? " volume unmuted." : " volume muted.";
                break;
            }
        }
        return message;
    }

    private static bool ListArrayHasElement(string element, params List<string>[] lists)
    {
        if (!element.IsNullOrWhitespace())
        {
            return Array.Exists(lists, list => list.Contains(element));
        }
        return false;
    }

    protected override void Disable()
    {
        Service.Chat.CheckMessageHandled -= OnChatMessage;
        Service.Framework.Update -= PopulateLoc;
        SaveConfig(Config);
    }
}

