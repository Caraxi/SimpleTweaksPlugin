using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltipField;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakName("Show Collectable Rewards")]
[TweakDescription("Shows rewards when viewing a collectable delivery item.")]
[TweakAuthor("Orphis")]
[TweakAutoConfig]
public class CollectableRewards : TooltipTweaks.SubTweak
{
    public class Configs : TweakConfig
    {
        [TweakConfigOption("Colour scrip values", HelpText = "Colour the script values or spell out the scrip colour name for visually impaired people.")]
        public bool ColourScripValues = true;
    }

    // List of categories with collectables for each job
    private List<uint[]> RowIdToJob = [
        /* CRP */ [15, 23],
        /* BSM */ [16, 24],
        /* ARM */ [17, 25],
        /* GSM */ [18, 26],
        /* LTW */ [19, 27],
        /* WVR */ [20, 28],
        /* ALC */ [21, 29],
        /* CUL */ [22, 30],
        /* MIN */ [31],
        /* BTN */ [32],
        /* FSH */ [14],
        ];

    public Configs Config { get; private set; }

    private ConcurrentDictionary<uint, Lazy<CollectableCachedDetails>> CollectableCache = new();
    private List<long> experiencePerLevel = new();
    private List<string> jobShortName = new();

    private const ushort Green = 504;
    private const ushort Orange = 500;
    private const ushort Purple = 522;
    private const string headerText = "Collectable Delivery Rewards";

    class CollectableCachedDetails
    {
        public uint Id;
        public sbyte? JobTableIndex;
        public string JobName;
        public int LevelMin;
        public int LevelMax;
        public int ScripRewardType;
        public List<CollectableReward> Rewards;
    }

    class CollectableReward
    {
        public int QualityRequired;
        public int ScriptRewardCount;
        public long Experience;
    }

    protected override void Enable()
    {
        Config = LoadConfig<Configs>() ?? new Configs();
        base.Enable();
    }

    protected override void Disable()
    {
        SaveConfig(Config);
        base.Disable();
    }

    protected override void Setup()
    {
        // Cache the experience levels
        foreach (var paramGrow in Service.Data.Excel.GetSheet<ParamGrow>())
        {
            if (paramGrow.ExpToNext == 0) break;
            experiencePerLevel.Add(paramGrow.ExpToNext);
        }

        var craftingJobExpArrayIndex = Service.Data.Excel.GetSheet<ClassJob>()
            .Where(job => job.DohDolJobIndex >= 0 && job.ItemSoulCrystal.Row != 0)
            .ToDictionary(job => job.DohDolJobIndex, job => job.ExpArrayIndex);

        var gatheringJobExpArrayIndex = Service.Data.Excel.GetSheet<ClassJob>()
            .Where(job => job.DohDolJobIndex >= 0 && job.ItemSoulCrystal.Row == 0)
            .ToDictionary(job => job.DohDolJobIndex, job => job.ExpArrayIndex);

        jobShortName = Service.Data.Excel.GetSheet<ClassJob>()
            .Where(job => job.DohDolJobIndex >= 0)
            .Select(job => job.Abbreviation.ToString()).ToList();

        Dictionary<uint, (sbyte, string)> rowToJobInfo = new();

        // Cache the list of collectable items we support
        foreach (var collectable in Service.Data.Excel.GetSheet<CollectablesShopItem>())
        {
            if (collectable.Item.Row == 0)
                continue;

            if (!rowToJobInfo.TryGetValue(collectable.RowId, out var jobInfo))
            {
                var jobIds = RowIdToJob
                            .Select((ids, index) => (ids, index))
                            .Where(entry => entry.ids.Contains(collectable.RowId))
                            .Select(entry => (entry.index < 8
                                        ? craftingJobExpArrayIndex[(sbyte)entry.index]
                                        : gatheringJobExpArrayIndex[(sbyte)(entry.index - 8)],
                                        jobShortName[entry.index]));
                if (!jobIds.Any())
                    continue;
                jobInfo = jobIds.First();
                rowToJobInfo.Add(collectable.RowId, jobInfo);
            }

            // Only handle collectable delivery items for which we matched a job
            if (jobInfo.Item1 < 0)
                continue;

            CollectableCache.TryAdd(collectable.Item.Row, new Lazy<CollectableCachedDetails>(() =>
            {
                return new CollectableCachedDetails
                {
                    Id = collectable.Item.Row,
                    JobTableIndex = (sbyte)jobInfo.Item1,
                    JobName = jobInfo.Item2,
                    LevelMin = collectable.LevelMin,
                    LevelMax = collectable.LevelMax,
                    ScripRewardType = collectable.CollectablesShopRewardScrip.Value.Currency,
                    Rewards = [
                        new CollectableReward {
                            QualityRequired = collectable.CollectablesShopRefine.Value.LowCollectability,
                            ScriptRewardCount = collectable.CollectablesShopRewardScrip.Value.LowReward,
                            Experience = collectable.CollectablesShopRewardScrip.Value.ExpRatioLow
                                * experiencePerLevel[collectable.LevelMax] / 1000,
                        },
                        new CollectableReward {
                            QualityRequired = collectable.CollectablesShopRefine.Value.MidCollectability,
                            ScriptRewardCount = collectable.CollectablesShopRewardScrip.Value.MidReward,
                            Experience = collectable.CollectablesShopRewardScrip.Value.ExpRatioMid
                                * experiencePerLevel[collectable.LevelMax] / 1000,
                        },
                        new CollectableReward {
                            QualityRequired = collectable.CollectablesShopRefine.Value.HighCollectability,
                            ScriptRewardCount = collectable.CollectablesShopRewardScrip.Value.HighReward,
                            Experience = collectable.CollectablesShopRewardScrip.Value.ExpRatioHigh
                                * experiencePerLevel[collectable.LevelMax] / 1000,
                        }],
                };
            }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));
        }

        base.Setup();
    }

