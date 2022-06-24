using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;
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
    public override string Description => "Allow clicking on hunting log targets (including GC logs) to generate map markers. The hunting log has no connection to the Hunts you unlock later!";

    private string _category { get; set; }
    private string _difficulty { get; set; }

    private delegate byte EventHandle(AtkUnitBase* atkUnitBase, AtkEventType eventType, uint eventParam, AtkEvent* atkEvent, void* a5);
    private delegate void* SetupHandle(AtkUnitBase* atkUnitBase, void* param_2, void* param_3);
    private HookWrapper<EventHandle> eventHook;
    private HookWrapper<SetupHandle> setupHook;
    private SeString pukHatchName;

    public override void Enable()
    {
        var eventSigMaybe = "4C 8B ?? 55 56 41 ?? 48 8B ?? 48 81 ?? ?? ?? ?? ?? 0F";

        var listHandlerSetupSigMaybe = "?? 89 ?? ?? ?? ?? 89 ?? ?? ?? ?? 89 ?? ?? ?? 57 48 83 ?? ?? 8B C2 BF";

        eventHook ??= Common.Hook<EventHandle>(eventSigMaybe, EventDetour);
        eventHook.Enable();

        setupHook ??= Common.Hook<SetupHandle>(listHandlerSetupSigMaybe, SetupDetour);
        setupHook.Enable();

        pukHatchName = Service.Data.GetExcelSheet<BNpcName>().GetRow(401).Singular;

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
        WalkEventList(atkUnitBase, eventType, eventParam, atkEvent, a5, depth);

        return eventHook.Original(atkUnitBase, eventType, eventParam, atkEvent, a5);
    }

    private void WalkEventList(AtkUnitBase* atkUnitBase, AtkEventType eventType, uint eventParam, AtkEvent* atkEvent, void* a5, int depth)
    {
        string indent = new('\t', depth); //gives us n tab characters to make debugging recursive logs easier (also it's pretty)
        try
        {
            if (eventType == AtkEventType.MouseClick)
            {
                SimpleLog.Debug(indent + $"click with param {eventParam}; matches our magic number: {(eventParam & 0x10F2C000) == 0x10F2C000}");

                if ((eventParam & 0x10F2C000) == 0x10F2C000)
                {
                    SimpleLog.Debug(indent + "we're in it now");
                    ReallyDoTheThing(atkEvent);
                    return;
                }
                AtkEvent* nextEvent = atkEvent->NextEvent;
                if (nextEvent is not null)
                {
                    WalkEventList(atkUnitBase, eventType, nextEvent->Param, nextEvent, a5, depth + 1);
                }
                SimpleLog.Debug(indent + "done at this level");
            }
        }
        catch (Exception ex)
        {
            SimpleLog.Error(indent + ex);
        }
    }

    private void ReallyDoTheThing(AtkEvent* atkEvent)
    {
        AtkCollisionNode* colNode = (AtkCollisionNode*)atkEvent->Target;
        // do the actual stuff!

        string mobName = colNode->AtkResNode.ParentNode->GetComponent()->UldManager.SearchNodeById(4)->GetAsAtkTextNode()->NodeText.ToString();
        var mobBNPCName = Service.Data.GetExcelSheet<BNpcName>().Where(x => x.Singular.ToString().Equals(mobName, StringComparison.CurrentCultureIgnoreCase)).First();

        // so! it turns out there is a single case where the 'same' mob name gets used twice for a single class
        // it's actually a completely separate mob! the two do not count towards each others' logs
        // there are loads of cases where there are multiple instances of what seems to be the 'same' mob,
        // in different places and with different levels but they count equally for whichever log you're on
        // those are all cross-class though!
        // this is an ugly, simple solution that (hopefully) works?
        if (mobBNPCName.RowId.Equals(401) && _difficulty.Equals("2"))
        {
            // do something complicated here i guess????
            mobBNPCName = Service.Data.GetExcelSheet<BNpcName>().GetRow(402);
        }

        var realCategory = FindCategory(_category);

        var foundIt = coordsDict.TryGetValue((mobBNPCName.RowId, realCategory), out var location);

        if (foundIt)
            PutItOnTheMap(location);
        else
            SimpleLog.Log($"tried to look up a missing mob: {mobName}, {_category}. It's probably in a dungeon.");

        return;
    }

    enum ClassOrCompany
    {
        Class,
        Company
    }

    private (uint, ClassOrCompany) FindCategory(string category)
    {
        (uint, ClassOrCompany) realCategory;

        var matchingClass = Service.Data.GetExcelSheet<ClassJob>().SingleOrDefault(r => r.Name.ToString().Equals(category, StringComparison.OrdinalIgnoreCase));
        var matchingGC = Service.Data.GetExcelSheet<GrandCompany>().SingleOrDefault(r => r.Name.ToString().Equals(category, StringComparison.OrdinalIgnoreCase));

        if (matchingClass is not null) realCategory = (matchingClass.RowId, ClassOrCompany.Class);
        else if (matchingGC is not null) realCategory = (matchingGC.RowId, ClassOrCompany.Company);
        else throw new Exception();

        return realCategory;
    }

    // 'borrowed' shamefully from chat coords plugin
    private static void PutItOnTheMap(((uint territoryType, uint mapId), float x, float y) location)
    {
        SimpleLog.Debug($"handling coords for {location}");

        var mapLink = new MapLinkPayload(
                location.Item1.territoryType,
                location.Item1.mapId,
                location.x,
                location.y
            );

        Service.GameGui.OpenMapWithMapLink(mapLink);
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

    private static readonly Dictionary<(uint nameId, (uint categoryId, ClassOrCompany)), ((uint ttID, uint mapID), float x, float y)> coordsDict = new()
    {
        {(49, (26, ClassOrCompany.Class)), ((134, 15), 22f, 24f)},
        {(417, (26, ClassOrCompany.Class)), ((134, 15), 21f, 23f)},
        {(392, (26, ClassOrCompany.Class)), ((134, 15), 23f, 24f)},
        {(115, (26, ClassOrCompany.Class)), ((135, 16), 28f, 20f)},
        {(401, (26, ClassOrCompany.Class)), ((134, 15), 20f, 19f)},
        {(299, (26, ClassOrCompany.Class)), ((135, 16), 31f, 16f)},
        {(404, (26, ClassOrCompany.Class)), ((134, 15), 20f, 19f)},
        {(364, (26, ClassOrCompany.Class)), ((135, 16), 25f, 15f)},
        {(408, (26, ClassOrCompany.Class)), ((135, 16), 29f, 14f)},
        {(421, (26, ClassOrCompany.Class)), ((134, 15), 20f, 16f)},
        {(117, (26, ClassOrCompany.Class)), ((135, 16), 20f, 32f)},
        {(410, (26, ClassOrCompany.Class)), ((138, 18), 33f, 28f)},
        {(354, (26, ClassOrCompany.Class)), ((134, 15), 21f, 23f)},
        {(394, (26, ClassOrCompany.Class)), ((138, 18), 32f, 27f)},
        {(13, (26, ClassOrCompany.Class)), ((138, 18), 30f, 29f)},
        {(350, (26, ClassOrCompany.Class)), ((135, 16), 18f, 35f)},
        {(363, (26, ClassOrCompany.Class)), ((138, 18), 28f, 22f)},
        {(402, (26, ClassOrCompany.Class)), ((138, 18), 28f, 22f)},
        {(403, (26, ClassOrCompany.Class)), ((138, 18), 26f, 23f)},
        {(1181, (26, ClassOrCompany.Class)), ((138, 18), 24f, 22f)},
        {(644, (26, ClassOrCompany.Class)), ((138, 18), 22f, 20f)},
        {(296, (26, ClassOrCompany.Class)), ((139, 19), 12f, 23f)},
        {(214, (26, ClassOrCompany.Class)), ((152, 5), 23f, 29f)},
        {(278, (26, ClassOrCompany.Class)), ((140, 20), 13f, 10f)},
        {(23, (26, ClassOrCompany.Class)), ((153, 6), 21f, 21f)},
        {(28, (26, ClassOrCompany.Class)), ((139, 19), 9f, 20f)},
        {(4, (26, ClassOrCompany.Class)), ((153, 6), 22f, 20f)},
        {(17, (26, ClassOrCompany.Class)), ((154, 7), 17f, 26f)},
        {(301, (26, ClassOrCompany.Class)), ((145, 22), 22f, 20f)},
        {(281, (26, ClassOrCompany.Class)), ((145, 22), 25f, 18f)},
        {(30, (26, ClassOrCompany.Class)), ((154, 7), 19f, 28f)},
        {(280, (26, ClassOrCompany.Class)), ((146, 23), 22f, 11f)},
        {(221, (26, ClassOrCompany.Class)), ((148, 4), 15f, 20f)},
        {(130, (26, ClassOrCompany.Class)), ((148, 4), 13f, 19f)},
        {(351, (26, ClassOrCompany.Class)), ((137, 17), 26f, 32f)},
        {(411, (26, ClassOrCompany.Class)), ((137, 17), 16f, 25f)},
        {(26, (26, ClassOrCompany.Class)), ((137, 17), 18f, 26f)},
        {(264, (26, ClassOrCompany.Class)), ((146, 23), 25f, 38f)},
        {(639, (26, ClassOrCompany.Class)), ((137, 17), 30f, 24f)},
        {(106, (26, ClassOrCompany.Class)), ((180, 30), 14f, 14f)},
        {(355, (26, ClassOrCompany.Class)), ((137, 17), 17f, 32f)},
        {(659, (26, ClassOrCompany.Class)), ((155, 53), 21f, 29f)},
        {(784, (26, ClassOrCompany.Class)), ((155, 53), 27f, 24f)},
        {(25, (26, ClassOrCompany.Class)), ((154, 7), 23f, 25f)},
        {(1182, (26, ClassOrCompany.Class)), ((155, 53), 32f, 12f)},
        {(222, (26, ClassOrCompany.Class)), ((152, 5), 25f, 24f)},
        {(275, (26, ClassOrCompany.Class)), ((145, 22), 30f, 25f)},
        {(1853, (26, ClassOrCompany.Class)), ((138, 18), 12f, 36f)},
        {(58, (26, ClassOrCompany.Class)), ((152, 5), 29f, 20f)},
        {(27, (26, ClassOrCompany.Class)), ((156, 25), 19f, 9f)},
        {(645, (26, ClassOrCompany.Class)), ((156, 25), 14f, 10f)},
        {(15, (26, ClassOrCompany.Class)), ((153, 6), 29f, 24f)},
        {(174, (26, ClassOrCompany.Class)), ((154, 7), 20f, 18f)},
        {(1810, (26, ClassOrCompany.Class)), ((156, 25), 11f, 11f)},
        {(653, (26, ClassOrCompany.Class)), ((155, 53), 16f, 32f)},
        {(1846, (26, ClassOrCompany.Class)), ((155, 53), 31f, 18f)},
        {(1831, (26, ClassOrCompany.Class)), ((138, 18), 12f, 15f)},
        {(1841, (26, ClassOrCompany.Class)), ((146, 23), 31f, 19f)},
        {(1820, (26, ClassOrCompany.Class)), ((140, 20), 12f, 7f)},
        {(16, (5, ClassOrCompany.Class)), ((152, 5), 16f, 23f)},
        {(49, (5, ClassOrCompany.Class)), ((154, 7), 25f, 27f)},
        {(37, (5, ClassOrCompany.Class)), ((148, 4), 22f, 17f)},
        {(47, (5, ClassOrCompany.Class)), ((148, 4), 24f, 18f)},
        {(9, (5, ClassOrCompany.Class)), ((154, 7), 25f, 27f)},
        {(118, (5, ClassOrCompany.Class)), ((154, 7), 27f, 21f)},
        {(56, (5, ClassOrCompany.Class)), ((148, 4), 25f, 21f)},
        {(196, (5, ClassOrCompany.Class)), ((148, 4), 26f, 19f)},
        {(120, (5, ClassOrCompany.Class)), ((148, 4), 30f, 20f)},
        {(21, (5, ClassOrCompany.Class)), ((148, 4), 26f, 25f)},
        {(22, (5, ClassOrCompany.Class)), ((148, 4), 22f, 26f)},
        {(54, (5, ClassOrCompany.Class)), ((148, 4), 23f, 25f)},
        {(13, (5, ClassOrCompany.Class)), ((148, 4), 26f, 29f)},
        {(20, (5, ClassOrCompany.Class)), ((148, 4), 19f, 27f)},
        {(128, (5, ClassOrCompany.Class)), ((148, 4), 27f, 15f)},
        {(225, (5, ClassOrCompany.Class)), ((152, 5), 13f, 27f)},
        {(107, (5, ClassOrCompany.Class)), ((152, 5), 14f, 25f)},
        {(14, (5, ClassOrCompany.Class)), ((152, 5), 13f, 23f)},
        {(6, (5, ClassOrCompany.Class)), ((152, 5), 20f, 29f)},
        {(220, (5, ClassOrCompany.Class)), ((152, 5), 18f, 28f)},
        {(7, (5, ClassOrCompany.Class)), ((152, 5), 19f, 27f)},
        {(239, (5, ClassOrCompany.Class)), ((152, 5), 19f, 30f)},
        {(3, (5, ClassOrCompany.Class)), ((153, 6), 15f, 18f)},
        {(638, (5, ClassOrCompany.Class)), ((139, 19), 13f, 24f)},
        {(232, (5, ClassOrCompany.Class)), ((152, 5), 16f, 23f)},
        {(215, (5, ClassOrCompany.Class)), ((153, 6), 18f, 19f)},
        {(227, (5, ClassOrCompany.Class)), ((140, 20), 14f, 8f)},
        {(381, (5, ClassOrCompany.Class)), ((154, 7), 21f, 31f)},
        {(44, (5, ClassOrCompany.Class)), ((139, 19), 12f, 21f)},
        {(83, (5, ClassOrCompany.Class)), ((153, 6), 23f, 19f)},
        {(11, (5, ClassOrCompany.Class)), ((154, 7), 18f, 26f)},
        {(301, (5, ClassOrCompany.Class)), ((145, 22), 22f, 19f)},
        {(303, (5, ClassOrCompany.Class)), ((146, 23), 19f, 10f)},
        {(224, (5, ClassOrCompany.Class)), ((154, 7), 15f, 27f)},
        {(48, (5, ClassOrCompany.Class)), ((148, 4), 15f, 17f)},
        {(341, (5, ClassOrCompany.Class)), ((137, 17), 28f, 35f)},
        {(207, (5, ClassOrCompany.Class)), ((148, 4), 11f, 22f)},
        {(290, (5, ClassOrCompany.Class)), ((146, 23), 23f, 32f)},
        {(204, (5, ClassOrCompany.Class)), ((146, 23), 13f, 32f)},
        {(366, (5, ClassOrCompany.Class)), ((137, 17), 20f, 32f)},
        {(132, (5, ClassOrCompany.Class)), ((146, 23), 19f, 35f)},
        {(91, (5, ClassOrCompany.Class)), ((148, 4), 12f, 16f)},
        {(361, (5, ClassOrCompany.Class)), ((137, 17), 31f, 26f)},
        {(352, (5, ClassOrCompany.Class)), ((137, 17), 18f, 28f)},
        {(407, (5, ClassOrCompany.Class)), ((180, 30), 15f, 12f)},
        {(398, (5, ClassOrCompany.Class)), ((180, 30), 16f, 16f)},
        {(391, (5, ClassOrCompany.Class)), ((139, 19), 27f, 24f)},
        {(321, (5, ClassOrCompany.Class)), ((146, 23), 21f, 38f)},
        {(114, (5, ClassOrCompany.Class)), ((155, 53), 20f, 31f)},
        {(784, (5, ClassOrCompany.Class)), ((155, 53), 26f, 24f)},
        {(658, (5, ClassOrCompany.Class)), ((155, 53), 27f, 14f)},
        {(1850, (5, ClassOrCompany.Class)), ((155, 53), 3f, 21f)},
        {(790, (5, ClassOrCompany.Class)), ((155, 53), 9f, 20f)},
        {(637, (5, ClassOrCompany.Class)), ((155, 53), 9f, 14f)},
        {(233, (5, ClassOrCompany.Class)), ((152, 5), 25f, 20f)},
        {(1853, (5, ClassOrCompany.Class)), ((138, 18), 12f, 36f)},
        {(1854, (5, ClassOrCompany.Class)), ((138, 18), 14f, 35f)},
        {(237, (5, ClassOrCompany.Class)), ((152, 5), 23f, 21f)},
        {(645, (5, ClassOrCompany.Class)), ((155, 53), 16f, 30f)},
        {(112, (5, ClassOrCompany.Class)), ((153, 6), 29f, 23f)},
        {(787, (5, ClassOrCompany.Class)), ((155, 53), 13f, 28f)},
        {(789, (5, ClassOrCompany.Class)), ((156, 25), 32f, 8f)},
        {(1812, (5, ClassOrCompany.Class)), ((156, 25), 11f, 13f)},
        {(337, (5, ClassOrCompany.Class)), ((146, 23), 21f, 19f)},
        {(567, (5, ClassOrCompany.Class)), ((152, 5), 26f, 16f)},
        {(162, (5, ClassOrCompany.Class)), ((152, 5), 23f, 13f)},
        {(242, (5, ClassOrCompany.Class)), ((147, 24), 24f, 22f)},
        {(559, (5, ClassOrCompany.Class)), ((138, 18), 13f, 16f)},
        {(49, (6, ClassOrCompany.Class)), ((148, 4), 23f, 17f)},
        {(37, (6, ClassOrCompany.Class)), ((148, 4), 21f, 16f)},
        {(47, (6, ClassOrCompany.Class)), ((148, 4), 25f, 18f)},
        {(9, (6, ClassOrCompany.Class)), ((154, 7), 24f, 27f)},
        {(43, (6, ClassOrCompany.Class)), ((148, 4), 24f, 21f)},
        {(56, (6, ClassOrCompany.Class)), ((148, 4), 24f, 21f)},
        {(118, (6, ClassOrCompany.Class)), ((154, 7), 26f, 21f)},
        {(32, (6, ClassOrCompany.Class)), ((154, 7), 26f, 22f)},
        {(41, (6, ClassOrCompany.Class)), ((148, 4), 27f, 24f)},
        {(12, (6, ClassOrCompany.Class)), ((152, 5), 14f, 27f)},
        {(39, (6, ClassOrCompany.Class)), ((148, 4), 25f, 29f)},
        {(13, (6, ClassOrCompany.Class)), ((148, 4), 26f, 29f)},
        {(225, (6, ClassOrCompany.Class)), ((152, 5), 13f, 27f)},
        {(129, (6, ClassOrCompany.Class)), ((145, 22), 24f, 29f)},
        {(107, (6, ClassOrCompany.Class)), ((152, 5), 14f, 26f)},
        {(36, (6, ClassOrCompany.Class)), ((152, 5), 20f, 28f)},
        {(220, (6, ClassOrCompany.Class)), ((152, 5), 19f, 28f)},
        {(7, (6, ClassOrCompany.Class)), ((152, 5), 19f, 25f)},
        {(241, (6, ClassOrCompany.Class)), ((152, 5), 19f, 30f)},
        {(219, (6, ClassOrCompany.Class)), ((153, 6), 15f, 17f)},
        {(38, (6, ClassOrCompany.Class)), ((152, 5), 16f, 21f)},
        {(638, (6, ClassOrCompany.Class)), ((139, 19), 13f, 24f)},
        {(217, (6, ClassOrCompany.Class)), ((140, 20), 14f, 7f)},
        {(232, (6, ClassOrCompany.Class)), ((152, 5), 16f, 23f)},
        {(278, (6, ClassOrCompany.Class)), ((140, 20), 13f, 10f)},
        {(228, (6, ClassOrCompany.Class)), ((153, 6), 17f, 24f)},
        {(211, (6, ClassOrCompany.Class)), ((152, 5), 15f, 20f)},
        {(4, (6, ClassOrCompany.Class)), ((153, 6), 22f, 19f)},
        {(286, (6, ClassOrCompany.Class)), ((146, 23), 15f, 14f)},
        {(268, (6, ClassOrCompany.Class)), ((145, 22), 24f, 23f)},
        {(50, (6, ClassOrCompany.Class)), ((153, 6), 28f, 20f)},
        {(48, (6, ClassOrCompany.Class)), ((148, 4), 14f, 17f)},
        {(341, (6, ClassOrCompany.Class)), ((137, 17), 28f, 35f)},
        {(130, (6, ClassOrCompany.Class)), ((148, 4), 12f, 19f)},
        {(26, (6, ClassOrCompany.Class)), ((137, 17), 18f, 26f)},
        {(235, (6, ClassOrCompany.Class)), ((153, 6), 18f, 30f)},
        {(416, (6, ClassOrCompany.Class)), ((139, 19), 28f, 23f)},
        {(290, (6, ClassOrCompany.Class)), ((146, 23), 28f, 25f)},
        {(236, (6, ClassOrCompany.Class)), ((148, 4), 12f, 20f)},
        {(361, (6, ClassOrCompany.Class)), ((137, 17), 31f, 26f)},
        {(795, (6, ClassOrCompany.Class)), ((155, 53), 25f, 19f)},
        {(170, (6, ClassOrCompany.Class)), ((153, 6), 25f, 21f)},
        {(25, (6, ClassOrCompany.Class)), ((154, 7), 22f, 23f)},
        {(1849, (6, ClassOrCompany.Class)), ((155, 53), 26f, 10f)},
        {(45, (6, ClassOrCompany.Class)), ((153, 6), 22f, 25f)},
        {(637, (6, ClassOrCompany.Class)), ((155, 53), 9f, 14f)},
        {(271, (6, ClassOrCompany.Class)), ((145, 22), 26f, 25f)},
        {(270, (6, ClassOrCompany.Class)), ((180, 30), 22f, 13f)},
        {(790, (6, ClassOrCompany.Class)), ((155, 53), 10f, 18f)},
        {(1853, (6, ClassOrCompany.Class)), ((138, 18), 12f, 36f)},
        {(1854, (6, ClassOrCompany.Class)), ((138, 18), 17f, 36f)},
        {(53, (6, ClassOrCompany.Class)), ((152, 5), 29f, 20f)},
        {(112, (6, ClassOrCompany.Class)), ((153, 6), 28f, 22f)},
        {(653, (6, ClassOrCompany.Class)), ((155, 53), 16f, 32f)},
        {(1811, (6, ClassOrCompany.Class)), ((156, 25), 12f, 17f)},
        {(360, (6, ClassOrCompany.Class)), ((138, 18), 14f, 17f)},
        {(166, (6, ClassOrCompany.Class)), ((152, 5), 26f, 13f)},
        {(242, (6, ClassOrCompany.Class)), ((147, 24), 24f, 20f)},
        {(1826, (6, ClassOrCompany.Class)), ((137, 17), 29f, 21f)},
        {(49, (1, ClassOrCompany.Class)), ((140, 20), 27f, 24f)},
        {(262, (1, ClassOrCompany.Class)), ((141, 21), 20f, 27f)},
        {(287, (1, ClassOrCompany.Class)), ((140, 20), 27f, 24f)},
        {(318, (1, ClassOrCompany.Class)), ((141, 21), 25f, 31f)},
        {(282, (1, ClassOrCompany.Class)), ((140, 20), 26f, 22f)},
        {(294, (1, ClassOrCompany.Class)), ((141, 21), 17f, 15f)},
        {(113, (1, ClassOrCompany.Class)), ((140, 20), 17f, 28f)},
        {(317, (1, ClassOrCompany.Class)), ((141, 21), 16f, 23f)},
        {(266, (1, ClassOrCompany.Class)), ((141, 21), 17f, 19f)},
        {(292, (1, ClassOrCompany.Class)), ((141, 21), 23f, 19f)},
        {(302, (1, ClassOrCompany.Class)), ((141, 21), 22f, 21f)},
        {(316, (1, ClassOrCompany.Class)), ((140, 20), 27f, 17f)},
        {(277, (1, ClassOrCompany.Class)), ((140, 20), 27f, 17f)},
        {(288, (1, ClassOrCompany.Class)), ((141, 21), 24f, 19f)},
        {(326, (1, ClassOrCompany.Class)), ((141, 21), 24f, 21f)},
        {(244, (1, ClassOrCompany.Class)), ((140, 20), 21f, 26f)},
        {(636, (1, ClassOrCompany.Class)), ((140, 20), 16f, 16f)},
        {(635, (1, ClassOrCompany.Class)), ((140, 20), 17f, 14f)},
        {(306, (1, ClassOrCompany.Class)), ((145, 22), 11f, 19f)},
        {(273, (1, ClassOrCompany.Class)), ((145, 22), 18f, 24f)},
        {(1198, (1, ClassOrCompany.Class)), ((145, 22), 14f, 18f)},
        {(322, (1, ClassOrCompany.Class)), ((145, 22), 14f, 16f)},
        {(309, (1, ClassOrCompany.Class)), ((145, 22), 13f, 11f)},
        {(638, (1, ClassOrCompany.Class)), ((139, 19), 14f, 24f)},
        {(23, (1, ClassOrCompany.Class)), ((139, 19), 21f, 20f)},
        {(278, (1, ClassOrCompany.Class)), ((140, 20), 13f, 10f)},
        {(215, (1, ClassOrCompany.Class)), ((153, 6), 18f, 19f)},
        {(28, (1, ClassOrCompany.Class)), ((139, 19), 9f, 20f)},
        {(17, (1, ClassOrCompany.Class)), ((154, 7), 17f, 26f)},
        {(286, (1, ClassOrCompany.Class)), ((146, 23), 14f, 13f)},
        {(268, (1, ClassOrCompany.Class)), ((145, 22), 24f, 23f)},
        {(50, (1, ClassOrCompany.Class)), ((153, 6), 27f, 20f)},
        {(169, (1, ClassOrCompany.Class)), ((153, 6), 28f, 21f)},
        {(341, (1, ClassOrCompany.Class)), ((137, 17), 28f, 36f)},
        {(62, (1, ClassOrCompany.Class)), ((137, 17), 15f, 18f)},
        {(207, (1, ClassOrCompany.Class)), ((148, 4), 10f, 23f)},
        {(415, (1, ClassOrCompany.Class)), ((148, 4), 27f, 23f)},
        {(643, (1, ClassOrCompany.Class)), ((139, 19), 29f, 24f)},
        {(34, (1, ClassOrCompany.Class)), ((139, 19), 17f, 30f)},
        {(290, (1, ClassOrCompany.Class)), ((146, 23), 17f, 33f)},
        {(55, (1, ClassOrCompany.Class)), ((148, 4), 16f, 21f)},
        {(412, (1, ClassOrCompany.Class)), ((180, 30), 19f, 15f)},
        {(324, (1, ClassOrCompany.Class)), ((146, 23), 20f, 37f)},
        {(659, (1, ClassOrCompany.Class)), ((155, 53), 21f, 29f)},
        {(24, (1, ClassOrCompany.Class)), ((153, 6), 22f, 26f)},
        {(658, (1, ClassOrCompany.Class)), ((155, 53), 27f, 14f)},
        {(790, (1, ClassOrCompany.Class)), ((155, 53), 8f, 20f)},
        {(270, (1, ClassOrCompany.Class)), ((155, 53), 22f, 13f)},
        {(1852, (1, ClassOrCompany.Class)), ((138, 18), 15f, 34f)},
        {(1853, (1, ClassOrCompany.Class)), ((138, 18), 12f, 36f)},
        {(233, (1, ClassOrCompany.Class)), ((152, 5), 26f, 20f)},
        {(59, (1, ClassOrCompany.Class)), ((152, 5), 29f, 21f)},
        {(1854, (1, ClassOrCompany.Class)), ((138, 18), 12f, 36f)},
        {(237, (1, ClassOrCompany.Class)), ((138, 18), 23f, 21f)},
        {(645, (1, ClassOrCompany.Class)), ((156, 25), 13f, 11f)},
        {(1851, (1, ClassOrCompany.Class)), ((156, 25), 27f, 13f)},
        {(786, (1, ClassOrCompany.Class)), ((155, 53), 13f, 27f)},
        {(339, (1, ClassOrCompany.Class)), ((146, 23), 22f, 19f)},
        {(29, (1, ClassOrCompany.Class)), ((154, 7), 22f, 19f)},
        {(304, (1, ClassOrCompany.Class)), ((147, 24), 25f, 21f)},
        {(649, (1, ClassOrCompany.Class)), ((156, 25), 33f, 15f)},
        {(1821, (1, ClassOrCompany.Class)), ((156, 25), 25f, 21f)},
        {(49, (4, ClassOrCompany.Class)), ((148, 4), 24f, 18f)},
        {(37, (4, ClassOrCompany.Class)), ((148, 4), 22f, 17f)},
        {(47, (4, ClassOrCompany.Class)), ((148, 4), 25f, 19f)},
        {(9, (4, ClassOrCompany.Class)), ((154, 7), 26f, 26f)},
        {(5, (4, ClassOrCompany.Class)), ((154, 7), 27f, 24f)},
        {(32, (4, ClassOrCompany.Class)), ((154, 7), 26f, 22f)},
        {(196, (4, ClassOrCompany.Class)), ((148, 4), 26f, 20f)},
        {(197, (4, ClassOrCompany.Class)), ((148, 4), 24f, 24f)},
        {(195, (4, ClassOrCompany.Class)), ((148, 4), 30f, 23f)},
        {(120, (4, ClassOrCompany.Class)), ((148, 4), 31f, 20f)},
        {(10, (4, ClassOrCompany.Class)), ((148, 4), 18f, 19f)},
        {(39, (4, ClassOrCompany.Class)), ((152, 5), 13f, 26f)},
        {(13, (4, ClassOrCompany.Class)), ((148, 4), 25f, 29f)},
        {(128, (4, ClassOrCompany.Class)), ((148, 4), 22f, 16f)},
        {(107, (4, ClassOrCompany.Class)), ((152, 5), 14f, 25f)},
        {(14, (4, ClassOrCompany.Class)), ((152, 5), 13f, 23f)},
        {(6, (4, ClassOrCompany.Class)), ((152, 5), 15f, 27f)},
        {(36, (4, ClassOrCompany.Class)), ((152, 5), 15f, 27f)},
        {(220, (4, ClassOrCompany.Class)), ((152, 5), 19f, 28f)},
        {(7, (4, ClassOrCompany.Class)), ((152, 5), 19f, 27f)},
        {(240, (4, ClassOrCompany.Class)), ((152, 5), 19f, 30f)},
        {(223, (4, ClassOrCompany.Class)), ((152, 5), 20f, 25f)},
        {(38, (4, ClassOrCompany.Class)), ((152, 5), 17f, 22f)},
        {(219, (4, ClassOrCompany.Class)), ((153, 6), 15f, 17f)},
        {(3, (4, ClassOrCompany.Class)), ((153, 6), 18f, 22f)},
        {(638, (4, ClassOrCompany.Class)), ((139, 19), 13f, 24f)},
        {(234, (4, ClassOrCompany.Class)), ((153, 6), 16f, 18f)},
        {(227, (4, ClassOrCompany.Class)), ((154, 7), 20f, 31f)},
        {(52, (4, ClassOrCompany.Class)), ((153, 6), 24f, 18f)},
        {(4, (4, ClassOrCompany.Class)), ((153, 6), 26f, 18f)},
        {(314, (4, ClassOrCompany.Class)), ((140, 20), 16f, 7f)},
        {(286, (4, ClassOrCompany.Class)), ((146, 23), 14f, 14f)},
        {(50, (4, ClassOrCompany.Class)), ((153, 6), 27f, 20f)},
        {(303, (4, ClassOrCompany.Class)), ((146, 23), 19f, 10f)},
        {(332, (4, ClassOrCompany.Class)), ((146, 23), 24f, 9f)},
        {(140, (4, ClassOrCompany.Class)), ((153, 6), 28f, 21f)},
        {(341, (4, ClassOrCompany.Class)), ((137, 17), 28f, 35f)},
        {(566, (4, ClassOrCompany.Class)), ((153, 6), 17f, 26f)},
        {(207, (4, ClassOrCompany.Class)), ((148, 4), 10f, 23f)},
        {(1313, (4, ClassOrCompany.Class)), ((137, 17), 28f, 30f)},
        {(132, (4, ClassOrCompany.Class)), ((146, 23), 19f, 34f)},
        {(264, (4, ClassOrCompany.Class)), ((146, 23), 25f, 38f)},
        {(91, (4, ClassOrCompany.Class)), ((148, 4), 10f, 16f)},
        {(365, (4, ClassOrCompany.Class)), ((180, 30), 13f, 15f)},
        {(407, (4, ClassOrCompany.Class)), ((180, 30), 15f, 11f)},
        {(795, (4, ClassOrCompany.Class)), ((155, 53), 25f, 19f)},
        {(112, (4, ClassOrCompany.Class)), ((153, 6), 22f, 24f)},
        {(659, (4, ClassOrCompany.Class)), ((155, 53), 21f, 29f)},
        {(25, (4, ClassOrCompany.Class)), ((154, 7), 22f, 23f)},
        {(1183, (4, ClassOrCompany.Class)), ((155, 53), 17f, 17f)},
        {(1849, (4, ClassOrCompany.Class)), ((155, 53), 27f, 10f)},
        {(634, (4, ClassOrCompany.Class)), ((145, 22), 26f, 24f)},
        {(637, (4, ClassOrCompany.Class)), ((155, 53), 9f, 14f)},
        {(1850, (4, ClassOrCompany.Class)), ((155, 53), 3f, 21f)},
        {(1854, (4, ClassOrCompany.Class)), ((138, 18), 15f, 35f)},
        {(61, (4, ClassOrCompany.Class)), ((152, 5), 32f, 20f)},
        {(237, (4, ClassOrCompany.Class)), ((152, 5), 23f, 21f)},
        {(15, (4, ClassOrCompany.Class)), ((153, 6), 29f, 24f)},
        {(650, (4, ClassOrCompany.Class)), ((156, 25), 16f, 15f)},
        {(1851, (4, ClassOrCompany.Class)), ((156, 25), 25f, 12f)},
        {(653, (4, ClassOrCompany.Class)), ((155, 53), 16f, 32f)},
        {(360, (4, ClassOrCompany.Class)), ((138, 18), 13f, 17f)},
        {(1814, (4, ClassOrCompany.Class)), ((156, 25), 10f, 13f)},
        {(1846, (4, ClassOrCompany.Class)), ((155, 53), 34f, 23f)},
        {(163, (4, ClassOrCompany.Class)), ((152, 5), 24f, 11f)},
        {(304, (4, ClassOrCompany.Class)), ((147, 24), 22f, 24f)},
        {(1823, (4, ClassOrCompany.Class)), ((137, 17), 30f, 21f)},
        {(49, (3, ClassOrCompany.Class)), ((134, 15), 23f, 24f)},
        {(417, (3, ClassOrCompany.Class)), ((134, 15), 21f, 23f)},
        {(563, (3, ClassOrCompany.Class)), ((135, 16), 24f, 26f)},
        {(395, (3, ClassOrCompany.Class)), ((134, 15), 19f, 18f)},
        {(393, (3, ClassOrCompany.Class)), ((135, 16), 29f, 19f)},
        {(405, (3, ClassOrCompany.Class)), ((134, 15), 22f, 18f)},
        {(404, (3, ClassOrCompany.Class)), ((134, 15), 20f, 19f)},
        {(358, (3, ClassOrCompany.Class)), ((134, 15), 18f, 17f)},
        {(418, (3, ClassOrCompany.Class)), ((134, 15), 20f, 17f)},
        {(561, (3, ClassOrCompany.Class)), ((134, 15), 14f, 13f)},
        {(129, (3, ClassOrCompany.Class)), ((135, 16), 26f, 37f)},
        {(354, (3, ClassOrCompany.Class)), ((135, 16), 22f, 24f)},
        {(394, (3, ClassOrCompany.Class)), ((138, 18), 32f, 27f)},
        {(409, (3, ClassOrCompany.Class)), ((135, 16), 21f, 34f)},
        {(350, (3, ClassOrCompany.Class)), ((135, 16), 18f, 35f)},
        {(420, (3, ClassOrCompany.Class)), ((138, 18), 33f, 27f)},
        {(363, (3, ClassOrCompany.Class)), ((138, 18), 28f, 24f)},
        {(401, (3, ClassOrCompany.Class)), ((138, 18), 28f, 24f)},
        {(403, (3, ClassOrCompany.Class)), ((138, 18), 26f, 23f)},
        {(1181, (3, ClassOrCompany.Class)), ((138, 18), 24f, 22f)},
        {(644, (3, ClassOrCompany.Class)), ((138, 18), 21f, 22f)},
        {(1180, (3, ClassOrCompany.Class)), ((139, 19), 13f, 26f)},
        {(638, (3, ClassOrCompany.Class)), ((139, 19), 13f, 24f)},
        {(232, (3, ClassOrCompany.Class)), ((152, 5), 17f, 23f)},
        {(227, (3, ClassOrCompany.Class)), ((140, 20), 15f, 7f)},
        {(172, (3, ClassOrCompany.Class)), ((153, 6), 23f, 19f)},
        {(40, (3, ClassOrCompany.Class)), ((154, 7), 17f, 27f)},
        {(286, (3, ClassOrCompany.Class)), ((146, 23), 14f, 14f)},
        {(17, (3, ClassOrCompany.Class)), ((154, 7), 17f, 26f)},
        {(272, (3, ClassOrCompany.Class)), ((145, 22), 19f, 28f)},
        {(303, (3, ClassOrCompany.Class)), ((146, 23), 22f, 21f)},
        {(281, (3, ClassOrCompany.Class)), ((145, 22), 27f, 17f)},
        {(48, (3, ClassOrCompany.Class)), ((148, 4), 14f, 17f)},
        {(207, (3, ClassOrCompany.Class)), ((148, 4), 10f, 23f)},
        {(238, (3, ClassOrCompany.Class)), ((153, 6), 19f, 28f)},
        {(34, (3, ClassOrCompany.Class)), ((153, 6), 15f, 29f)},
        {(132, (3, ClassOrCompany.Class)), ((146, 23), 19f, 34f)},
        {(411, (3, ClassOrCompany.Class)), ((137, 17), 15f, 26f)},
        {(560, (3, ClassOrCompany.Class)), ((137, 17), 31f, 35f)},
        {(361, (3, ClassOrCompany.Class)), ((137, 17), 31f, 26f)},
        {(352, (3, ClassOrCompany.Class)), ((137, 17), 19f, 28f)},
        {(659, (3, ClassOrCompany.Class)), ((155, 53), 21f, 29f)},
        {(794, (3, ClassOrCompany.Class)), ((155, 53), 24f, 13f)},
        {(795, (3, ClassOrCompany.Class)), ((155, 53), 25f, 19f)},
        {(1612, (3, ClassOrCompany.Class)), ((155, 53), 25f, 19f)},
        {(1849, (3, ClassOrCompany.Class)), ((155, 53), 26f, 10f)},
        {(1611, (3, ClassOrCompany.Class)), ((155, 53), 19f, 17f)},
        {(270, (3, ClassOrCompany.Class)), ((180, 30), 23f, 13f)},
        {(222, (3, ClassOrCompany.Class)), ((152, 5), 25f, 24f)},
        {(275, (3, ClassOrCompany.Class)), ((145, 22), 31f, 26f)},
        {(1854, (3, ClassOrCompany.Class)), ((138, 18), 15f, 35f)},
        {(237, (3, ClassOrCompany.Class)), ((138, 18), 23f, 21f)},
        {(131, (3, ClassOrCompany.Class)), ((148, 4), 11f, 17f)},
        {(15, (3, ClassOrCompany.Class)), ((153, 6), 29f, 24f)},
        {(788, (3, ClassOrCompany.Class)), ((155, 53), 16f, 30f)},
        {(1813, (3, ClassOrCompany.Class)), ((156, 25), 9f, 14f)},
        {(1836, (3, ClassOrCompany.Class)), ((180, 30), 23f, 8f)},
        {(174, (3, ClassOrCompany.Class)), ((180, 30), 20f, 20f)},
        {(243, (3, ClassOrCompany.Class)), ((154, 7), 19f, 23f)},
        {(165, (3, ClassOrCompany.Class)), ((152, 5), 23f, 16f)},
        {(1818, (3, ClassOrCompany.Class)), ((140, 20), 10f, 6f)},
        {(1822, (3, ClassOrCompany.Class)), ((137, 17), 28f, 21f)},
        {(632, (2, ClassOrCompany.Class)), ((141, 21), 21f, 26f)},
        {(262, (2, ClassOrCompany.Class)), ((141, 21), 20f, 27f)},
        {(287, (2, ClassOrCompany.Class)), ((140, 20), 27f, 24f)},
        {(318, (2, ClassOrCompany.Class)), ((141, 21), 23f, 27f)},
        {(308, (2, ClassOrCompany.Class)), ((141, 21), 21f, 24f)},
        {(299, (2, ClassOrCompany.Class)), ((140, 20), 21f, 25f)},
        {(317, (2, ClassOrCompany.Class)), ((141, 21), 17f, 23f)},
        {(283, (2, ClassOrCompany.Class)), ((140, 20), 18f, 26f)},
        {(265, (2, ClassOrCompany.Class)), ((140, 20), 22f, 22f)},
        {(289, (2, ClassOrCompany.Class)), ((141, 21), 23f, 18f)},
        {(298, (2, ClassOrCompany.Class)), ((140, 20), 17f, 15f)},
        {(305, (2, ClassOrCompany.Class)), ((140, 20), 24f, 20f)},
        {(316, (2, ClassOrCompany.Class)), ((140, 20), 27f, 16f)},
        {(288, (2, ClassOrCompany.Class)), ((141, 21), 25f, 21f)},
        {(293, (2, ClassOrCompany.Class)), ((141, 21), 16f, 14f)},
        {(244, (2, ClassOrCompany.Class)), ((140, 20), 28f, 25f)},
        {(13, (2, ClassOrCompany.Class)), ((140, 20), 17f, 16f)},
        {(635, (2, ClassOrCompany.Class)), ((140, 20), 17f, 14f)},
        {(636, (2, ClassOrCompany.Class)), ((140, 20), 16f, 16f)},
        {(306, (2, ClassOrCompany.Class)), ((145, 22), 15f, 20f)},
        {(274, (2, ClassOrCompany.Class)), ((145, 22), 18f, 22f)},
        {(1199, (2, ClassOrCompany.Class)), ((145, 22), 11f, 22f)},
        {(1198, (2, ClassOrCompany.Class)), ((145, 22), 14f, 18f)},
        {(309, (2, ClassOrCompany.Class)), ((140, 20), 13f, 11f)},
        {(319, (2, ClassOrCompany.Class)), ((145, 22), 15f, 16f)},
        {(322, (2, ClassOrCompany.Class)), ((145, 22), 15f, 16f)},
        {(214, (2, ClassOrCompany.Class)), ((152, 5), 23f, 29f)},
        {(234, (2, ClassOrCompany.Class)), ((153, 6), 16f, 18f)},
        {(381, (2, ClassOrCompany.Class)), ((139, 19), 11f, 21f)},
        {(28, (2, ClassOrCompany.Class)), ((139, 19), 9f, 20f)},
        {(40, (2, ClassOrCompany.Class)), ((154, 7), 21f, 30f)},
        {(228, (2, ClassOrCompany.Class)), ((153, 6), 17f, 27f)},
        {(323, (2, ClassOrCompany.Class)), ((146, 23), 17f, 24f)},
        {(224, (2, ClassOrCompany.Class)), ((154, 7), 16f, 27f)},
        {(331, (2, ClassOrCompany.Class)), ((146, 23), 24f, 9f)},
        {(30, (2, ClassOrCompany.Class)), ((154, 7), 16f, 29f)},
        {(139, (2, ClassOrCompany.Class)), ((153, 6), 28f, 22f)},
        {(130, (2, ClassOrCompany.Class)), ((148, 4), 13f, 19f)},
        {(235, (2, ClassOrCompany.Class)), ((153, 6), 18f, 30f)},
        {(341, (2, ClassOrCompany.Class)), ((137, 17), 28f, 35f)},
        {(414, (2, ClassOrCompany.Class)), ((139, 19), 33f, 26f)},
        {(204, (2, ClassOrCompany.Class)), ((146, 23), 14f, 32f)},
        {(132, (2, ClassOrCompany.Class)), ((146, 23), 19f, 34f)},
        {(55, (2, ClassOrCompany.Class)), ((148, 4), 17f, 22f)},
        {(352, (2, ClassOrCompany.Class)), ((137, 17), 17f, 28f)},
        {(353, (2, ClassOrCompany.Class)), ((137, 17), 17f, 31f)},
        {(365, (2, ClassOrCompany.Class)), ((180, 30), 13f, 15f)},
        {(412, (2, ClassOrCompany.Class)), ((180, 30), 19f, 15f)},
        {(1612, (2, ClassOrCompany.Class)), ((155, 53), 23f, 29f)},
        {(784, (2, ClassOrCompany.Class)), ((155, 53), 25f, 21f)},
        {(794, (2, ClassOrCompany.Class)), ((155, 53), 26f, 12f)},
        {(33, (2, ClassOrCompany.Class)), ((152, 5), 26f, 21f)},
        {(222, (2, ClassOrCompany.Class)), ((152, 5), 25f, 24f)},
        {(1611, (2, ClassOrCompany.Class)), ((155, 53), 23f, 17f)},
        {(275, (2, ClassOrCompany.Class)), ((145, 22), 31f, 26f)},
        {(1854, (2, ClassOrCompany.Class)), ((138, 18), 15f, 35f)},
        {(61, (2, ClassOrCompany.Class)), ((152, 5), 32f, 20f)},
        {(15, (2, ClassOrCompany.Class)), ((153, 6), 29f, 24f)},
        {(651, (2, ClassOrCompany.Class)), ((156, 25), 16f, 15f)},
        {(788, (2, ClassOrCompany.Class)), ((155, 53), 11f, 29f)},
        {(647, (2, ClassOrCompany.Class)), ((156, 25), 27f, 11f)},
        {(653, (2, ClassOrCompany.Class)), ((155, 53), 16f, 32f)},
        {(1809, (2, ClassOrCompany.Class)), ((156, 25), 12f, 12f)},
        {(164, (2, ClassOrCompany.Class)), ((152, 5), 26f, 18f)},
        {(793, (2, ClassOrCompany.Class)), ((156, 25), 31f, 5f)},
        {(1841, (2, ClassOrCompany.Class)), ((146, 23), 29f, 19f)},
        {(304, (2, ClassOrCompany.Class)), ((147, 24), 22f, 24f)},
        {(345, (2, ClassOrCompany.Class)), ((138, 18), 13f, 16f)},
        {(417, (29, ClassOrCompany.Class)), ((134, 15), 21f, 23f)},
        {(392, (29, ClassOrCompany.Class)), ((134, 15), 23f, 24f)},
        {(563, (29, ClassOrCompany.Class)), ((135, 16), 24f, 26f)},
        {(393, (29, ClassOrCompany.Class)), ((135, 16), 29f, 19f)},
        {(640, (29, ClassOrCompany.Class)), ((134, 15), 20f, 22f)},
        {(367, (29, ClassOrCompany.Class)), ((134, 15), 23f, 21f)},
        {(405, (29, ClassOrCompany.Class)), ((134, 15), 22f, 18f)},
        {(364, (29, ClassOrCompany.Class)), ((135, 16), 25f, 15f)},
        {(408, (29, ClassOrCompany.Class)), ((135, 16), 29f, 14f)},
        {(421, (29, ClassOrCompany.Class)), ((134, 15), 20f, 16f)},
        {(418, (29, ClassOrCompany.Class)), ((134, 15), 20f, 17f)},
        {(561, (29, ClassOrCompany.Class)), ((134, 15), 14f, 13f)},
        {(399, (29, ClassOrCompany.Class)), ((135, 16), 23f, 34f)},
        {(400, (29, ClassOrCompany.Class)), ((138, 18), 34f, 30f)},
        {(410, (29, ClassOrCompany.Class)), ((138, 18), 33f, 28f)},
        {(394, (29, ClassOrCompany.Class)), ((138, 18), 32f, 27f)},
        {(409, (29, ClassOrCompany.Class)), ((135, 16), 21f, 34f)},
        {(350, (29, ClassOrCompany.Class)), ((135, 16), 18f, 35f)},
        {(401, (29, ClassOrCompany.Class)), ((138, 18), 28f, 22f)},
        {(1181, (29, ClassOrCompany.Class)), ((138, 18), 24f, 22f)},
        {(644, (29, ClassOrCompany.Class)), ((138, 18), 21f, 22f)},
        {(403, (29, ClassOrCompany.Class)), ((138, 18), 26f, 23f)},
        {(1180, (29, ClassOrCompany.Class)), ((139, 19), 13f, 26f)},
        {(296, (29, ClassOrCompany.Class)), ((139, 19), 12f, 23f)},
        {(38, (29, ClassOrCompany.Class)), ((152, 5), 17f, 22f)},
        {(2157, (29, ClassOrCompany.Class)), ((152, 5), 22f, 30f)},
        {(214, (29, ClassOrCompany.Class)), ((152, 5), 22f, 29f)},
        {(228, (29, ClassOrCompany.Class)), ((153, 6), 18f, 24f)},
        {(52, (29, ClassOrCompany.Class)), ((153, 6), 23f, 18f)},
        {(172, (29, ClassOrCompany.Class)), ((153, 6), 23f, 18f)},
        {(4, (29, ClassOrCompany.Class)), ((153, 6), 26f, 20f)},
        {(226, (29, ClassOrCompany.Class)), ((153, 6), 23f, 22f)},
        {(331, (29, ClassOrCompany.Class)), ((146, 23), 24f, 11f)},
        {(332, (29, ClassOrCompany.Class)), ((146, 23), 24f, 11f)},
        {(139, (29, ClassOrCompany.Class)), ((153, 6), 28f, 21f)},
        {(169, (29, ClassOrCompany.Class)), ((153, 6), 28f, 21f)},
        {(280, (29, ClassOrCompany.Class)), ((146, 23), 22f, 12f)},
        {(1313, (29, ClassOrCompany.Class)), ((137, 17), 28f, 30f)},
        {(411, (29, ClassOrCompany.Class)), ((137, 17), 16f, 25f)},
        {(351, (29, ClassOrCompany.Class)), ((137, 17), 27f, 33f)},
        {(639, (29, ClassOrCompany.Class)), ((137, 17), 31f, 24f)},
        {(106, (29, ClassOrCompany.Class)), ((180, 30), 14f, 14f)},
        {(398, (29, ClassOrCompany.Class)), ((180, 30), 15f, 17f)},
        {(365, (29, ClassOrCompany.Class)), ((180, 30), 13f, 15f)},
        {(412, (29, ClassOrCompany.Class)), ((180, 30), 19f, 15f)},
        {(784, (29, ClassOrCompany.Class)), ((155, 53), 25f, 20f)},
        {(1612, (29, ClassOrCompany.Class)), ((155, 53), 25f, 20f)},
        {(794, (29, ClassOrCompany.Class)), ((155, 53), 26f, 12f)},
        {(1182, (29, ClassOrCompany.Class)), ((155, 53), 32f, 12f)},
        {(1183, (29, ClassOrCompany.Class)), ((155, 53), 16f, 19f)},
        {(2156, (29, ClassOrCompany.Class)), ((155, 53), 16f, 19f)},
        {(271, (29, ClassOrCompany.Class)), ((145, 22), 26f, 25f)},
        {(275, (29, ClassOrCompany.Class)), ((145, 22), 30f, 25f)},
        {(27, (29, ClassOrCompany.Class)), ((156, 25), 19f, 9f)},
        {(645, (29, ClassOrCompany.Class)), ((156, 25), 13f, 11f)},
        {(650, (29, ClassOrCompany.Class)), ((156, 25), 16f, 15f)},
        {(651, (29, ClassOrCompany.Class)), ((156, 25), 16f, 15f)},
        {(647, (29, ClassOrCompany.Class)), ((156, 25), 27f, 11f)},
        {(648, (29, ClassOrCompany.Class)), ((156, 25), 27f, 11f)},
        {(789, (29, ClassOrCompany.Class)), ((156, 25), 27f, 8f)},
        {(793, (29, ClassOrCompany.Class)), ((156, 25), 31f, 5f)},
        {(1823, (29, ClassOrCompany.Class)), ((137, 17), 29f, 20f)},
        {(1825, (29, ClassOrCompany.Class)), ((137, 17), 29f, 20f)},
        {(1824, (29, ClassOrCompany.Class)), ((137, 17), 30f, 19f)},
        {(1826, (29, ClassOrCompany.Class)), ((137, 17), 30f, 19f)},
        {(49, (7, ClassOrCompany.Class)), ((140, 20), 28f, 24f)},
        {(632, (7, ClassOrCompany.Class)), ((141, 21), 22f, 27f)},
        {(287, (7, ClassOrCompany.Class)), ((140, 20), 27f, 24f)},
        {(318, (7, ClassOrCompany.Class)), ((141, 21), 24f, 30f)},
        {(201, (7, ClassOrCompany.Class)), ((141, 21), 18f, 21f)},
        {(284, (7, ClassOrCompany.Class)), ((140, 20), 24f, 27f)},
        {(276, (7, ClassOrCompany.Class)), ((140, 20), 20f, 28f)},
        {(317, (7, ClassOrCompany.Class)), ((141, 21), 18f, 23f)},
        {(266, (7, ClassOrCompany.Class)), ((141, 21), 17f, 19f)},
        {(279, (7, ClassOrCompany.Class)), ((141, 21), 26f, 18f)},
        {(277, (7, ClassOrCompany.Class)), ((140, 20), 27f, 16f)},
        {(316, (7, ClassOrCompany.Class)), ((140, 20), 27f, 16f)},
        {(288, (7, ClassOrCompany.Class)), ((141, 21), 23f, 19f)},
        {(330, (7, ClassOrCompany.Class)), ((141, 21), 23f, 20f)},
        {(244, (7, ClassOrCompany.Class)), ((140, 20), 21f, 26f)},
        {(293, (7, ClassOrCompany.Class)), ((141, 21), 19f, 16f)},
        {(636, (7, ClassOrCompany.Class)), ((140, 20), 16f, 16f)},
        {(216, (7, ClassOrCompany.Class)), ((141, 21), 27f, 18f)},
        {(306, (7, ClassOrCompany.Class)), ((145, 22), 15f, 24f)},
        {(274, (7, ClassOrCompany.Class)), ((145, 22), 18f, 22f)},
        {(1199, (7, ClassOrCompany.Class)), ((145, 22), 12f, 21f)},
        {(319, (7, ClassOrCompany.Class)), ((145, 22), 15f, 16f)},
        {(309, (7, ClassOrCompany.Class)), ((140, 20), 13f, 11f)},
        {(23, (7, ClassOrCompany.Class)), ((153, 6), 21f, 20f)},
        {(214, (7, ClassOrCompany.Class)), ((153, 6), 23f, 29f)},
        {(227, (7, ClassOrCompany.Class)), ((140, 20), 15f, 8f)},
        {(381, (7, ClassOrCompany.Class)), ((140, 20), 11f, 21f)},
        {(217, (7, ClassOrCompany.Class)), ((140, 20), 15f, 7f)},
        {(228, (7, ClassOrCompany.Class)), ((140, 20), 17f, 24f)},
        {(44, (7, ClassOrCompany.Class)), ((152, 5), 15f, 21f)},
        {(211, (7, ClassOrCompany.Class)), ((152, 5), 15f, 21f)},
        {(226, (7, ClassOrCompany.Class)), ((153, 6), 23f, 22f)},
        {(564, (7, ClassOrCompany.Class)), ((146, 23), 20f, 16f)},
        {(272, (7, ClassOrCompany.Class)), ((145, 22), 23f, 20f)},
        {(331, (7, ClassOrCompany.Class)), ((146, 23), 24f, 9f)},
        {(116, (7, ClassOrCompany.Class)), ((146, 23), 13f, 20f)},
        {(238, (7, ClassOrCompany.Class)), ((148, 4), 12f, 21f)},
        {(34, (7, ClassOrCompany.Class)), ((153, 6), 16f, 30f)},
        {(413, (7, ClassOrCompany.Class)), ((139, 19), 27f, 23f)},
        {(236, (7, ClassOrCompany.Class)), ((148, 4), 12f, 20f)},
        {(204, (7, ClassOrCompany.Class)), ((146, 23), 14f, 32f)},
        {(132, (7, ClassOrCompany.Class)), ((146, 23), 19f, 35f)},
        {(396, (7, ClassOrCompany.Class)), ((137, 17), 18f, 28f)},
        {(26, (7, ClassOrCompany.Class)), ((137, 17), 18f, 26f)},
        {(91, (7, ClassOrCompany.Class)), ((148, 4), 11f, 16f)},
        {(391, (7, ClassOrCompany.Class)), ((139, 19), 28f, 24f)},
        {(46, (7, ClassOrCompany.Class)), ((180, 30), 25f, 18f)},
        {(114, (7, ClassOrCompany.Class)), ((155, 53), 20f, 31f)},
        {(784, (7, ClassOrCompany.Class)), ((155, 53), 26f, 24f)},
        {(45, (7, ClassOrCompany.Class)), ((153, 6), 23f, 24f)},
        {(271, (7, ClassOrCompany.Class)), ((145, 22), 27f, 24f)},
        {(233, (7, ClassOrCompany.Class)), ((152, 5), 25f, 20f)},
        {(637, (7, ClassOrCompany.Class)), ((155, 53), 9f, 14f)},
        {(131, (7, ClassOrCompany.Class)), ((148, 4), 10f, 18f)},
        {(1854, (7, ClassOrCompany.Class)), ((138, 18), 15f, 34f)},
        {(60, (7, ClassOrCompany.Class)), ((152, 5), 32f, 20f)},
        {(237, (7, ClassOrCompany.Class)), ((152, 5), 23f, 21f)},
        {(27, (7, ClassOrCompany.Class)), ((156, 25), 19f, 9f)},
        {(112, (7, ClassOrCompany.Class)), ((153, 6), 28f, 22f)},
        {(648, (7, ClassOrCompany.Class)), ((156, 25), 29f, 14f)},
        {(785, (7, ClassOrCompany.Class)), ((155, 53), 13f, 26f)},
        {(243, (7, ClassOrCompany.Class)), ((146, 23), 19f, 23f)},
        {(1836, (7, ClassOrCompany.Class)), ((180, 30), 23f, 8f)},
        {(8, (7, ClassOrCompany.Class)), ((153, 6), 32f, 24f)},
        {(1815, (7, ClassOrCompany.Class)), ((140, 20), 12f, 7f)},
        {(1825, (7, ClassOrCompany.Class)), ((137, 17), 27f, 21f)},
        {(250, (3, ClassOrCompany.Company)), ((145, 22), 19f, 28f)},
        {(230, (3, ClassOrCompany.Company)), ((152, 5), 19f, 21f)},
        {(380, (3, ClassOrCompany.Company)), ((139, 19), 11f, 22f)},
        {(370, (3, ClassOrCompany.Company)), ((139, 19), 11f, 22f)},
        {(259, (3, ClassOrCompany.Company)), ((146, 23), 20f, 15f)},
        {(208, (3, ClassOrCompany.Company)), ((154, 7), 22f, 28f)},
        {(662, (3, ClassOrCompany.Company)), ((155, 53), 32f, 27f)},
        {(248, (3, ClassOrCompany.Company)), ((146, 23), 25f, 34f)},
        {(373, (3, ClassOrCompany.Company)), ((137, 17), 28f, 25f)},
        {(376, (3, ClassOrCompany.Company)), ((139, 19), 26f, 19f)},
        {(562, (3, ClassOrCompany.Company)), ((180, 30), 22f, 14f)},
        {(64, (3, ClassOrCompany.Company)), ((152, 5), 21f, 21f)},
        {(389, (3, ClassOrCompany.Company)), ((138, 18), 19f, 21f)},
        {(249, (3, ClassOrCompany.Company)), ((146, 23), 20f, 23f)},
        {(436, (3, ClassOrCompany.Company)), ((154, 7), 20f, 20f)},
        {(1835, (3, ClassOrCompany.Company)), ((180, 30), 23f, 8f)},
        {(1829, (3, ClassOrCompany.Company)), ((138, 18), 16f, 15f)},
        {(1840, (3, ClassOrCompany.Company)), ((146, 23), 32f, 18f)},
        {(1842, (3, ClassOrCompany.Company)), ((155, 53), 31f, 17f)},
        {(1843, (3, ClassOrCompany.Company)), ((155, 53), 31f, 17f)},
        {(250, (1, ClassOrCompany.Company)), ((145, 22), 19f, 28f)},
        {(231, (1, ClassOrCompany.Company)), ((152, 5), 19f, 21f)},
        {(230, (1, ClassOrCompany.Company)), ((152, 5), 19f, 21f)},
        {(370, (1, ClassOrCompany.Company)), ((139, 19), 11f, 22f)},
        {(256, (1, ClassOrCompany.Company)), ((146, 23), 20f, 15f)},
        {(210, (1, ClassOrCompany.Company)), ((154, 7), 23f, 28f)},
        {(660, (1, ClassOrCompany.Company)), ((155, 53), 30f, 27f)},
        {(260, (1, ClassOrCompany.Company)), ((146, 23), 27f, 34f)},
        {(369, (1, ClassOrCompany.Company)), ((137, 17), 28f, 26f)},
        {(375, (1, ClassOrCompany.Company)), ((180, 30), 22f, 13f)},
        {(371, (1, ClassOrCompany.Company)), ((180, 30), 22f, 12f)},
        {(65, (1, ClassOrCompany.Company)), ((152, 5), 23f, 21f)},
        {(386, (1, ClassOrCompany.Company)), ((138, 18), 18f, 21f)},
        {(253, (1, ClassOrCompany.Company)), ((146, 23), 20f, 23f)},
        {(103, (1, ClassOrCompany.Company)), ((154, 7), 21f, 20f)},
        {(67, (1, ClassOrCompany.Company)), ((152, 5), 27f, 19f)},
        {(1834, (1, ClassOrCompany.Company)), ((180, 30), 22f, 6f)},
        {(565, (1, ClassOrCompany.Company)), ((138, 18), 20f, 20f)},
        {(1828, (1, ClassOrCompany.Company)), ((138, 18), 18f, 16f)},
        {(1838, (1, ClassOrCompany.Company)), ((146, 23), 29f, 20f)},
        {(1845, (1, ClassOrCompany.Company)), ((155, 53), 32f, 18f)},
        {(1843, (1, ClassOrCompany.Company)), ((155, 53), 31f, 17f)},
        {(247, (2, ClassOrCompany.Company)), ((145, 22), 19f, 27f)},
        {(229, (2, ClassOrCompany.Company)), ((152, 5), 19f, 21f)},
        {(370, (2, ClassOrCompany.Company)), ((139, 19), 13f, 22f)},
        {(256, (2, ClassOrCompany.Company)), ((146, 23), 21f, 14f)},
        {(209, (2, ClassOrCompany.Company)), ((154, 7), 22f, 28f)},
        {(251, (2, ClassOrCompany.Company)), ((145, 22), 24f, 20f)},
        {(663, (2, ClassOrCompany.Company)), ((155, 53), 31f, 28f)},
        {(252, (2, ClassOrCompany.Company)), ((146, 23), 26f, 34f)},
        {(373, (2, ClassOrCompany.Company)), ((137, 17), 28f, 26f)},
        {(376, (2, ClassOrCompany.Company)), ((139, 19), 26f, 19f)},
        {(377, (2, ClassOrCompany.Company)), ((180, 30), 22f, 14f)},
        {(66, (2, ClassOrCompany.Company)), ((152, 5), 23f, 20f)},
        {(384, (2, ClassOrCompany.Company)), ((138, 18), 18f, 21f)},
        {(245, (2, ClassOrCompany.Company)), ((146, 23), 22f, 21f)},
        {(1832, (2, ClassOrCompany.Company)), ((180, 30), 23f, 9f)},
        {(436, (2, ClassOrCompany.Company)), ((154, 7), 20f, 20f)},
        {(69, (2, ClassOrCompany.Company)), ((152, 5), 27f, 18f)},
        {(1833, (2, ClassOrCompany.Company)), ((180, 30), 23f, 8f)},
        {(1830, (2, ClassOrCompany.Company)), ((138, 18), 17f, 15f)},
        {(1839, (2, ClassOrCompany.Company)), ((146, 23), 28f, 20f)},
        {(1844, (2, ClassOrCompany.Company)), ((155, 53), 31f, 17f)},
        {(1843, (2, ClassOrCompany.Company)), ((155, 53), 31f, 17f)},

    };

}
