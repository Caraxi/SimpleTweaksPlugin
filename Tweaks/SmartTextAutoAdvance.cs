#nullable enable
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SimpleTweaksPlugin.Tweaks;

// Converted to a tweak from https://github.com/anti-matt-er/ZQOLSmartAutoAdvance
public unsafe class SmartTextAutoAdvance : Tweak
{
    public override string Name => "Smart Text Auto-Advance";

    public override string Description => "Automatically toggles \"text auto-advance\" on for voiced cutscenes and off for unvoiced";

    protected override string Author => "Anti-Matt-Er";

    private bool InNewCutscene { get; set; }

    private ClientFunctions TweakClientFunctions { get; } = new();

    private Config TweakConfig { get; set; } = null!;

    public static class VoicelineValidator
    {
        private static readonly Regex MatchIfValid = new(
            @"^cut/\w+/sound/[\w/]+/vo_\w+\.scd$",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(1000)
        );

        public static bool IsValid(string path) => MatchIfValid.IsMatch(path);
    }

    public static class Util
    {
        private static unsafe byte[] ReadTerminatedBytes(byte* ptr)
        {
            if (ptr == null)
            {
                return Array.Empty<byte>();
            }

            var bytes = new List<byte>();
            while (*ptr != 0)
            {
                bytes.Add(*ptr);
                ptr += 1;
            }

            return bytes.ToArray();
        }

        public static unsafe string ReadTerminatedString(byte* ptr)
        {
            return Encoding.UTF8.GetString(ReadTerminatedBytes(ptr));
        }
    }

    public class PlaySpecificSoundEventArgs : EventArgs
    {
        public PlaySpecificSoundEventArgs(string path, int index)
        {
            Path = path;
            Index = index;
        }

        public string Path { get; private set; }

        public int Index { get; private set; }
    }

    private unsafe class ClientFunctions : IDisposable
    {
        private readonly IntPtr pCutsceneAgent;

        private const int ResourceDataPointerOffset = 0xB0;

        private delegate byte GetAutoAdvanceDelegate(IntPtr pAgent);

        private delegate nint ToggleAutoAdvanceDelegate(IntPtr pAgent, uint togglePause, bool toggleAutoAdvance);

        private delegate nint EnableCutsceneInputModeDelegate(IntPtr pUIModule, nint a2);

        private delegate nint DisableCutsceneInputModeDelegate(IntPtr pUIModule);

        private delegate void* PlaySpecificSoundDelegate(long a1, int idx);

        private delegate void* GetResourceSyncPrototype(IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown);

        private delegate void* GetResourceAsyncPrototype(IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown);

        private delegate IntPtr LoadSoundFileDelegate(IntPtr resourceHandle, uint a2);

        [Signature("E8 ?? ?? ?? ?? 84 C0 75 0D B0 01 48 8B 5C 24 ?? 48 83 C4 20 5F C3 83 7B 70 00")]
        private readonly GetAutoAdvanceDelegate? getAutoAdvanceDelegate = null;

        [Signature("48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 48 8B 49 10 41 0F B6 F0")]
        private readonly ToggleAutoAdvanceDelegate? toggleAutoAdvanceDelegate = null;