    public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData)
    {
        // Check if we have a known collectable item
        if (!CollectableCache.TryGetValue((uint)Item.ItemId, out var itemDetailsLazy))
        {
            return;
        }

        var itemDetails = itemDetailsLazy.Value;
        var seStr = GetTooltipString(stringArrayData, ItemDescription);
        if (seStr == null && seStr.Payloads.Count == 0)
        {
            return;
        }
        // Check if the description has already been updated, as the callback can be called multiple
        // times on the same tooltip.
        if (seStr.TextValue.Contains(headerText))
        {
            return;
        }

        var (scripType, scripUiColour) = itemDetails.ScripRewardType switch
        {
            2 or 4 => ("Purple", Purple),
            6 or 7 => ("Orange", Orange),
            _ => ("Unknown", (ushort)1),
        };
        if (!Config.ColourScripValues)
        {
            scripUiColour = 1;
        }
        if (itemDetails.JobTableIndex.HasValue)
        {
            int currentJobLevel = PlayerState.Instance()->ClassJobLevels[itemDetails.JobTableIndex.Value];
            int levelingCutoff = Math.Min(currentJobLevel - (currentJobLevel % 10), experiencePerLevel.Count - 11);
            if (itemDetails.LevelMax <= levelingCutoff)
            {
                foreach (var reward in itemDetails.Rewards)
                {
                    reward.Experience = 1000;
                }
            }
        }

        var description = new SeStringBuilder()
            .AddUiForeground($"\n\n{headerText}\n", Orange);

        if (Item.Container == (InventoryType)9999)
        {
            // Temporary container, means it could be from a link, crafting log, or collectable appraiser.
            // We show all the values for all reward levels.

            description.AddUiForeground($"      Level: ", Green)
                .AddText($"{itemDetails.LevelMin} - {itemDetails.LevelMax} ")
                .AddUiForeground($"{itemDetails.JobName}\n", Green);

            if (!Config.ColourScripValues)
            {
                description.AddUiForeground("      Scrip Colour: ", Green)
                .AddUiForeground($"{scripType}\n", scripUiColour);
            }

            AddGenericRewardToDescription(itemDetails.Rewards[0], itemDetails.Rewards[1].QualityRequired, scripType, scripUiColour, description);
            AddGenericRewardToDescription(itemDetails.Rewards[1], itemDetails.Rewards[2].QualityRequired, scripType, scripUiColour, description);
            AddGenericRewardToDescription(itemDetails.Rewards[2], null, scripType, scripUiColour, description);
        }
        else
        {
            // The container is a proper inventory type, a real item with a collectability.
            // We show only the relevant reward.

            if (Item.Spiritbond < itemDetails.Rewards[0].QualityRequired)
            {
                description.AddUiForeground($"      Minimum quality of {itemDetails.Rewards[0].QualityRequired} has not been reached.", 16);
            }
            else if (Item.Spiritbond < itemDetails.Rewards[1].QualityRequired)
            {
                AddItemRewardToDescription(itemDetails.Rewards[0], scripType, scripUiColour, description);
            }
            else if (Item.Spiritbond < itemDetails.Rewards[2].QualityRequired)
            {
                AddItemRewardToDescription(itemDetails.Rewards[1], scripType, scripUiColour, description);
            }
            else
            {
                AddItemRewardToDescription(itemDetails.Rewards[2], scripType, scripUiColour, description);
            }
        }
        seStr.Append(description.Build());

        try
        {
            SetTooltipString(stringArrayData, ItemDescription, seStr);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex);
            Plugin.Error(this, ex);
        }
    }

    private unsafe void AddGenericRewardToDescription(CollectableReward reward, int? nextQuality, string scriptType, ushort scripUiColour, SeStringBuilder description)
    {
        string maxQualityForReward = nextQuality != null ? (nextQuality - 1).ToString() : "Max";

        description.AddUiForeground("      Quality: ", Green)
            .AddText($"{reward.QualityRequired} - {maxQualityForReward}")
            .AddUiForeground("      Exp: ", Green)
            .AddText($"{reward.Experience:N0}")
            .AddUiForeground("      Scrips: ", Green)
            .AddUiForeground($"{reward.ScriptRewardCount}\n", scripUiColour);
    }

    private unsafe void AddItemRewardToDescription(CollectableReward reward, string scripType, ushort scripUiColour, SeStringBuilder description)
    {
        description.AddUiForeground("      Exp: ", Green)
            .AddText($"{reward.Experience:N0}\n");

        if (!Config.ColourScripValues)
        {
            description.AddUiForeground("      Scrip Colour: ", Green)
                .AddUiForeground($"{scripType}\n", scripUiColour);
        }
        description.AddUiForeground("      Scrips: ", Green)
            .AddUiForeground($"{reward.ScriptRewardCount}\n", scripUiColour);
    }
}
