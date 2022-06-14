using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

/**
 * shamelessly cribbed from CharacterClassSwitcher
 * 
 * TODO
 * - testing
 *
 */

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class TweakingHunter : Tweak
{
    public override string Name => "Hunting Log Target Coords";
    public override string Description => "Allow clicking on hunting log targets to generate map marker (requires chatcoords plugin)";

    private delegate byte EventHandle(AtkUnitBase* atkUnitBase, AtkEventType eventType, uint eventParam, AtkEvent* atkEvent, void* a5);
    private delegate void* SetupHandle(AtkUnitBase* atkUnitBase, void* param_2, void* param_3);
    private HookWrapper<EventHandle> eventHook;
    private HookWrapper<SetupHandle> setupHook;

    public override void Enable()
    {
        var eventSigMaybe = "4C 8B ?? 55 56 41 ?? 48 8B ?? 48 81 ?? ?? ?? ?? ?? 0F";

        var listHandlerSetupSigMaybe = "?? 89 ?? ?? ?? ?? 89 ?? ?? ?? ?? 89 ?? ?? ?? 57 48 83 ?? ?? 8B C2 BF";

        eventHook ??= Common.Hook<EventHandle>(eventSigMaybe, EventDetour);
        eventHook.Enable();
        setupHook ??= Common.Hook<SetupHandle>(listHandlerSetupSigMaybe, SetupDetour);
        setupHook.Enable();
        try
        {
            SetupMonsterNote(Common.GetUnitBase("MonsterNote"));
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex);
        }
        base.Enable();
    }

    private void SetupMonsterNote(AtkUnitBase* atkUnitBase)
    {
        try
        {
            if (atkUnitBase != null)
            {
                SimpleLog.Debug("Setup MonsterNote Events with " + atkUnitBase->X);
                var mainTreeListNode = (AtkComponentNode*)atkUnitBase->GetNodeById(46);
                SimpleLog.Debug(mainTreeListNode->ToString());

                const uint theImportantNodeId = 5;
                SimpleLog.Debug($"about to try looking up {theImportantNodeId}");

                for (var listItemRenderer = (AtkComponentNode*)mainTreeListNode->Component->UldManager.SearchNodeById(theImportantNodeId);
                    listItemRenderer->AtkResNode.NodeID != 6;// so many magic numbers; the stuff we want is stored in 5, 51001, 51002, etc and then the next type of thing starts at 6
                    listItemRenderer = listItemRenderer->AtkResNode.NextSiblingNode->GetAsAtkComponentNode())
                {
                    if (listItemRenderer is null) throw new Exception("aw no, this should literally never happen");
                    AtkResNode atkResNode = listItemRenderer->AtkResNode;
                    SimpleLog.Debug($"{atkResNode.NodeID} at {atkResNode.X}, {atkResNode.Y}: {atkResNode.Height} x {atkResNode.Width}");

                    var colNode = (AtkCollisionNode*)listItemRenderer->Component->UldManager.SearchNodeById(8);
                    if (colNode == null)
                    {
                        SimpleLog.Error("we've ended up somewhere without a collision node, probably followed a wrong sibling link. giving up");
                        return;
                    }
                    if (colNode->AtkResNode.Type != NodeType.Collision)
                    {
                        SimpleLog.Error("we've ended up in a place that isn't a collision node, somehow? this seems bad. giving up");
                        return;
                    }
                    SimpleLog.Debug($"{colNode->AtkResNode.X}, {colNode->AtkResNode.Y}: {colNode->AtkResNode.Width} x {colNode->AtkResNode.Height}");
                    colNode->AtkResNode.AddEvent(AtkEventType.MouseClick, 0x10F2C000, (AtkEventListener*)atkUnitBase, (AtkResNode*)colNode, false);
                    SimpleLog.Debug("event registered successfully (hopefully)");
                }


                SimpleLog.Debug("MonsterNote Events Setup");
            }
        }
        catch (Exception ex)
        {
            SimpleLog.Error("aww beans " + ex);
            //throw;
        }
    }

    private void* SetupDetour(AtkUnitBase* atkUnitBase, void* param_2, void* param_3)
    {
        SimpleLog.Debug("in setup detour");
        var retVal = setupHook.Original(atkUnitBase, param_2, param_3);

        SetupMonsterNote(atkUnitBase);

        return retVal;
    }

    private byte EventDetour(AtkUnitBase* atkUnitBase, AtkEventType eventType, uint eventParam, AtkEvent* atkEvent, void* a5)
    {
        int depth = 0;
        ProcessEventAndRecurse(atkUnitBase, eventType, eventParam, atkEvent, a5, depth);

        return eventHook.Original(atkUnitBase, eventType, eventParam, atkEvent, a5);
    }

    private void ProcessEventAndRecurse(AtkUnitBase* atkUnitBase, AtkEventType eventType, uint eventParam, AtkEvent* atkEvent, void* a5, int depth)
    {
        string indent = new('\t', depth);
        try
        {
            if (eventType == AtkEventType.MouseClick)
            {
                SimpleLog.Debug(indent + $"click with param {(eventParam & 0x10F2C000) == 0x10F2C000}");

                if ((eventParam & 0x10F2C000) == 0x10F2C000)
                {
                    AtkCollisionNode* colNode = (AtkCollisionNode*)atkEvent->Target;
                    SimpleLog.Debug(indent + "oh shit!");
                    // do the actual stuff!
                    AtkResNode* parentNode = colNode->AtkResNode.ParentNode;
                    AtkTextNode* nameTextNode = parentNode->GetComponent()->UldManager.SearchNodeById(4)->GetAsAtkTextNode();

                    string mobName = colNode->AtkResNode.ParentNode->GetComponent()->UldManager.SearchNodeById(4)->GetAsAtkTextNode()->NodeText.ToString();
                    var theRealMob = MonstrousThing(mobName);

                    string message = $"/ctp {theRealMob.coords[0]} {theRealMob.coords[1]} : {theRealMob.zone}";
                    SimpleLog.Debug(message);
                    Plugin.XivCommon.Functions.Chat.SendMessage(message);

                    return;
                }
                AtkEvent* nextEvent = atkEvent->NextEvent;
                if (nextEvent is not null)
                {
                    ProcessEventAndRecurse(atkUnitBase, eventType, nextEvent->Param, nextEvent, a5, depth + 1);
                }
                SimpleLog.Debug(indent + "done at this level");
            }
        }
        catch (Exception ex)
        {
            SimpleLog.Error(indent + ex);
        }
    }

    public override void Disable()
    {
        eventHook?.Disable();
        setupHook?.Disable();

        base.Disable();
    }

    public override void Dispose()
    {
        setupHook?.Dispose();
        eventHook?.Dispose();
        base.Dispose();
    }
    public (string name, string zone, float[] coords) MonstrousThing(string inputName)
    {
        var name = inputName;
        Lumina.Excel.ExcelSheet<MonsterNoteTarget> monsterNoteTargets = Service.Data.Excel.GetSheet<MonsterNoteTarget>();
        Lumina.Excel.ExcelSheet<PlaceName> placeNames = Service.Data.Excel.GetSheet<PlaceName>();
        
        var theMob = monsterNoteTargets.Where(mob => mob.BNpcName.Value.Singular.ToString().Equals(inputName, StringComparison.CurrentCultureIgnoreCase)).First();

        IEnumerable<uint> zoneIDs = theMob.UnkData3.Select(u => (uint)u.PlaceNameZone);
        var theZone = placeNames.Where(p => p.RowId != 0 && zoneIDs.Contains(p.RowId)).First();
        var zone = theZone.Name;

        var coords = getNameCoordsDict()[name];

        return (name, zone, coords);
    }

    private static Dictionary<string, float[]> getNameCoordsDict()
    {
        var goods = new Dictionary<string, float[]>();

        foreach (var row in getNameCoordsCSV().Split('\n').Skip(1))
        {
            var columns = row.Split(',');
            if (columns[1].Equals("")) continue;

            var name = columns[0];
            var coords_x = float.Parse(columns[1]);
            string s = columns[2].Split('\r')[0].Trim();
            SimpleLog.Log(s);
            var coords_y = float.Parse(s);

            goods.Add(name, new[] { coords_x, coords_y });
        }

        return goods;
    }

    private static string getNameCoordsCSV()
    {
        return @"name,coords_x,coords_y
Little Ladybug,21.38,24.63
Wharf Rat,21.16,24.4
Lost Lamb,23.12,25.59
Wind Sprite,28.47,35.96
Nesting Buzzard,32.33,16.29
Bogy,20.35,18.74
Cave Bat,26.84,15.82
Galago,29.76,14.45
Grounded Pirate,20.19,17
Lightning Sprite,14.39,18.98
Ground Squirrel,30.4,25.93
Forest Funguar,24.4,18.38
Miteling,25.65,27.96
Midge Swarm,27.38,24.65
Water Sprite,18.61,19.88
Black Eft,26.34,19.91
Anole,31.06,20.45
Trickster Imp,26.47,25.31
Roselet,23.01,26.08
Chigoe,27.03,19
Microchu,27.67,21.49
Syrphid Swarm,26.84,25.05
Northern Vulture,11.62,23.5
Star Marmot,29.6,24.42
Cactuar,27.97,24.86
Snapping Shrew,23.21,27.31
Hammer Beak,23.2,25.42
Antling Worker,21.55,18.95
Earth Sprite,22.86,26.24
Spriggan Graverobber,17.4,23.68
Qiqirn Shellsweeper,17.32,18.92
Antling Soldier,25.14,18.32
Dusty Mongrel,22.58,22.05
Opo-opo,27.4,24.6
Bog Yarzon,22.97,21.22
Hoglet,29.76,23.44
Diremite,19.8,18.69
Tree Slug,13,25
Aurelia,25.39,25.3
Bee Cloud,22.22,20.83
Wild Dodo,28,20
Tiny Mandragora,22.87,18.14
Wounded Aurochs,18.6,17.23
Grounded Raider,20.25,16.89
Megalocrab,15.81,14.32
Huge Hornet,20.1,26.62
Orobon,18.46,16.28
Goblin Mugger,,
Sandtoad,23.55,22.79
Eft,23.83,18.37
Sun Midge Swarm,23.99,20.36
Desert Peiste,25.02,20.04
Syrphid Cloud,21.72,18.52
Yarzon Feeder,23,27
Rusty Coblyn,21.63,27.57
Sun Bat,25.04,17.59
Sewer Mole,33.62,28.62
Mossless Goobbue,23.11,23.92
Fat Dodo,33.97,28.71
Arbor Buzzard,31.14,29.55
Qiqirn Eggdigger,18,35
Dusk Bat,26.89,24.02
Puk Hatchling,22.29,20.37
Hedgemole,26.4,23.56
Rothlyt Pelican,24.26,23.67
Killer Mantis,22.1,22.09
Bumble Beetle,13.07,25.8
Hornet Swarm,20.51,21.82
Magicked Bones,19.49,27.62
Treant Sapling,27.46,27.41
Goblin Hunter,13,27
Mandragora,14.18,25.55
Wild Hoglet,13.23,23.95
Lemur,16.39,26.55
Faerie Funguar,19.21,28.69
Giant Gnat,18,27
Raptor Poacher,19.78,30.16
Antelope Doe,14.95,17.83
Wild Boar,18.47,24.7
Firefly,23.15,20.33
Boring Weevil,15.93,26.93
Wolf Poacher,19.64,30.14
Qiqirn Beater,21.86,30.32
Black Bat,18.11,24.61
Copper Coblyn,26,16
Bomb,27.62,17.13
Cochineal Cactuar,24.62,20.28
Giant Tortoise,27.7,24.68
Thickshell,15.95,16.37
Scaphite,16.98,14.64
Tuco-tuco,13.66,26.6
Myotragus Billy,17.41,22.37
Vandalous Imp,14.41,18.24
Bloated Bogy,13,11
Rotting Noble,14.99,16.68
Boar Poacher,19.72,30.1
Ziz Gorlin,20.63,25.85
Moraby Mole,21,34
Rhotano Buccaneer,33.68,27.56
Wild Wolf,13.17,25.75
Antling Sentry,16,15
Myotragus Nanny,17.14,22.33
Blowfly Swarm,11.95,22.51
Rotting Corpse,14.87,16.72
Quiveron Attendant,24.1,20.94
Toxic Toad,27.76,19.12
Overgrown Ivy,23,29
Lead Coblyn,13,11
Kedtrap,16.78,20.42
Coeurl Pup,9.47,21.37
Antelope Stag,22.5,19.95
Balloon,21.16,30.72
Chasm Buzzard,22.19,19.91
Axe Beak,24.78,18.41
Clay Golem,18.22,28.73
Sandstone Golem,23.24,12.3
Brood Ziz,16.16,20.27
Lindwurm,13.42,19.1
Stoneshell,13.56,24.46
Diseased Treant,17,23
Overgrown Offering,18.85,19.04
Yarzon Scavenger,14.43,7.81
Forest Yarzon,12.33,21.74
Jumping Djigga,16.06,20.91
Redbelly Sharpeye,23,19.48
Banemite,24.35,25.8
Sandskin Peiste,20.05,10.16
Ziz,16.36,27.67
Toadstool,15.84,17.94
Apkallu,29.18,35.7
Laughing Toad,15.21,6.49
Bark Eft,19.21,23.37
Glowfly,15.59,20.87
Sabotender,15.06,14.94
Qiqirn Roerunner,23.42,23.12
Goblin Thug,27.76,21.46
Coeurlclaw Cutter,29.6,21.4
Pteroc,14.79,19
Smallmouth Orobon,17,18
Redbelly Lookout,22.97,19.26
Moondrip Piledriver,17.85,6.79
Corpse Brigade Firedancer,,
Coeurlclaw Poacher,29.56,21.13
Midland Condor,17.7,26.95
Redbelly Larcener,23.07,19.28
Shroud Hare,21.02,30.46
Phurble,23.59,21.47
Floating Eye,11,22
Fallen Mage,19,17
Corpse Brigade Knuckledancer,24.24,9.64
Coeurlclaw Hunter,29.41,21.46
River Yarzon,23.48,22.43
Potter Wasp Swarm,15.6,15.41
Fire Sprite,14.54,20.33
Qiqirn Gullroaster,26,32
Grass Raptor,20.21,26.92
Gigantoad,17.97,26.91
Sundrake,25,38
Colibri,30.73,24.64
Coeurl,14.51,15.03
Mildewed Goobbue,16.87,32.46
Snow Wolf Pup,22.49,28.11
Dryad,22.9,24.35
Feral Croc,26.67,23.08
Taurus,33.79,14.21
Sandworm,15.7,35.96
Russet Yarzon,13.59,32.27
Giant Pelican,20.3,32.08
Smoke Bomb,19.19,36.07
Spriggan,11.86,15.99
Bloodshore Bell,31.59,26.08
Jungle Coeurl,18.8,27.94
Ringtail,15.38,12.02
Highland Condor,16.48,17.15
Salamander,27.58,23.3
Fallen Pikeman,20.79,38.47
Ice Sprite,23.75,28.12
Vodoriga,29.43,15.23
Baritine Croc,3.49,21.57
Mamool Ja Infiltrator,34.31,24.78
Bigmouth Orobon,18.02,30.8
Revenant,12,20
Ornery Karakul,29.55,29.72
Deepvoid Deathmouse,25.68,21.99
Downy Aevis,25.86,9.58
Will-o-the-wisp,23.68,24.98
Dragonfly,9.3,13.41
Mamool Ja Sophist,34.62,24.87
Adamantoise,15.98,30.24
Uragnite,33,24
Velociraptor,19.73,15.97
Fallen Wizard,20.92,38.67
Treant,23.63,23.62
Grenade,,
Large Buffalo,28.44,30.73
Basalt Golem,14.41,15.85
Bateleur,17.83,18.37
Mirrorknight,30.29,24.25
Stroper,20.77,28.6
Snipper,31.19,35.27
Redhorn Ogre,28.02,10.89
Highland Goobbue,24,20
Snowstorm Goobbue,22.06,17.4
Mamool Ja Breeder,34.09,25.05
Goobbue,18.09,30.95
Ochu,25,24
Mamool Ja Executioner,34.17,24.84
Dung Midge Swarm,20.56,27.21
Plasmoid,7.63,31.63
Golden Fleece,30.1,24.27
Quartz Doblyn,23.46,26.06
Lammergeyer,12.11,36.14
3rd Cohort Laquearius,,
Nix,17.42,9.77
Mudpuppy,14.62,30.41
Wild Hog,30.04,24.76
Watchwolf,21.43,20.78
5th Cohort Laquearius,11.36,13.48
Snow Wolf,,
Axolotl,14,15.5
Natalan Watchwolf,33.21,21.07
Zaharak Battle Drake,30.88,19.26
4th Cohort Vanguard,10.68,6.03
Hippocerf,10.64,19.54
Dead Mans Moan,15.8,35.84
Morbol,23,21
Lesser Kalong,,
Giant Reader,14.04,27.19
Hippogryph,29.93,8.82
5th Cohort Secutor,11.29,14.27
Tempered Gladiator,22,19
Sylphlands Condor,28.29,16.45
Milkroot Sapling,23.72,14.83
Ahriman,,
Shelfeye Reaver,13,17
3rd Cohort Hoplomachus,17.74,17.79
5th Cohort Eques,11.31,14.31
Sea Wasp,14,17
Sylph Bonnet,25.96,13.44
2nd Cohort Vanguard,29.7,19.98
Preying Mantis,15.29,34.44
3rd Cohort Eques,,
Lake Cobra,25.96,12.93
Giant Lugger,13.87,26.66
Tempered Orator,21,20
Dullahan,23.69,20.39
Basilisk,22.46,24.6
Gigas Bhikkhu,33.1,15.65
2nd Cohort Hoplomachus,28.13,21.05
Daring Harrier,16.96,16.11
5th Cohort Vanguard,10.97,15.1
Sylphlands Sentinel,23.41,10.85
2nd Cohort Eques,28.8,20.44
Molted Ziz,26.47,24.13
Crater Golem,10.9,17.42
Biast,14.45,28.7
5th Cohort Signifer,11.1,14.61
Synthetic Doblyn,22.33,7.39
Iron Tortoise,19.18,24.82
Milkroot Cluster,24.05,17.17
4th Cohort Secutor,10.63,6.44
2nd Cohort Laquearius,28.4,20.64
3rd Cohort Signifer,,
Raging Harrier,17.04,16.45
Gigas Shramana,29.75,12.69
5th Cohort Hoplomachus,11.77,13.67
Dreamtoad,27.28,18.56
Hapalit,31.21,6
Shelfclaw Reaver,13,17
3rd Cohort Secutor,,
Gigas Sozu,29.29,13.11
Giant Logger,14.59,26.73
Ked,32.35,24.02
4th Cohort Hoplomachus,11.25,6.33
2nd Cohort Signifer,28.67,20.66";
    }
}