        [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8D 99 ?? ?? ?? ?? 48 8B F9 80 7B 25 00", DetourName = nameof(EnableCutsceneInputModeDetour))]
        private readonly Hook<EnableCutsceneInputModeDelegate>? enableCutsceneInputModeHook = null;

        [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B F9 48 81 C1 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 07", DetourName = nameof(DisableCutsceneInputModeDetour))]
        private readonly Hook<DisableCutsceneInputModeDelegate>? disableCutsceneInputModeHook = null;

        [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 33 F6 8B DA 48 8B F9 0F BA E2 0F", DetourName = nameof(PlaySpecificSoundDetour))]
        private readonly Hook<PlaySpecificSoundDelegate>? playSpecificSoundHook = null;

        [Signature("E8 ?? ?? ?? ?? 48 8D 8F ?? ?? ?? ?? 48 89 87 ?? ?? ?? ?? 48 8D 54 24", DetourName = nameof(GetResourceSyncDetour))]
        private readonly Hook<GetResourceSyncPrototype>? getResourceSyncHook = null;

        [Signature("E8 ?? ?? ?? ?? 48 8B D8 EB 07 F0 FF 83", DetourName = nameof(GetResourceAsyncDetour))]
        private readonly Hook<GetResourceAsyncPrototype>? getResourceAsyncHook = null;

        [Signature("E8 ?? ?? ?? ?? 48 85 C0 75 04 B0 F6", DetourName = nameof(LoadSoundFileDetour))]
        private readonly Hook<LoadSoundFileDelegate>? loadSoundFileHook = null;

        private ConcurrentDictionary<IntPtr, string> Scds { get; } = new();

        public event Action<PlaySpecificSoundEventArgs> OnPlaySpecificSound = null!;

        public event Action OnCutsceneChanged = null!;

        private bool autoAdvanceEnabled = false;
        public bool AutoAdvanceEnabled
        {
            get
            {
                // We have to check the current client setting of auto-advance because:
                // a) we cannot infer an initial value
                // b) the player may change it in-game at any time
                this.autoAdvanceEnabled = this.GetAutoAdvance();

                return this.autoAdvanceEnabled;
            }
            set
            {
                // Just as in get, we have to check the current client setting
                this.autoAdvanceEnabled = this.GetAutoAdvance();

                if (this.autoAdvanceEnabled != value)
                {
                    this.autoAdvanceEnabled = value;

                    this.ToggleAutoAdvance();
#if DEBUG
                    PluginLog.Verbose($"Auto-advance set to [{value}]", value);
#endif
                }
            }
        }

        public ClientFunctions()
        {
            SignatureHelper.Initialise(this);

            this.pCutsceneAgent = new IntPtr(Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.Cutscene));

            this.Enable();
        }

        public void Enable()
        {
            this.enableCutsceneInputModeHook?.Enable();
            this.disableCutsceneInputModeHook?.Enable();
            this.playSpecificSoundHook?.Enable();
            this.loadSoundFileHook?.Enable();
            this.getResourceSyncHook?.Enable();
            this.getResourceAsyncHook?.Enable();
        }

        public void Disable()
        {
            this.enableCutsceneInputModeHook?.Disable();
            this.disableCutsceneInputModeHook?.Disable();
            this.playSpecificSoundHook?.Disable();
            this.loadSoundFileHook?.Disable();
            this.getResourceSyncHook?.Disable();
            this.getResourceAsyncHook?.Disable();
        }

        public void Dispose()
        {
            this.enableCutsceneInputModeHook?.Dispose();
            this.disableCutsceneInputModeHook?.Dispose();
            this.playSpecificSoundHook?.Dispose();
            this.loadSoundFileHook?.Dispose();
            this.getResourceSyncHook?.Dispose();
            this.getResourceAsyncHook?.Dispose();
        }

        private bool GetAutoAdvance()
        {
            if (this.getAutoAdvanceDelegate == null)
            {
                throw new InvalidOperationException("GetAutoAdvance signature wasn't found!");
            }
#if DEBUG
            var currentState = this.getAutoAdvanceDelegate(this.pCutsceneAgent);
            PluginLog.Verbose($"{currentState}");

            return currentState != 0;
#endif
            return this.getAutoAdvanceDelegate(this.pCutsceneAgent) != 0;
        }

        private void ToggleAutoAdvance()
        {
            if (this.toggleAutoAdvanceDelegate == null)
            {
                throw new InvalidOperationException("ToggleAutoAdvance signature wasn't found!");
            }

            // True always toggles
            this.toggleAutoAdvanceDelegate(this.pCutsceneAgent, 0, true);
        }

        private nint EnableCutsceneInputModeDetour(IntPtr pUIModule, nint a2)
        {
#if DEBUG
            PluginLog.Verbose($"Client: EnableCutsceneInputMode(a1: {pUIModule}, a2: {a2})", pUIModule, a2);
#endif
            this.OnCutsceneChanged?.Invoke();

            return this.enableCutsceneInputModeHook!.Original(pUIModule, a2);
        }

        private nint DisableCutsceneInputModeDetour(IntPtr pUIModule)
        {
#if DEBUG
            PluginLog.Verbose($"Client: DisableCutsceneInputMode(a1: {pUIModule})", pUIModule);
#endif
            this.OnCutsceneChanged?.Invoke();

            return this.disableCutsceneInputModeHook!.Original(pUIModule);
        }

        private void* PlaySpecificSoundDetour(long a1, int idx)
        {
            try
            {
                if (a1 == 0)
                {
                    return null;
                }

                var scdData = *(byte**)(a1 + 8);
                if (scdData == null)
                {
                    return null;
                }

                // check cached scds for path
                if (!this.Scds.TryGetValue((IntPtr)scdData, out var path))
                {
                    return null;
                }

                path = path.ToLowerInvariant();
#if DEBUG
                PluginLog.Verbose($".scd played: {path}", path);
#endif

                this.OnPlaySpecificSound?.Invoke(new PlaySpecificSoundEventArgs(path, idx));
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Error in PlaySpecificSoundDetour!");
            }

            return this.playSpecificSoundHook!.Original(a1, idx);
        }

        private void* GetResourceSyncDetour(IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown)
        {
            return this.ResourceDetour(true, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, false);
        }

        private void* GetResourceAsyncDetour(IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown)
        {
            return this.ResourceDetour(false, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown);
        }

        private void* ResourceDetour(bool isSync, IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown)
        {
            var ret = this.CallOriginalResourceHandler(isSync, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown);

            var path = Util.ReadTerminatedString((byte*)pPath);
            if (ret != null && path.EndsWith(".scd"))
            {
                var scdData = Marshal.ReadIntPtr((IntPtr)ret + ResourceDataPointerOffset);
                // if we immediately have the scd data, cache it, otherwise add it to a waiting list to hopefully be picked up at sound play time
                if (scdData != IntPtr.Zero)
                {
                    this.Scds[scdData] = path;
                }
            }

            return ret;
        }

        private void* CallOriginalResourceHandler(bool isSync, IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown)
        {
            return isSync
                ? this.getResourceSyncHook!.Original(pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown)
                : this.getResourceAsyncHook!.Original(pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown);
        }

        private IntPtr LoadSoundFileDetour(IntPtr resourceHandle, uint a2)
        {
            var ret = this.loadSoundFileHook!.Original(resourceHandle, a2);

            try
            {
                var handle = (ResourceHandle*)resourceHandle;
                var name = handle->FileName.ToString();
                if (name.EndsWith(".scd"))
                {
                    var dataPtr = Marshal.ReadIntPtr(resourceHandle + ResourceDataPointerOffset);
                    this.Scds[dataPtr] = name;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Error in LoadSoundFileDetour!");
            }

            return ret;
        }
    }

    private class Config : TweakConfig
    {
        public bool ForceEnableInParty { get; set; } = true;
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) =>
    {
        var enabledInParty = this.TweakConfig.ForceEnableInParty;
        if (ImGui.Checkbox("Enable Auto-Advance for unvoiced cutscenes when in a party", ref enabledInParty))
        {
            this.TweakConfig.ForceEnableInParty = enabledInParty;
            this.SaveConfig(this.TweakConfig);
        }
    };

    public override void Setup()
    {
        if (this.Ready) return;

        AddChangelogNewTweak(Changelog.UnreleasedVersion);

        this.InNewCutscene = false;

        base.Setup();
    }

    public override void Enable()
    {
        this.TweakConfig = this.LoadConfig<Config>() ?? new Config();

        Service.Condition.ConditionChange += this.OnConditionChanged;
        this.TweakClientFunctions.OnCutsceneChanged += this.OnCutsceneChanged;
        this.TweakClientFunctions.OnPlaySpecificSound += this.OnPlaySpecificSound;

        this.TweakClientFunctions.Enable();

        base.Enable();
    }

    public override void Disable()
    {
        this.SaveConfig(this.TweakConfig);

        Service.Condition.ConditionChange -= this.OnConditionChanged;
        this.TweakClientFunctions.OnCutsceneChanged -= this.OnCutsceneChanged;
        this.TweakClientFunctions.OnPlaySpecificSound -= this.OnPlaySpecificSound;

        this.TweakClientFunctions.Disable();

        base.Disable();
    }

    public override void Dispose()
    {
        Service.Condition.ConditionChange -= this.OnConditionChanged;
        this.TweakClientFunctions.OnCutsceneChanged -= this.OnCutsceneChanged;
        this.TweakClientFunctions.OnPlaySpecificSound -= this.OnPlaySpecificSound;

        this.TweakClientFunctions.Dispose();

        base.Dispose();
    }

    public void ToggleAutoAdvance()
    {
        this.TweakClientFunctions.AutoAdvanceEnabled = !this.TweakClientFunctions.AutoAdvanceEnabled;
    }

    public void LogAutoAdvance()
    {
        PluginLog.Information($"Client Auto-Advance is currently: {this.TweakClientFunctions.AutoAdvanceEnabled}");
    }

    private static bool IsInPartyWithOthers => GroupManager.Instance() is not null && GroupManager.Instance()->MemberCount is > 1;

    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
#if DEBUG
            PluginLog.Verbose($"Flag [{flag}] changed to [{value}]", flag, value);
#endif
        if (value != this.InNewCutscene && (
            flag == ConditionFlag.WatchingCutscene ||
            flag == ConditionFlag.WatchingCutscene78 ||
            flag == ConditionFlag.OccupiedInCutSceneEvent
        ))
        {
            if (value)
            {
                this.OnCutsceneChanged();
            }
            else
            {
                this.InNewCutscene = false;
            }
        }
    }

    private void OnCutsceneChanged()
    {
        if (this.InNewCutscene)
        {
            return;
        }

        if (this.TweakConfig.ForceEnableInParty && IsInPartyWithOthers)
        {
            PluginLog.Information("Cutscene started/ended in a party, enabling auto-advance!");

            this.TweakClientFunctions.AutoAdvanceEnabled = true;
        }
        else
        {
            PluginLog.Information("Cutscene started/ended, disabling auto-advance!");

            this.TweakClientFunctions.AutoAdvanceEnabled = false;
        }

        this.InNewCutscene = true;
    }

    private void OnPlaySpecificSound(PlaySpecificSoundEventArgs e)
    {
        if (!this.InNewCutscene)
        {
            return;
        }

        // return early if .scd audio isn't cutscene, to avoid the RegEx check
        if (!e.Path.StartsWith("cut/"))
        {
            return;
        }

        // We're only concerned with voice line sounds
        if (!VoicelineValidator.IsValid(e.Path))
        {
            return;
        }

        PluginLog.Information("Voice line detected, enabling auto-advance!");

        // Cutscene is still playing, but no longer "new". We don't need to listen for new sounds
        this.InNewCutscene = false;
        this.TweakClientFunctions.AutoAdvanceEnabled = true;
    }
}