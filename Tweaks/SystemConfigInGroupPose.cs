using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class SystemConfigInGroupPose : Tweak {
    public override string Name => "SystemConfig in Group Pose";
    public override string Description => "Allows the use of the /systemconfig command while in gpose.";
    
    private readonly string[] commands = {
        // EN
        "The command “/systemconfig” is unavailable at this time.",
        "The command “/sconfig” is unavailable at this time.",
        
        // DE
        "„/systemconfig“ wurde als Textkommando nicht richtig verwendet.",
        "„/sconfig“ wurde als Textkommando nicht richtig verwendet.",
        "„/systemkonfig“ wurde als Textkommando nicht richtig verwendet.",
        "„/skon“ wurde als Textkommando nicht richtig verwendet.",
        
        // FR
        "La commande texte “/systemconfig” ne peut pas être utilisée de cette façon.",
        "La commande texte “/sconfig” ne peut pas être utilisée de cette façon.",
        "La commande texte “/confs” ne peut pas être utilisée de cette façon.",
        "La commande texte “/configsys” ne peut pas être utilisée de cette façon.",
        
        // JA
        "そのコマンドは現在使用できません。： /systemconfig",
        "そのコマンドは現在使用できません。： /sconfig",
    };
    
    
    public override void Setup() {
        AddChangelogNewTweak("1.8.3.0");
        base.Setup();
    }

    protected override void Enable() {
        Service.Chat.CheckMessageHandled += OnChatMessage;
        base.Enable();
    }

    private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (type != XivChatType.ErrorMessage) return;
        if (!Service.ClientState.IsGPosing) return;
        if (commands.Contains(message.TextValue)) {
            var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Config);
            agent->Show();
            isHandled = true;
        }
    }

    protected override void Disable() {
        Service.Chat.CheckMessageHandled -= OnChatMessage;
        base.Disable();
    }
}
