using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
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
    public override string Description => "Allow clicking on hunting log targets (including GC logs) to generate map marker. NB this is not Hunts!";

    private string _category { get; set; }
    private string _difficulty { get; set; }

    private delegate byte EventHandle(AtkUnitBase* atkUnitBase, AtkEventType eventType, uint eventParam, AtkEvent* atkEvent, void* a5);
    private delegate void* SetupHandle(AtkUnitBase* atkUnitBase, void* param_2, void* param_3);
    private HookWrapper<EventHandle> eventHook;
    private HookWrapper<SetupHandle> setupHook;
    private static readonly IEnumerable<TerritoryDetail> _territoryDetails = LoadTerritoryDetails();

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
                _category = atkUnitBase->GetNodeById(22)->GetAsAtkTextNode()->NodeText.ToString();
                _difficulty = atkUnitBase->GetNodeById(29)->GetAsAtkTextNode()->NodeText.ToString().Split(' ')[1];

                SimpleLog.Debug("Setup MonsterNote Events with " + atkUnitBase->X);
                var mainTreeListNode = (AtkComponentNode*)atkUnitBase->GetNodeById(46);
                SimpleLog.Debug(mainTreeListNode->ToString());

                for (
                    var listItemRenderer = (AtkComponentNode*)mainTreeListNode->Component->UldManager.SearchNodeById(5);
                    listItemRenderer->AtkResNode.NodeID != 6;// so many magic numbers; the stuff we want is stored in 5, 51001, 51002, etc and then the next type of thing starts at 6
                    listItemRenderer = listItemRenderer->AtkResNode.NextSiblingNode->GetAsAtkComponentNode()
                    )
                {
                    if (listItemRenderer is null) throw new Exception("aw no, this should literally never happen");

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
        string indent = new('\t', depth); //gives us n tab characters to make debugging recursive logs easier (also it's pretty)
        try
        {
            if (eventType == AtkEventType.MouseClick)
            {
                SimpleLog.Debug(indent + $"click with param {eventParam}; matches our magic number: {(eventParam & 0x10F2C000) == 0x10F2C000}");

                if ((eventParam & 0x10F2C000) == 0x10F2C000)
                {
                    AtkCollisionNode* colNode = (AtkCollisionNode*)atkEvent->Target;
                    SimpleLog.Debug(indent + "we're in it now");
                    // do the actual stuff!
                   
                    string mobName = colNode->AtkResNode.ParentNode->GetComponent()->UldManager.SearchNodeById(4)->GetAsAtkTextNode()->NodeText.ToString();

                    // so! it turns out there is a single case where the 'same' mob name gets used twice for a single class
                    // it's actually a completely separate mob! the two do not count towards each others' logs
                    // there are loads of cases where there are multiple instances of what seems to be the 'same' mob,
                    // in different places and with different levels but they count equally for whichever log you're on
                    // those are all cross-class though!
                    // this is an ugly, simple solution that (hopefully) works?
                    if (mobName.Equals("Puk Hatchling") && _difficulty.Equals("2"))
                    {
                        mobName = "Puk Hatchling2";
                    }

                    (string zone, float x, float y) location;
                    var foundIt = nameCoords.TryGetValue((mobName, _category), out location);

                    if (foundIt)
                        PutItOnTheMap(location);
                    else
                        SimpleLog.Log($"tried to look up a missing mob: {mobName}, {_category}. This may be in a dungeon, or I may have made an oopsy?");

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

    // 'borrowed' shamefully from chat coords plugin
    private void PutItOnTheMap((string zone, float x, float y) location)
    {
        SimpleLog.Debug($"handling coords for {location}");

        var td = _territoryDetails.Where(x => x.Name.Equals(location.zone, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

        var mapLink = new MapLinkPayload(
                td.TerritoryType,
                td.MapId,
                location.x,
                location.y,
                0f
            );

        Service.GameGui.OpenMapWithMapLink(mapLink);

    }

    private static IEnumerable<TerritoryDetail> LoadTerritoryDetails()
    {
        return (from territoryType in Service.Data.GetExcelSheet<TerritoryType>()
                let type = territoryType.Bg.RawString.Split('/')
                where type.Length >= 3
                where type[2] == "twn" || type[2] == "fld" || type[2] == "hou"
                where !string.IsNullOrWhiteSpace(territoryType.Map.Value.PlaceName.Value.Name)
                select new TerritoryDetail
                {
                    TerritoryType = territoryType.RowId,
                    MapId = territoryType.Map.Value.RowId,
                    SizeFactor = territoryType.Map.Value.SizeFactor,
                    Name = territoryType.Map.Value.PlaceName.Value.Name
                }).ToList();
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

    private static readonly Dictionary<(string name, string category), (string zone, float x, float y)> nameCoords = new()
    {
        {("Little Ladybug", "Arcanist"), ("Middle La Noscea", 22f, 24f)},
        {("Wharf Rat", "Arcanist"), ("Middle La Noscea", 21f, 23f)},
        {("Lost Lamb", "Arcanist"), ("Middle La Noscea", 23f, 24f)},
        {("Wind Sprite", "Arcanist"), ("Lower La Noscea", 28f, 20f)},
        {("Puk Hatchling", "Arcanist"), ("Middle La Noscea", 20f, 19f)},
        {("Nesting Buzzard", "Arcanist"), ("Lower La Noscea", 31f, 16f)},
        {("Bogy", "Arcanist"), ("Middle La Noscea", 20f, 19f)},
        {("Cave Bat", "Arcanist"), ("Lower La Noscea", 25f, 15f)},
        {("Galago", "Arcanist"), ("Lower La Noscea", 29f, 14f)},
        {("Grounded Pirate", "Arcanist"), ("Middle La Noscea", 20f, 16f)},
        {("Lightning Sprite", "Arcanist"), ("Lower La Noscea", 20f, 32f)},
        {("Sewer Mole", "Arcanist"), ("Western La Noscea", 33f, 28f)},
        {("Mossless Goobbue", "Arcanist"), ("Middle La Noscea", 21f, 23f)},
        {("Fat Dodo", "Arcanist"), ("Western La Noscea", 32f, 27f)},
        {("Arbor Buzzard", "Arcanist"), ("Western La Noscea", 30f, 29f)},
        {("Qiqirn Eggdigger", "Arcanist"), ("Lower La Noscea", 18f, 35f)},
        {("Dusk Bat", "Arcanist"), ("Western La Noscea", 28f, 22f)},
        {("Puk Hatchling2", "Arcanist"), ("Western La Noscea", 28f, 22f)},
        {("Hedgemole", "Arcanist"), ("Western La Noscea", 26f, 23f)},
        {("Rothlyt Pelican", "Arcanist"), ("Western La Noscea", 24f, 22f)},
        {("Killer Mantis", "Arcanist"), ("Western La Noscea", 22f, 20f)},
        {("Bumble Beetle", "Arcanist"), ("Upper La Noscea", 12f, 23f)},
        {("Overgrown Ivy", "Arcanist"), ("East Shroud", 23f, 29f)},
        {("Lead Coblyn", "Arcanist"), ("Western Thanalan", 13f, 10f)},
        {("Kedtrap", "Arcanist"), ("South Shroud", 21f, 21f)},
        {("Coeurl Pup", "Arcanist"), ("Upper La Noscea", 9f, 20f)},
        {("Antelope Stag", "Arcanist"), ("South Shroud", 22f, 20f)},
        {("Balloon", "Arcanist"), ("North Shroud", 17f, 26f)},
        {("Chasm Buzzard", "Arcanist"), ("Eastern Thanalan", 22f, 20f)},
        {("Axe Beak", "Arcanist"), ("Eastern Thanalan", 25f, 18f)},
        {("Clay Golem", "Arcanist"), ("North Shroud", 19f, 28f)},
        {("Sandstone Golem", "Arcanist"), ("Southern Thanalan", 22f, 11f)},
        {("Brood Ziz", "Arcanist"), ("Central Shroud", 15f, 20f)},
        {("Lindwurm", "Arcanist"), ("Central Shroud", 13f, 19f)},
        {("Qiqirn Gullroaster", "Arcanist"), ("Eastern La Noscea", 26f, 32f)},
        {("Grass Raptor", "Arcanist"), ("Eastern La Noscea", 16f, 25f)},
        {("Gigantoad", "Arcanist"), ("Eastern La Noscea", 18f, 26f)},
        {("Sundrake", "Arcanist"), ("Southern Thanalan", 25f, 38f)},
        {("Colibri", "Arcanist"), ("Eastern La Noscea", 30f, 24f)},
        {("Coeurl", "Arcanist"), ("Outer La Noscea", 14f, 14f)},
        {("Mildewed Goobbue", "Arcanist"), ("Eastern La Noscea", 17f, 32f)},
        {("Snow Wolf Pup", "Arcanist"), ("Coerthas Central Highlands", 21f, 29f)},
        {("Feral Croc", "Arcanist"), ("Coerthas Central Highlands", 27f, 24f)},
        {("Dryad", "Arcanist"), ("North Shroud", 23f, 25f)},
        {("Taurus", "Arcanist"), ("Coerthas Central Highlands", 32f, 12f)},
        {("Molted Ziz", "Arcanist"), ("East Shroud", 25f, 24f)},
        {("Quartz Doblyn", "Arcanist"), ("Eastern Thanalan", 30f, 25f)},
        {("Lammergeyer", "Arcanist"), ("Western La Noscea", 12f, 36f)},
        {("3rd Cohort Laquearius", "Arcanist"), ("East Shroud", 29f, 20f)},
        {("Nix", "Arcanist"), ("Mor Dhona", 19f, 9f)},
        {("Mudpuppy", "Arcanist"), ("Mor Dhona", 14f, 10f)},
        {("Wild Hog", "Arcanist"), ("South Shroud", 29f, 24f)},
        {("Watchwolf", "Arcanist"), ("North Shroud", 20f, 18f)},
        {("5th Cohort Laquearius", "Arcanist"), ("Mor Dhona", 11f, 11f)},
        {("Snow Wolf", "Arcanist"), ("Coerthas Central Highlands", 16f, 32f)},
        {("Natalan Watchwolf", "Arcanist"), ("Coerthas Central Highlands", 31f, 18f)},
        {("Axolotl", "Arcanist"), ("Western La Noscea", 12f, 15f)},
        {("Zahar'ak Battle Drake", "Arcanist"), ("Southern Thanalan", 31f, 19f)},
        {("4th Cohort Vanguard", "Arcanist"), ("Western Thanalan", 12f, 7f)},
        {("Wild Boar", "Archer "), ("East Shroud", 16f, 23f)},
        {("Little Ladybug", "Archer"), ("North Shroud", 25f, 27f)},
        {("Ground Squirrel", "Archer"), ("Central Shroud", 22f, 17f)},
        {("Forest Funguar", "Archer"), ("Central Shroud", 24f, 18f)},
        {("Miteling", "Archer"), ("North Shroud", 25f, 27f)},
        {("Midge Swarm", "Archer"), ("North Shroud", 27f, 21f)},
        {("Water Sprite", "Archer"), ("Central Shroud", 25f, 21f)},
        {("Black Eft", "Archer"), ("Central Shroud", 26f, 19f)},
        {("Anole", "Archer"), ("Central Shroud", 30f, 20f)},
        {("Trickster Imp", "Archer"), ("Central Shroud", 26f, 25f)},
        {("Roselet", "Archer"), ("Central Shroud", 22f, 26f)},
        {("Hornet Swarm", "Archer"), ("Central Shroud", 23f, 25f)},
        {("Arbor Buzzard", "Archer"), ("Central Shroud", 26f, 29f)},
        {("Magicked Bones", "Archer"), ("Central Shroud", 19f, 27f)},
        {("Treant Sapling", "Archer"), ("Central Shroud", 27f, 15f)},
        {("Goblin Hunter", "Archer"), ("East Shroud", 13f, 27f)},
        {("Mandragora", "Archer"), ("East Shroud", 14f, 25f)},
        {("Wild Hoglet", "Archer"), ("East Shroud", 13f, 23f)},
        {("Lemur", "Archer"), ("East Shroud", 20f, 29f)},
        {("Faerie Funguar", "Archer"), ("East Shroud", 18f, 28f)},
        {("Giant Gnat", "Archer"), ("East Shroud", 19f, 27f)},
        {("Raptor Poacher", "Archer"), ("East Shroud", 19f, 30f)},
        {("Antelope Doe", "Archer"), ("South Shroud", 15f, 18f)},
        {("Stoneshell", "Archer"), ("Upper La Noscea", 13f, 24f)},
        {("Diseased Treant", "Archer"), ("East Shroud", 16f, 23f)},
        {("Overgrown Offering", "Archer"), ("South Shroud", 18f, 19f)},
        {("Yarzon Scavenger", "Archer"), ("Western Thanalan", 14f, 8f)},
        {("Forest Yarzon", "Archer"), ("North Shroud", 21f, 31f)},
        {("Jumping Djigga", "Archer"), ("Upper La Noscea", 12f, 21f)},
        {("Redbelly Sharpeye", "Archer"), ("South Shroud", 23f, 19f)},
        {("Banemite", "Archer"), ("North Shroud", 18f, 26f)},
        {("Chasm Buzzard", "Archer"), ("Eastern Thanalan", 22f, 19f)},
        {("Sandskin Peiste", "Archer"), ("Southern Thanalan", 19f, 10f)},
        {("Ziz", "Archer"), ("North Shroud", 15f, 27f)},
        {("Toadstool", "Archer"), ("Central Shroud", 15f, 17f)},
        {("Apkallu", "Archer"), ("Eastern La Noscea", 28f, 35f)},
        {("Floating Eye", "Archer"), ("Central Shroud", 11f, 22f)},
        {("Sandworm", "Archer"), ("Southern Thanalan", 23f, 32f)},
        {("Russet Yarzon", "Archer"), ("Southern Thanalan", 13f, 32f)},
        {("Giant Pelican", "Archer"), ("Eastern La Noscea", 20f, 32f)},
        {("Smoke Bomb", "Archer"), ("Southern Thanalan", 19f, 35f)},
        {("Spriggan", "Archer"), ("Central Shroud", 12f, 16f)},
        {("Bloodshore Bell", "Archer"), ("Eastern La Noscea", 31f, 26f)},
        {("Jungle Coeurl", "Archer"), ("Eastern La Noscea", 18f, 28f)},
        {("Ringtail", "Archer"), ("Outer La Noscea", 15f, 12f)},
        {("Highland Condor", "Archer"), ("Outer La Noscea", 16f, 16f)},
        {("Salamander", "Archer"), ("Upper La Noscea", 27f, 24f)},
        {("Fallen Pikeman", "Archer"), ("Southern Thanalan", 21f, 38f)},
        {("Ice Sprite", "Archer"), ("Coerthas Central Highlands", 20f, 31f)},
        {("Feral Croc", "Archer"), ("Coerthas Central Highlands", 26f, 24f)},
        {("Vodoriga", "Archer"), ("Coerthas Central Highlands", 27f, 14f)},
        {("Baritine Croc", "Archer"), ("Coerthas Central Highlands", 3f, 21f)},
        {("Hippocerf", "Archer"), ("Coerthas Central Highlands", 9f, 20f)},
        {("Dragonfly", "Archer"), ("Coerthas Central Highlands", 9f, 14f)},
        {("Oldgrowth Treant", "Archer"), ("East Shroud", 25f, 20f)},
        {("Lammergeyer", "Archer"), ("Western La Noscea", 12f, 36f)},
        {("Dead Man's Moan", "Archer"), ("Western La Noscea", 14f, 35f)},
        {("Morbol", "Archer"), ("East Shroud", 23f, 21f)},
        {("Mudpuppy", "Archer"), ("Coerthas Central Highlands", 16f, 30f)},
        {("Lesser Kalong", "Archer"), ("South Shroud", 29f, 23f)},
        {("Giant Reader", "Archer"), ("Coerthas Central Highlands", 13f, 28f)},
        {("Hippogryph", "Archer"), ("Mor Dhona", 32f, 8f)},
        {("5th Cohort Secutor", "Archer"), ("Mor Dhona", 11f, 13f)},
        {("Tempered Gladiator", "Archer"), ("Southern Thanalan", 21f, 19f)},
        {("Sylphlands Condor", "Archer"), ("East Shroud", 26f, 16f)},
        {("Milkroot Sapling", "Archer"), ("East Shroud", 23f, 13f)},
        {("Ahriman", "Archer"), ("Northern Thanalan", 24f, 22f)},
        {("Shelfeye Reaver", "Archer"), ("Western La Noscea", 13f, 16f)},
        {("Little Ladybug", "Conjurer"), ("Central Shroud", 23f, 17f)},
        {("Ground Squirrel", "Conjurer"), ("Central Shroud", 21f, 16f)},
        {("Forest Funguar", "Conjurer"), ("Central Shroud", 25f, 18f)},
        {("Miteling", "Conjurer"), ("North Shroud", 24f, 27f)},
        {("Chigoe", "Conjurer"), ("Central Shroud", 24f, 21f)},
        {("Water Sprite", "Conjurer"), ("Central Shroud", 24f, 21f)},
        {("Midge Swarm", "Conjurer"), ("North Shroud", 26f, 21f)},
        {("Microchu", "Conjurer"), ("North Shroud", 26f, 22f)},
        {("Syrphid Swarm", "Conjurer"), ("Central Shroud", 27f, 24f)},
        {("Northern Vulture", "Conjurer"), ("East Shroud", 14f, 27f)},
        {("Tree Slug", "Conjurer"), ("Central Shroud", 25f, 29f)},
        {("Arbor Buzzard", "Conjurer"), ("Central Shroud", 26f, 29f)},
        {("Goblin Hunter", "Conjurer"), ("East Shroud", 13f, 27f)},
        {("Firefly", "Conjurer"), ("Eastern Thanalan", 24f, 29f)},
        {("Mandragora", "Conjurer"), ("East Shroud", 14f, 26f)},
        {("Boring Weevil", "Conjurer"), ("East Shroud", 20f, 28f)},
        {("Faerie Funguar", "Conjurer"), ("East Shroud", 19f, 28f)},
        {("Giant Gnat", "Conjurer"), ("East Shroud", 19f, 25f)},
        {("Wolf Poacher", "Conjurer"), ("East Shroud", 19f, 30f)},
        {("Qiqirn Beater", "Conjurer"), ("South Shroud", 15f, 17f)},
        {("Black Bat", "Conjurer"), ("East Shroud", 16f, 21f)},
        {("Stoneshell", "Conjurer"), ("Upper La Noscea", 13f, 24f)},
        {("Laughing Toad", "Conjurer"), ("Western Thanalan", 14f, 7f)},
        {("Diseased Treant", "Conjurer"), ("East Shroud", 16f, 23f)},
        {("Lead Coblyn", "Conjurer"), ("Western Thanalan", 13f, 10f)},
        {("Bark Eft", "Conjurer"), ("South Shroud", 17f, 24f)},
        {("Glowfly", "Conjurer"), ("East Shroud", 15f, 20f)},
        {("Antelope Stag", "Conjurer"), ("South Shroud", 22f, 19f)},
        {("Sabotender", "Conjurer"), ("Southern Thanalan", 15f, 14f)},
        {("Qiqirn Roerunner", "Conjurer"), ("Eastern Thanalan", 24f, 23f)},
        {("Goblin Thug", "Conjurer"), ("South Shroud", 28f, 20f)},
        {("Toadstool", "Conjurer"), ("Central Shroud", 14f, 17f)},
        {("Apkallu", "Conjurer"), ("Eastern La Noscea", 28f, 35f)},
        {("Lindwurm", "Conjurer"), ("Central Shroud", 12f, 19f)},
        {("Gigantoad", "Conjurer"), ("Eastern La Noscea", 18f, 26f)},
        {("Bigmouth Orobon", "Conjurer"), ("South Shroud", 18f, 30f)},
        {("Mamool Ja Infiltrator", "Conjurer"), ("Upper La Noscea", 28f, 23f)},
        {("Sandworm", "Conjurer"), ("Southern Thanalan", 28f, 25f)},
        {("Revenant", "Conjurer"), ("Central Shroud", 12f, 20f)},
        {("Bloodshore Bell", "Conjurer"), ("Eastern La Noscea", 31f, 26f)},
        {("Ornery Karakul", "Conjurer"), ("Coerthas Central Highlands", 25f, 19f)},
        {("Deepvoid Deathmouse", "Conjurer"), ("South Shroud", 25f, 21f)},
        {("Dryad", "Conjurer"), ("North Shroud", 22f, 23f)},
        {("Downy Aevis", "Conjurer"), ("Coerthas Central Highlands", 26f, 10f)},
        {("Will-o'-the-wisp", "Conjurer"), ("South Shroud", 22f, 25f)},
        {("Dragonfly", "Conjurer"), ("Coerthas Central Highlands", 9f, 14f)},
        {("Golden Fleece", "Conjurer"), ("Eastern Thanalan", 26f, 25f)},
        {("Grenade", "Conjurer"), ("Outer La Noscea", 22f, 13f)},
        {("Hippocerf", "Conjurer"), ("Coerthas Central Highlands", 10f, 18f)},
        {("Lammergeyer", "Conjurer"), ("Western La Noscea", 12f, 36f)},
        {("Dead Man's Moan", "Conjurer"), ("Western La Noscea", 17f, 36f)},
        {("3rd Cohort Hoplomachus", "Conjurer"), ("East Shroud", 29f, 20f)},
        {("Lesser Kalong", "Conjurer"), ("South Shroud", 28f, 22f)},
        {("Snow Wolf", "Conjurer"), ("Coerthas Central Highlands", 16f, 32f)},
        {("5th Cohort Eques", "Conjurer"), ("Mor Dhona", 12f, 17f)},
        {("Sea Wasp", "Conjurer"), ("Western La Noscea", 14f, 17f)},
        {("Sylph Bonnet", "Conjurer"), ("East Shroud", 26f, 13f)},
        {("Ahriman", "Conjurer"), ("Northern Thanalan", 24f, 20f)},
        {("2nd Cohort Vanguard", "Conjurer"), ("Eastern La Noscea", 29f, 21f)},
        {("Little Ladybug", "Gladiator"), ("Western Thanalan", 27f, 24f)},
        {("Star Marmot", "Gladiator"), ("Central Thanalan", 20f, 27f)},
        {("Cactuar", "Gladiator"), ("Western Thanalan", 27f, 24f)},
        {("Snapping Shrew", "Gladiator"), ("Central Thanalan", 25f, 31f)},
        {("Hammer Beak", "Gladiator"), ("Western Thanalan", 26f, 22f)},
        {("Antling Worker", "Gladiator"), ("Central Thanalan", 17f, 15f)},
        {("Earth Sprite", "Gladiator"), ("Western Thanalan", 17f, 28f)},
        {("Spriggan Graverobber", "Gladiator"), ("Central Thanalan", 16f, 23f)},
        {("Qiqirn Shellsweeper", "Gladiator"), ("Central Thanalan", 17f, 19f)},
        {("Antling Soldier", "Gladiator"), ("Central Thanalan", 23f, 19f)},
        {("Dusty Mongrel", "Gladiator"), ("The Clutch", 22f, 21f)},
        {("Bomb", "Gladiator"), ("Western Thanalan", 27f, 17f)},
        {("Copper Coblyn", "Gladiator"), ("Horizon's Edge", 27f, 17f)},
        {("Cochineal Cactuar", "Gladiator"), ("Central Thanalan", 24f, 19f)},
        {("Quiveron Guard", "Gladiator"), ("Central Thanalan", 24f, 21f)},
        {("Giant Tortoise", "Gladiator"), ("Western Thanalan", 21f, 26f)},
        {("Thickshell", "Gladiator"), ("Western Thanalan", 16f, 16f)},
        {("Scaphite", "Gladiator"), ("Western Thanalan", 17f, 14f)},
        {("Tuco-tuco", "Gladiator"), ("Eastern Thanalan", 11f, 19f)},
        {("Myotragus Billy", "Gladiator"), ("Eastern Thanalan", 18f, 24f)},
        {("Vandalous Imp", "Gladiator"), ("Eastern Thanalan", 14f, 18f)},
        {("Rotting Noble", "Gladiator"), ("Eastern Thanalan", 14f, 16f)},
        {("Bloated Bogy", "Gladiator"), ("Drybone", 13f, 11f)},
        {("Stoneshell", "Gladiator"), ("Upper La Noscea", 14f, 24f)},
        {("Kedtrap", "Gladiator"), ("Oakwood", 21f, 20f)},
        {("Lead Coblyn", "Gladiator"), ("Western Thanalan", 13f, 10f)},
        {("Overgrown Offering", "Gladiator"), ("South Shroud", 18f, 19f)},
        {("Coeurl Pup", "Gladiator"), ("Upper La Noscea", 9f, 20f)},
        {("Balloon", "Gladiator"), ("North Shroud", 17f, 26f)},
        {("Sabotender", "Gladiator"), ("Southern Thanalan", 14f, 13f)},
        {("Qiqirn Roerunner", "Gladiator"), ("Eastern Thanalan", 24f, 23f)},
        {("Goblin Thug", "Gladiator"), ("South Shroud", 27f, 20f)},
        {("Coeurlclaw Cutter", "Gladiator"), ("South Shroud", 28f, 21f)},
        {("Apkallu", "Gladiator"), ("Eastern La Noscea", 28f, 36f)},
        {("Pteroc", "Gladiator"), ("Bloodshore", 15f, 18f)},
        {("Floating Eye", "Gladiator"), ("Central Shroud", 10f, 23f)},
        {("Mamool Ja Sophist", "Gladiator"), ("Sorrel Haven", 27f, 23f)},
        {("Uragnite", "Gladiator"), ("Upper La Noscea", 29f, 24f)},
        {("Adamantoise", "Gladiator"), ("Bronze Lake", 17f, 30f)},
        {("Sandworm", "Gladiator"), ("Southern Thanalan", 17f, 33f)},
        {("Death Gaze", "Gladiator"), ("Central Shroud", 16f, 21f)},
        {("Velociraptor", "Gladiator"), ("Outer La Noscea", 19f, 15f)},
        {("Fallen Wizard", "Gladiator"), ("Southern Thanalan", 20f, 37f)},
        {("Snow Wolf Pup", "Gladiator"), ("Coerthas Central Highlands", 21f, 29f)},
        {("Treant", "Gladiator"), ("South Shroud", 22f, 26f)},
        {("Vodoriga", "Gladiator"), ("Coerthas Central Highlands", 27f, 14f)},
        {("Hippocerf", "Gladiator"), ("Coerthas Central Highlands", 8f, 20f)},
        {("Grenade", "Gladiator"), ("Whitebrim", 22f, 13f)},
        {("Preying Mantis", "Gladiator"), ("Western La Noscea", 15f, 34f)},
        {("Lammergeyer", "Gladiator"), ("The Isles of Umbra", 12f, 36f)},
        {("Oldgrowth Treant", "Gladiator"), ("East Shroud", 26f, 20f)},
        {("3rd Cohort Eques", "Gladiator"), ("Larkscall", 29f, 21f)},
        {("Dead Man's Moan", "Gladiator"), ("Western La Noscea", 12f, 36f)},
        {("Morbol", "Gladiator"), ("The Isles of Umbra", 23f, 21f)},
        {("Mudpuppy", "Gladiator"), ("Mor Dhona", 13f, 11f)},
        {("Lake Cobra", "Gladiator"), ("Mor Dhona", 27f, 13f)},
        {("Giant Lugger", "Gladiator"), ("Coerthas Central Highlands", 13f, 27f)},
        {("Tempered Orator", "Gladiator"), ("Southern Thanalan", 22f, 19f)},
        {("Dullahan", "Gladiator"), ("North Shroud", 22f, 19f)},
        {("Basilisk", "Gladiator"), ("Northern Thanalan", 25f, 21f)},
        {("Gigas Bhikkhu", "Gladiator"), ("Mor Dhona", 33f, 15f)},
        {("2nd Cohort Hoplomachus", "Gladiator"), ("North Silvertear", 25f, 21f)},
        {("Little Ladybug", "Lancer"), ("Central Shroud", 24f, 18f)},
        {("Ground Squirrel", "Lancer"), ("Central Shroud", 22f, 17f)},
        {("Forest Funguar", "Lancer"), ("Central Shroud", 25f, 19f)},
        {("Miteling", "Lancer"), ("North Shroud", 26f, 26f)},
        {("Opo-opo", "Lancer"), ("North Shroud", 27f, 24f)},
        {("Microchu", "Lancer"), ("North Shroud", 26f, 22f)},
        {("Black Eft", "Lancer"), ("Central Shroud", 26f, 20f)},
        {("Bog Yarzon", "Lancer"), ("Central Shroud", 24f, 24f)},
        {("Hoglet", "Lancer"), ("Central Shroud", 30f, 23f)},
        {("Anole", "Lancer"), ("Central Shroud", 31f, 20f)},
        {("Diremite", "Lancer"), ("Central Shroud", 18f, 19f)},
        {("Tree Slug", "Lancer"), ("East Shroud", 13f, 26f)},
        {("Arbor Buzzard", "Lancer"), ("Central Shroud", 25f, 29f)},
        {("Treant Sapling", "Lancer"), ("Central Shroud", 22f, 16f)},
        {("Mandragora", "Lancer"), ("East Shroud", 14f, 25f)},
        {("Wild Hoglet", "Lancer"), ("East Shroud", 13f, 23f)},
        {("Lemur", "Lancer"), ("East Shroud", 15f, 27f)},
        {("Boring Weevil", "Lancer"), ("East Shroud", 15f, 27f)},
        {("Faerie Funguar", "Lancer"), ("East Shroud", 19f, 28f)},
        {("Giant Gnat", "Lancer"), ("East Shroud", 19f, 27f)},
        {("Boar Poacher", "Lancer"), ("East Shroud", 19f, 30f)},
        {("Ziz Gorlin", "Lancer"), ("East Shroud", 20f, 25f)},
        {("Black Bat", "Lancer"), ("East Shroud", 17f, 22f)},
        {("Qiqirn Beater", "Lancer"), ("South Shroud", 15f, 17f)},
        {("Antelope Doe", "Lancer"), ("South Shroud", 18f, 22f)},
        {("Stoneshell", "Lancer"), ("Upper La Noscea", 13f, 24f)},
        {("Smallmouth Orobon", "Lancer"), ("South Shroud", 16f, 18f)},
        {("Yarzon Scavenger", "Lancer"), ("North Shroud", 20f, 31f)},
        {("Redbelly Lookout", "Lancer"), ("South Shroud", 24f, 18f)},
        {("Antelope Stag", "Lancer"), ("South Shroud", 26f, 18f)},
        {("Moondrip Piledriver", "Lancer"), ("Western Thanalan", 16f, 7f)},
        {("Sabotender", "Lancer"), ("Southern Thanalan", 14f, 14f)},
        {("Goblin Thug", "Lancer"), ("South Shroud", 27f, 20f)},
        {("Sandskin Peiste", "Lancer"), ("Southern Thanalan", 19f, 10f)},
        {("Corpse Brigade Firedancer", "Lancer"), ("Southern Thanalan", 24f, 9f)},
        {("Coeurlclaw Poacher", "Lancer"), ("South Shroud", 28f, 21f)},
        {("Apkallu", "Lancer"), ("Eastern La Noscea", 28f, 35f)},
        {("Midland Condor", "Lancer"), ("South Shroud", 17f, 26f)},
        {("Floating Eye", "Lancer"), ("Central Shroud", 10f, 23f)},
        {("Large Buffalo", "Lancer"), ("Eastern La Noscea", 28f, 30f)},
        {("Smoke Bomb", "Lancer"), ("Southern Thanalan", 19f, 34f)},
        {("Sundrake", "Lancer"), ("Southern Thanalan", 25f, 38f)},
        {("Spriggan", "Lancer"), ("Central Shroud", 10f, 16f)},
        {("Basalt Golem", "Lancer"), ("Outer La Noscea", 13f, 15f)},
        {("Ringtail", "Lancer"), ("Outer La Noscea", 15f, 11f)},
        {("Ornery Karakul", "Lancer"), ("Coerthas Central Highlands", 25f, 19f)},
        {("Lesser Kalong", "Lancer"), ("South Shroud", 22f, 24f)},
        {("Snow Wolf Pup", "Lancer"), ("Coerthas Central Highlands", 21f, 29f)},
        {("Dryad", "Lancer"), ("North Shroud", 22f, 23f)},
        {("Bateleur", "Lancer"), ("Coerthas Central Highlands", 17f, 17f)},
        {("Downy Aevis", "Lancer"), ("Coerthas Central Highlands", 27f, 10f)},
        {("Mirrorknight", "Lancer"), ("Eastern Thanalan", 26f, 24f)},
        {("Dragonfly", "Lancer"), ("Coerthas Central Highlands", 9f, 14f)},
        {("Baritine Croc", "Lancer"), ("Coerthas Central Highlands", 3f, 21f)},
        {("Dead Man's Moan", "Lancer"), ("Western La Noscea", 15f, 35f)},
        {("3rd Cohort Signifer", "Lancer"), ("East Shroud", 32f, 20f)},
        {("Morbol", "Lancer"), ("East Shroud", 23f, 21f)},
        {("Wild Hog", "Lancer"), ("South Shroud", 29f, 24f)},
        {("Daring Harrier", "Lancer"), ("Mor Dhona", 16f, 15f)},
        {("Lake Cobra", "Lancer"), ("Mor Dhona", 25f, 12f)},
        {("Snow Wolf", "Lancer"), ("Coerthas Central Highlands", 16f, 32f)},
        {("Sea Wasp", "Lancer"), ("Western La Noscea", 13f, 17f)},
        {("5th Cohort Vanguard", "Lancer"), ("Mor Dhona", 10f, 13f)},
        {("Natalan Watchwolf", "Lancer"), ("Coerthas Central Highlands", 34f, 23f)},
        {("Sylphlands Sentinel", "Lancer"), ("East Shroud", 24f, 11f)},
        {("Basilisk", "Lancer"), ("Northern Thanalan", 22f, 24f)},
        {("2nd Cohort Eques", "Lancer"), ("Eastern La Noscea", 30f, 21f)},
        {("Little Ladybug", "Marauder"), ("Middle La Noscea", 23f, 24f)},
        {("Wharf Rat", "Marauder"), ("Middle La Noscea", 21f, 23f)},
        {("Aurelia", "Marauder"), ("Lower La Noscea", 24f, 26f)},
        {("Bee Cloud", "Marauder"), ("Middle La Noscea", 19f, 18f)},
        {("Wild Dodo", "Marauder"), ("Lower La Noscea", 29f, 19f)},
        {("Tiny Mandragora", "Marauder"), ("Middle La Noscea", 22f, 18f)},
        {("Bogy", "Marauder"), ("Middle La Noscea", 20f, 19f)},
        {("Wounded Aurochs", "Marauder"), ("Middle La Noscea", 18f, 17f)},
        {("Grounded Raider", "Marauder"), ("Middle La Noscea", 20f, 17f)},
        {("Megalocrab", "Marauder"), ("Middle La Noscea", 14f, 13f)},
        {("Firefly", "Marauder"), ("Lower La Noscea", 26f, 37f)},
        {("Mossless Goobbue", "Marauder"), ("Lower La Noscea", 22f, 24f)},
        {("Fat Dodo", "Marauder"), ("Western La Noscea", 32f, 27f)},
        {("Moraby Mole", "Marauder"), ("Lower La Noscea", 21f, 34f)},
        {("Qiqirn Eggdigger", "Marauder"), ("Lower La Noscea", 18f, 35f)},
        {("Rhotano Buccaneer", "Marauder"), ("Western La Noscea", 33f, 27f)},
        {("Dusk Bat", "Marauder"), ("Western La Noscea", 28f, 24f)},
        {("Puk Hatchling", "Marauder"), ("Western La Noscea", 28f, 24f)},
        {("Hedgemole", "Marauder"), ("Western La Noscea", 26f, 23f)},
        {("Rothlyt Pelican", "Marauder"), ("Western La Noscea", 24f, 22f)},
        {("Killer Mantis", "Marauder"), ("Western La Noscea", 21f, 22f)},
        {("Wild Wolf", "Marauder"), ("Upper La Noscea", 13f, 26f)},
        {("Stoneshell", "Marauder"), ("Upper La Noscea", 13f, 24f)},
        {("Diseased Treant", "Marauder"), ("East Shroud", 17f, 23f)},
        {("Yarzon Scavenger", "Marauder"), ("Western Thanalan", 15f, 7f)},
        {("Redbelly Larcener", "Marauder"), ("South Shroud", 23f, 19f)},
        {("Shroud Hare", "Marauder"), ("North Shroud", 17f, 27f)},
        {("Sabotender", "Marauder"), ("Southern Thanalan", 14f, 14f)},
        {("Balloon", "Marauder"), ("North Shroud", 17f, 26f)},
        {("Phurble", "Marauder"), ("Eastern Thanalan", 19f, 28f)},
        {("Sandskin Peiste", "Marauder"), ("Southern Thanalan", 22f, 21f)},
        {("Axe Beak", "Marauder"), ("Eastern Thanalan", 27f, 17f)},
        {("Toadstool", "Marauder"), ("Central Shroud", 14f, 17f)},
        {("Floating Eye", "Marauder"), ("Central Shroud", 10f, 23f)},
        {("Stroper", "Marauder"), ("South Shroud", 19f, 28f)},
        {("Adamantoise", "Marauder"), ("South Shroud", 15f, 29f)},
        {("Smoke Bomb", "Marauder"), ("Southern Thanalan", 19f, 34f)},
        {("Grass Raptor", "Marauder"), ("Eastern La Noscea", 15f, 26f)},
        {("Snipper", "Marauder"), ("Eastern La Noscea", 31f, 35f)},
        {("Bloodshore Bell", "Marauder"), ("Eastern La Noscea", 31f, 26f)},
        {("Jungle Coeurl", "Marauder"), ("Eastern La Noscea", 19f, 28f)},
        {("Snow Wolf Pup", "Marauder"), ("Coerthas Central Highlands", 21f, 29f)},
        {("Redhorn Ogre", "Marauder"), ("Coerthas Central Highlands", 24f, 13f)},
        {("Ornery Karakul", "Marauder"), ("Coerthas Central Highlands", 25f, 19f)},
        {("Highland Goobbue", "Marauder"), ("Coerthas Central Highlands", 25f, 19f)},
        {("Downy Aevis", "Marauder"), ("Coerthas Central Highlands", 26f, 10f)},
        {("Snowstorm Goobbue", "Marauder"), ("Coerthas Central Highlands", 19f, 17f)},
        {("Grenade", "Marauder"), ("Outer La Noscea", 23f, 13f)},
        {("Molted Ziz", "Marauder"), ("East Shroud", 25f, 24f)},
        {("Quartz Doblyn", "Marauder"), ("Eastern Thanalan", 31f, 26f)},
        {("Dead Man's Moan", "Marauder"), ("The Burning Wall", 15f, 35f)},
        {("Morbol", "Marauder"), ("Western La Noscea", 23f, 21f)},
        {("Crater Golem", "Marauder"), ("Central Shroud", 11f, 17f)},
        {("Wild Hog", "Marauder"), ("South Shroud", 29f, 24f)},
        {("Biast", "Marauder"), ("Coerthas Central Highlands", 16f, 30f)},
        {("5th Cohort Signifer", "Marauder"), ("Mor Dhona", 9f, 14f)},
        {("Synthetic Doblyn", "Marauder"), ("Outer La Noscea", 23f, 8f)},
        {("Watchwolf", "Marauder"), ("U'Ghamaro Mines", 20f, 20f)},
        {("Iron Tortoise", "Marauder"), ("North Shroud", 19f, 23f)},
        {("Milkroot Cluster", "Marauder"), ("East Shroud", 23f, 16f)},
        {("4th Cohort Secutor", "Marauder"), ("Western Thanalan", 10f, 6f)},
        {("2nd Cohort Laquearius", "Marauder"), ("Eastern La Noscea", 28f, 21f)},
        {("Huge Hornet", "Pugilist"), ("Central Thanalan", 21f, 26f)},
        {("Star Marmot", "Pugilist"), ("Central Thanalan", 20f, 27f)},
        {("Cactuar", "Pugilist"), ("Western Thanalan", 27f, 24f)},
        {("Snapping Shrew", "Pugilist"), ("Central Thanalan", 23f, 27f)},
        {("Orobon", "Pugilist"), ("Central Thanalan", 21f, 24f)},
        {("Nesting Buzzard", "Pugilist"), ("Western Thanalan", 21f, 25f)},
        {("Spriggan Graverobber", "Pugilist"), ("Central Thanalan", 17f, 23f)},
        {("Goblin Mugger", "Pugilist"), ("Western Thanalan", 18f, 26f)},
        {("Sandtoad", "Pugilist"), ("Western Thanalan", 22f, 22f)},
        {("Eft", "Pugilist"), ("Central Thanalan", 23f, 18f)},
        {("Sun Midge Swarm", "Pugilist"), ("Western Thanalan", 17f, 15f)},
        {("Desert Peiste", "Pugilist"), ("Western Thanalan", 24f, 20f)},
        {("Bomb", "Pugilist"), ("Western Thanalan", 27f, 16f)},
        {("Cochineal Cactuar", "Pugilist"), ("Central Thanalan", 25f, 21f)},
        {("Antling Sentry", "Pugilist"), ("Central Thanalan", 16f, 14f)},
        {("Giant Tortoise", "Pugilist"), ("Western Thanalan", 28f, 25f)},
        {("Arbor Buzzard", "Pugilist"), ("Western Thanalan", 17f, 16f)},
        {("Scaphite", "Pugilist"), ("Western Thanalan", 17f, 14f)},
        {("Thickshell", "Pugilist"), ("Western Thanalan", 16f, 16f)},
        {("Tuco-tuco", "Pugilist"), ("Eastern Thanalan", 15f, 20f)},
        {("Myotragus Nanny", "Pugilist"), ("Eastern Thanalan", 18f, 22f)},
        {("Blowfly Swarm", "Pugilist"), ("Eastern Thanalan", 11f, 22f)},
        {("Vandalous Imp", "Pugilist"), ("Eastern Thanalan", 14f, 18f)},
        {("Bloated Bogy", "Pugilist"), ("Western Thanalan", 13f, 11f)},
        {("Rotting Corpse", "Pugilist"), ("Eastern Thanalan", 15f, 16f)},
        {("Rotting Noble", "Pugilist"), ("Eastern Thanalan", 15f, 16f)},
        {("Overgrown Ivy", "Pugilist"), ("East Shroud", 23f, 29f)},
        {("Smallmouth Orobon", "Pugilist"), ("South Shroud", 16f, 18f)},
        {("Forest Yarzon", "Pugilist"), ("Upper La Noscea", 11f, 21f)},
        {("Coeurl Pup", "Pugilist"), ("Upper La Noscea", 9f, 20f)},
        {("Shroud Hare", "Pugilist"), ("North Shroud", 21f, 30f)},
        {("Bark Eft", "Pugilist"), ("South Shroud", 17f, 27f)},
        {("Fallen Mage", "Pugilist"), ("Southern Thanalan", 17f, 24f)},
        {("Ziz", "Pugilist"), ("North Shroud", 16f, 27f)},
        {("Corpse Brigade Knuckledancer", "Pugilist"), ("Southern Thanalan", 24f, 9f)},
        {("Clay Golem", "Pugilist"), ("North Shroud", 16f, 29f)},
        {("Coeurlclaw Hunter", "Pugilist"), ("South Shroud", 28f, 22f)},
        {("Lindwurm", "Pugilist"), ("Central Shroud", 13f, 19f)},
        {("Bigmouth Orobon", "Pugilist"), ("South Shroud", 18f, 30f)},
        {("Apkallu", "Pugilist"), ("Eastern La Noscea", 28f, 35f)},
        {("Mamool Ja Breeder", "Pugilist"), ("Upper La Noscea", 33f, 26f)},
        {("Russet Yarzon", "Pugilist"), ("Southern Thanalan", 14f, 32f)},
        {("Smoke Bomb", "Pugilist"), ("Southern Thanalan", 19f, 34f)},
        {("Death Gaze", "Pugilist"), ("Central Shroud", 17f, 22f)},
        {("Jungle Coeurl", "Pugilist"), ("Eastern La Noscea", 17f, 28f)},
        {("Goobbue", "Pugilist"), ("Eastern La Noscea", 17f, 31f)},
        {("Basalt Golem", "Pugilist"), ("Outer La Noscea", 13f, 15f)},
        {("Velociraptor", "Pugilist"), ("Outer La Noscea", 19f, 15f)},
        {("Highland Goobbue", "Pugilist"), ("Coerthas Central Highlands", 23f, 29f)},
        {("Feral Croc", "Pugilist"), ("Coerthas Central Highlands", 25f, 21f)},
        {("Redhorn Ogre", "Pugilist"), ("Coerthas Central Highlands", 26f, 12f)},
        {("Ochu", "Pugilist"), ("East Shroud", 26f, 21f)},
        {("Molted Ziz", "Pugilist"), ("East Shroud", 25f, 24f)},
        {("Snowstorm Goobbue", "Pugilist"), ("Coerthas Central Highlands", 23f, 17f)},
        {("Quartz Doblyn", "Pugilist"), ("Eastern Thanalan", 31f, 26f)},
        {("Dead Man's Moan", "Pugilist"), ("Western La Noscea", 15f, 35f)},
        {("3rd Cohort Signifer", "Pugilist"), ("East Shroud", 32f, 20f)},
        {("Wild Hog", "Pugilist"), ("South Shroud", 29f, 24f)},
        {("Raging Harrier", "Pugilist"), ("Mor Dhona", 16f, 15f)},
        {("Biast", "Pugilist"), ("Coerthas Central Highlands", 11f, 29f)},
        {("Gigas Shramana", "Pugilist"), ("Mor Dhona", 27f, 11f)},
        {("Snow Wolf", "Pugilist"), ("Coerthas Central Highlands", 16f, 32f)},
        {("5th Cohort Hoplomachus", "Pugilist"), ("Mor Dhona", 12f, 12f)},
        {("Dreamtoad", "Pugilist"), ("East Shroud", 26f, 18f)},
        {("Hapalit", "Pugilist"), ("Mor Dhona", 31f, 5f)},
        {("Zahar'ak Battle Drake", "Pugilist"), ("Southern Thanalan", 29f, 19f)},
        {("Basilisk", "Pugilist"), ("Northern Thanalan", 22f, 24f)},
        {("Shelfclaw Reaver", "Pugilist"), ("Western La Noscea", 13f, 16f)},
        {("Wharf Rat", "Rogue"), ("Middle La Noscea", 21f, 23f)},
        {("Lost Lamb", "Rogue"), ("Middle La Noscea", 23f, 24f)},
        {("Aurelia", "Rogue"), ("Lower La Noscea", 24f, 26f)},
        {("Wild Dodo", "Rogue"), ("Lower La Noscea", 29f, 19f)},
        {("Pugil", "Rogue"), ("Middle La Noscea", 20f, 22f)},
        {("Goblin Fisher", "Rogue"), ("Middle La Noscea", 23f, 21f)},
        {("Tiny Mandragora", "Rogue"), ("Middle La Noscea", 22f, 18f)},
        {("Cave Bat", "Rogue"), ("Lower La Noscea", 25f, 15f)},
        {("Galago", "Rogue"), ("Lower La Noscea", 29f, 14f)},
        {("Grounded Pirate", "Rogue"), ("Middle La Noscea", 20f, 16f)},
        {("Grounded Raider", "Rogue"), ("Middle La Noscea", 20f, 17f)},
        {("Megalocrab", "Rogue"), ("Middle La Noscea", 14f, 13f)},
        {("Wild Jackal", "Rogue"), ("Lower La Noscea", 23f, 34f)},
        {("Roseling", "Rogue"), ("Western La Noscea", 34f, 30f)},
        {("Sewer Mole", "Rogue"), ("Western La Noscea", 33f, 28f)},
        {("Fat Dodo", "Rogue"), ("Western La Noscea", 32f, 27f)},
        {("Moraby Mole", "Rogue"), ("Lower La Noscea", 21f, 34f)},
        {("Qiqirn Eggdigger", "Rogue"), ("Lower La Noscea", 18f, 35f)},
        {("Puk Hatchling", "Rogue"), ("Western La Noscea", 28f, 22f)},
        {("Rothlyt Pelican", "Rogue"), ("Western La Noscea", 24f, 22f)},
        {("Killer Mantis", "Rogue"), ("Western La Noscea", 21f, 22f)},
        {("Hedgemole", "Rogue"), ("Western La Noscea", 26f, 23f)},
        {("Wild Wolf", "Rogue"), ("Upper La Noscea", 13f, 26f)},
        {("Bumble Beetle", "Rogue"), ("Upper La Noscea", 12f, 23f)},
        {("Black Bat", "Rogue"), ("East Shroud", 17f, 22f)},
        {("Gall Gnat", "Rogue"), ("East Shroud", 22f, 30f)},
        {("Overgrown Ivy", "Rogue"), ("East Shroud", 22f, 29f)},
        {("Bark Eft", "Rogue"), ("South Shroud", 18f, 24f)},
        {("Redbelly Lookout", "Rogue"), ("South Shroud", 23f, 18f)},
        {("Redbelly Larcener", "Rogue"), ("South Shroud", 23f, 18f)},
        {("Antelope Stag", "Rogue"), ("South Shroud", 26f, 20f)},
        {("River Yarzon", "Rogue"), ("South Shroud", 23f, 22f)},
        {("Corpse Brigade Knuckledancer", "Rogue"), ("Southern Thanalan", 24f, 11f)},
        {("Corpse Brigade Firedancer", "Rogue"), ("Southern Thanalan", 24f, 11f)},
        {("Coeurlclaw Hunter", "Rogue"), ("South Shroud", 28f, 21f)},
        {("Coeurlclaw Cutter", "Rogue"), ("South Shroud", 28f, 21f)},
        {("Sandstone Golem", "Rogue"), ("Southern Thanalan", 22f, 12f)},
        {("Large Buffalo", "Rogue"), ("Eastern La Noscea", 28f, 30f)},
        {("Grass Raptor", "Rogue"), ("Eastern La Noscea", 16f, 25f)},
        {("Qiqirn Gullroaster", "Rogue"), ("Eastern La Noscea", 27f, 33f)},
        {("Colibri", "Rogue"), ("Eastern La Noscea", 31f, 24f)},
        {("Coeurl", "Rogue"), ("Outer La Noscea", 14f, 14f)},
        {("Highland Condor", "Rogue"), ("Outer La Noscea", 15f, 17f)},
        {("Basalt Golem", "Rogue"), ("Outer La Noscea", 13f, 15f)},
        {("Velociraptor", "Rogue"), ("Outer La Noscea", 19f, 15f)},
        {("Feral Croc", "Rogue"), ("Coerthas Central Highlands", 25f, 20f)},
        {("Highland Goobbue", "Rogue"), ("Coerthas Central Highlands", 25f, 20f)},
        {("Redhorn Ogre", "Rogue"), ("Coerthas Central Highlands", 26f, 12f)},
        {("Taurus", "Rogue"), ("Coerthas Central Highlands", 32f, 12f)},
        {("Bateleur", "Rogue"), ("Coerthas Central Highlands", 16f, 19f)},
        {("Chinchilla", "Rogue"), ("Coerthas Central Highlands", 16f, 19f)},
        {("Golden Fleece", "Rogue"), ("Eastern Thanalan", 26f, 25f)},
        {("Quartz Doblyn", "Rogue"), ("Eastern Thanalan", 30f, 25f)},
        {("Nix", "Rogue"), ("Mor Dhona", 19f, 9f)},
        {("Mudpuppy", "Rogue"), ("Mor Dhona", 13f, 11f)},
        {("Daring Harrier", "Rogue"), ("Mor Dhona", 16f, 15f)},
        {("Raging Harrier", "Rogue"), ("Mor Dhona", 16f, 15f)},
        {("Gigas Shramana", "Rogue"), ("Mor Dhona", 27f, 11f)},
        {("Gigas Sozu", "Rogue"), ("Mor Dhona", 27f, 11f)},
        {("Hippogryph", "Rogue"), ("Mor Dhona", 27f, 8f)},
        {("Hapalit", "Rogue"), ("Mor Dhona", 31f, 5f)},
        {("2nd Cohort Eques", "Rogue"), ("Eastern La Noscea", 29f, 20f)},
        {("2nd Cohort Signifer", "Rogue"), ("Eastern La Noscea", 29f, 20f)},
        {("2nd Cohort Secutor", "Rogue"), ("Eastern La Noscea", 30f, 19f)},
        {("2nd Cohort Vanguard", "Rogue"), ("Eastern La Noscea", 30f, 19f)},
        {("Little Ladybug", "Thaumaturge"), ("Western Thanalan", 28f, 24f)},
        {("Huge Hornet", "Thaumaturge"), ("Central Thanalan", 22f, 27f)},
        {("Cactuar", "Thaumaturge"), ("Western Thanalan", 27f, 24f)},
        {("Snapping Shrew", "Thaumaturge"), ("Central Thanalan", 24f, 30f)},
        {("Syrphid Cloud", "Thaumaturge"), ("Central Thanalan", 18f, 21f)},
        {("Yarzon Feeder", "Thaumaturge"), ("Western Thanalan", 24f, 27f)},
        {("Rusty Coblyn", "Thaumaturge"), ("Western Thanalan", 20f, 28f)},
        {("Spriggan Graverobber", "Thaumaturge"), ("Central Thanalan", 18f, 23f)},
        {("Qiqirn Shellsweeper", "Thaumaturge"), ("Central Thanalan", 17f, 19f)},
        {("Sun Bat", "Thaumaturge"), ("Central Thanalan", 26f, 18f)},
        {("Copper Coblyn", "Thaumaturge"), ("Western Thanalan", 27f, 16f)},
        {("Bomb", "Thaumaturge"), ("Western Thanalan", 27f, 16f)},
        {("Cochineal Cactuar", "Thaumaturge"), ("Central Thanalan", 23f, 19f)},
        {("Quiveron Attendant", "Thaumaturge"), ("Central Thanalan", 23f, 20f)},
        {("Giant Tortoise", "Thaumaturge"), ("Western Thanalan", 21f, 26f)},
        {("Antling Sentry", "Thaumaturge"), ("Central Thanalan", 19f, 16f)},
        {("Thickshell", "Thaumaturge"), ("Western Thanalan", 16f, 16f)},
        {("Toxic Toad", "Thaumaturge"), ("Central Thanalan", 27f, 18f)},
        {("Tuco-tuco", "Thaumaturge"), ("Eastern Thanalan", 15f, 24f)},
        {("Myotragus Nanny", "Thaumaturge"), ("Eastern Thanalan", 18f, 22f)},
        {("Blowfly Swarm", "Thaumaturge"), ("Eastern Thanalan", 12f, 21f)},
        {("Rotting Corpse", "Thaumaturge"), ("Eastern Thanalan", 15f, 16f)},
        {("Bloated Bogy", "Thaumaturge"), ("Western Thanalan", 13f, 11f)},
        {("Kedtrap", "Thaumaturge"), ("South Shroud", 21f, 20f)},
        {("Overgrown Ivy", "Thaumaturge"), ("Upper Paths", 23f, 29f)},
        {("Yarzon Scavenger", "Thaumaturge"), ("Western Thanalan", 15f, 8f)},
        {("Forest Yarzon", "Thaumaturge"), ("Cape Westwind (area)", 11f, 21f)},
        {("Laughing Toad", "Thaumaturge"), ("Western Thanalan", 15f, 7f)},
        {("Bark Eft", "Thaumaturge"), ("The Footfalls", 17f, 24f)},
        {("Jumping Djigga", "Thaumaturge"), ("East Shroud", 15f, 21f)},
        {("Glowfly", "Thaumaturge"), ("The Bramble Patch", 15f, 21f)},
        {("River Yarzon", "Thaumaturge"), ("South Shroud", 23f, 22f)},
        {("Potter Wasp Swarm", "Thaumaturge"), ("Southern Thanalan", 20f, 16f)},
        {("Phurble", "Thaumaturge"), ("Eastern Thanalan", 23f, 20f)},
        {("Corpse Brigade Knuckledancer", "Thaumaturge"), ("Southern Thanalan", 24f, 9f)},
        {("Fire Sprite", "Thaumaturge"), ("Southern Thanalan", 13f, 20f)},
        {("Stroper", "Thaumaturge"), ("Central Shroud", 12f, 21f)},
        {("Adamantoise", "Thaumaturge"), ("South Shroud", 16f, 30f)},
        {("Mamool Ja Executioner", "Thaumaturge"), ("Upper La Noscea", 27f, 23f)},
        {("Revenant", "Thaumaturge"), ("Central Shroud", 12f, 20f)},
        {("Russet Yarzon", "Thaumaturge"), ("Southern Thanalan", 14f, 32f)},
        {("Smoke Bomb", "Thaumaturge"), ("Southern Thanalan", 19f, 35f)},
        {("Dung Midge Swarm", "Thaumaturge"), ("Eastern La Noscea", 18f, 28f)},
        {("Gigantoad", "Thaumaturge"), ("Eastern La Noscea", 18f, 26f)},
        {("Spriggan", "Thaumaturge"), ("Central Shroud", 11f, 16f)},
        {("Salamander", "Thaumaturge"), ("Upper La Noscea", 28f, 24f)},
        {("Plasmoid", "Thaumaturge"), ("Outer La Noscea", 25f, 18f)},
        {("Ice Sprite", "Thaumaturge"), ("Coerthas Central Highlands", 20f, 31f)},
        {("Feral Croc", "Thaumaturge"), ("Coerthas Central Highlands", 26f, 24f)},
        {("Will-o'-the-wisp", "Thaumaturge"), ("South Shroud", 23f, 24f)},
        {("Golden Fleece", "Thaumaturge"), ("Eastern Thanalan", 27f, 24f)},
        {("Oldgrowth Treant", "Thaumaturge"), ("East Shroud", 25f, 20f)},
        {("Dragonfly", "Thaumaturge"), ("Coerthas Central Highlands", 9f, 14f)},
        {("Crater Golem", "Thaumaturge"), ("Central Shroud", 10f, 18f)},
        {("Dead Man's Moan", "Thaumaturge"), ("Western La Noscea", 15f, 34f)},
        {("3rd Cohort Secutor", "Thaumaturge"), ("East Shroud", 32f, 20f)},
        {("Morbol", "Thaumaturge"), ("East Shroud", 23f, 21f)},
        {("Nix", "Thaumaturge"), ("Mor Dhona", 19f, 9f)},
        {("Lesser Kalong", "Thaumaturge"), ("South Shroud", 28f, 22f)},
        {("Gigas Sozu", "Thaumaturge"), ("Mor Dhona", 29f, 14f)},
        {("Giant Logger", "Thaumaturge"), ("Coerthas Central Highlands", 13f, 26f)},
        {("Iron Tortoise", "Thaumaturge"), ("Southern Thanalan", 19f, 23f)},
        {("Synthetic Doblyn", "Thaumaturge"), ("Outer La Noscea", 23f, 8f)},
        {("Ked", "Thaumaturge"), ("South Shroud", 32f, 24f)},
        {("4th Cohort Hoplomachus", "Thaumaturge"), ("Western Thanalan", 12f, 7f)},
        {("2nd Cohort Signifer", "Thaumaturge"), ("Eastern La Noscea", 27f, 21f)},
        {("Amalj'aa Hunter", "Immortal Flames"), ("Eastern Thanalan", 19f, 28f)},
        {("Firemane", "Immortal Flames"), ("Halatali", 11f, 13f)},
        {("Sylvan Sough", "Immortal Flames"), ("East Shroud", 19f, 21f)},
        {("Kobold Footman", "Immortal Flames"), ("Upper La Noscea", 11f, 22f)},
        {("Kobold Pickman", "Immortal Flames"), ("Upper La Noscea", 11f, 22f)},
        {("Amalj'aa Seer", "Immortal Flames"), ("Southern Thanalan", 20f, 15f)},
        {("Ixali Lightwing", "Immortal Flames"), ("North Shroud", 22f, 28f)},
        {("Ixali Boundwing", "Immortal Flames"), ("Coerthas Central Highlands", 32f, 27f)},
        {("Amalj'aa Halberdier", "Immortal Flames"), ("Southern Thanalan", 25f, 34f)},
        {("Kobold Missionary", "Immortal Flames"), ("Eastern La Noscea", 28f, 25f)},
        {("Kobold Sidesman", "Immortal Flames"), ("Upper La Noscea", 26f, 19f)},
        {("Kobold Quarryman", "Immortal Flames"), ("Outer La Noscea", 22f, 14f)},
        {("Sylvan Screech", "Immortal Flames"), ("East Shroud", 21f, 21f)},
        {("Shelfspine Sahagin", "Immortal Flames"), ("Western La Noscea", 19f, 21f)},
        {("Amalj'aa Archer", "Immortal Flames"), ("Southern Thanalan", 20f, 23f)},
        {("Ixali Windtalon", "Immortal Flames"), ("North Shroud", 20f, 20f)},
        {("U'Ghamaro Priest", "Immortal Flames"), ("Outer La Noscea", 23f, 8f)},
        {("Sapsa Shelfspine", "Immortal Flames"), ("Western La Noscea", 16f, 15f)},
        {("Zahar'ak Thaumaturge", "Immortal Flames"), ("Southern Thanalan", 32f, 18f)},
        {("Natalan Windtalon", "Immortal Flames"), ("Coerthas Central Highlands", 31f, 17f)},
        {("Natalan Boldwing", "Immortal Flames"), ("Coerthas Central Highlands", 31f, 17f)},
        {("Amalj'aa Hunter", "Maelstrom"), ("Eastern Thanalan", 19f, 28f)},
        {("Sylvan Groan", "Maelstrom"), ("East Shroud", 19f, 21f)},
        {("Sylvan Sough", "Maelstrom"), ("East Shroud", 19f, 21f)},
        {("Kobold Pickman", "Maelstrom"), ("Upper La Noscea", 11f, 22f)},
        {("Amalj'aa Bruiser", "Maelstrom"), ("Southern Thanalan", 20f, 15f)},
        {("Ixali Straightbeak", "Maelstrom"), ("North Shroud", 23f, 28f)},
        {("Ixali Wildtalon", "Maelstrom"), ("Coerthas Central Highlands", 30f, 27f)},
        {("Amalj'aa Divinator", "Maelstrom"), ("Southern Thanalan", 27f, 34f)},
        {("Kobold Pitman", "Maelstrom"), ("Eastern La Noscea", 28f, 26f)},
        {("Kobold Bedesman", "Maelstrom"), ("Outer La Noscea", 22f, 13f)},
        {("Kobold Priest", "Maelstrom"), ("Outer La Noscea", 22f, 12f)},
        {("Sylvan Sigh", "Maelstrom"), ("East Shroud", 23f, 21f)},
        {("Shelfscale Sahagin", "Maelstrom"), ("Western La Noscea", 18f, 21f)},
        {("Amalj'aa Pugilist", "Maelstrom"), ("Southern Thanalan", 20f, 23f)},
        {("Ixali Boldwing", "Maelstrom"), ("North Shroud", 21f, 20f)},
        {("Sylpheed Screech", "Maelstrom"), ("East Shroud", 27f, 19f)},
        {("U'Ghamaro Bedesman", "Maelstrom"), ("Outer La Noscea", 22f, 6f)},
        {("Trenchtooth Sahagin", "Maelstrom"), ("Western La Noscea", 20f, 20f)},
        {("Sapsa Shelfclaw", "Maelstrom"), ("Western La Noscea", 18f, 16f)},
        {("Zahar'ak Archer", "Maelstrom"), ("Southern Thanalan", 29f, 20f)},
        {("Natalan Fogcaller", "Maelstrom"), ("Coerthas Central Highlands", 32f, 18f)},
        {("Natalan Boldwing", "Maelstrom"), ("Coerthas Central Highlands", 31f, 17f)},
        {("Amalj'aa Javelinier", "Order of the Twin Adder"), ("Eastern Thanalan", 19f, 27f)},
        {("Sylvan Scream", "Order of the Twin Adder"), ("East Shroud", 19f, 21f)},
        {("Kobold Pickman", "Order of the Twin Adder"), ("Upper La Noscea", 13f, 22f)},
        {("Amalj'aa Bruiser", "Order of the Twin Adder"), ("Southern Thanalan", 21f, 14f)},
        {("Ixali Deftalon", "Order of the Twin Adder"), ("North Shroud", 22f, 28f)},
        {("Amalj'aa Ranger", "Order of the Twin Adder"), ("Eastern Thanalan", 24f, 20f)},
        {("Ixali Fearcaller", "Order of the Twin Adder"), ("Coerthas Central Highlands", 31f, 28f)},
        {("Amalj'aa Sniper", "Order of the Twin Adder"), ("Southern Thanalan", 26f, 34f)},
        {("Kobold Missionary", "Order of the Twin Adder"), ("Eastern La Noscea", 28f, 26f)},
        {("Kobold Sidesman", "Order of the Twin Adder"), ("Upper La Noscea", 26f, 19f)},
        {("Kobold Roundsman", "Order of the Twin Adder"), ("Outer La Noscea", 22f, 14f)},
        {("Sylvan Snarl", "Order of the Twin Adder"), ("East Shroud", 23f, 20f)},
        {("Shelfclaw Sahagin", "Order of the Twin Adder"), ("Western La Noscea", 18f, 21f)},
        {("Amalj'aa Lancer", "Order of the Twin Adder"), ("Southern Thanalan", 22f, 21f)},
        {("U'Ghamaro Roundsman", "Order of the Twin Adder"), ("Outer La Noscea", 23f, 9f)},
        {("Ixali Windtalon", "Order of the Twin Adder"), ("North Shroud", 20f, 20f)},
        {("Sylpheed Snarl", "Order of the Twin Adder"), ("East Shroud", 27f, 18f)},
        {("U'Ghamaro Quarryman", "Order of the Twin Adder"), ("Outer La Noscea", 23f, 8f)},
        {("Sapsa Shelftooth", "Order of the Twin Adder"), ("Western La Noscea", 17f, 15f)},
        {("Zahar'ak Pugilist", "Order of the Twin Adder"), ("Southern Thanalan", 28f, 20f)},
        {("Natalan Swiftbeak", "Order of the Twin Adder"), ("Coerthas Central Highlands", 31f, 17f)},
        {("Natalan Boldwing", "Order of the Twin Adder"), ("Coerthas Central Highlands", 31f, 17f)},

    };

}

internal class TerritoryDetail
{
    public uint TerritoryType { get; set; }
    public uint MapId { get; set; }
    public ushort SizeFactor { get; set; }
    public string Name { get; set; }
}
