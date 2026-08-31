using UnityEngine;

public enum PlantTier
{
    Beginner,
    Medium,
    Hard
}

public enum WateringRule
{
    OncePerDay,
    MorningAndEvening,
    StrictTimeWindow
}

[CreateAssetMenu(fileName = "New Plant Data", menuName = "Farming/Plant Data")]
public class PlantData : ScriptableObject
{
    [Header("Identity")]
    public SeedType seedType;
    public string displayName;
    public PlantTier tier;

    [Header("Watering Rule")]
    public WateringRule wateringRule;

    [Header("Strict Time Window (Hard tier only)")]
    [Tooltip("Target hour in 24 hour format, e.g. 6 for 6 AM.")]
    public int strictWindowHour = 6;
    public int strictWindowMinute = 0;
    [Tooltip("Minutes of tolerance around the target time that still count as a valid watering.")]
    public int strictWindowToleranceMinutes = 30;

    [Header("Growth")]
    [Tooltip("Minimum in-game days of growth before this plant can be harvested.")]
    public int minHarvestDays = 3;
    [Tooltip("In-game day at which this plant reaches full quality.")]
    public int maxHarvestDays = 5;

    [Header("Grace Period (real-world time)")]
    [Tooltip("Real-world hours after a missed watering before Thirsty becomes Critical.")]
    public float thirstyToCriticalHours = 1.5f;
    [Tooltip("Real-world hours after entering Critical before the plant dies.")]
    public float criticalToDeadHours = 0.5f;

    [Header("Economy")]
    [Tooltip("Coins required to buy one seed of this type from the shop.")]
    public int seedBuyPrice = 10;
    [Tooltip("Coins earned for selling one harvested item of this type at full quality.")]
    public int sellPricePerItem = 15;
    [Tooltip("Multiplier applied to sellPricePerItem when the item survived Critical health before recovering (0.5 = half price).")]
    [Range(0f, 1f)]
    public float reducedQualitySellMultiplier = 0.5f;

    [Header("Book Notes")]
    [Tooltip("Flavor text shown on this plant's page in the book. Leave empty to auto-generate a default sentence based on tier.")]
    [TextArea]
    public string notes;

    [Header("Visuals")]
    [Tooltip("Optional icon shown on this plant's shop card (and usable elsewhere later). Leave empty to show a plain tier-colored placeholder box instead -- no icon asset is required for a new seed to work.")]
    public Sprite icon;

    [Header("Growth Visual")]
    [Tooltip("Growth-stage models shown on the plot as the plant matures, ordered from just-planted (index 0) to fully grown/ready-to-harvest (last index). GroundPlot swaps between these based on days-since-planted, and always shows the last entry once the plant is ready to harvest. Needs at least 1 entry; leave empty to show no plant model (just the plot tile's own color tint).")]
    public GameObject[] growthStagePrefabs;
    [Tooltip("Optional model swapped in when this plant dies from neglect (a wilted look). Leave empty to just rely on the plot tile's dark tint.")]
    public GameObject deadStagePrefab;

    [Header("Growth Visual Ground-Fix (optional)")]
    [Tooltip("Per-stage vertical correction (world units, applied AFTER centering) for growth-stage models whose imported mesh pivot isn't at the base of the plant -- some asset-pack prefabs have their pivot somewhere in the middle of the model instead of at ground level, which makes them render sunken into (or floating above) the plot tile. Leave empty/all-zero for prefabs that already sit correctly; only fill in an entry for a stage that needs correcting. Parallel to growthStagePrefabs -- index i here corrects growthStagePrefabs[i]. Safe to leave shorter than growthStagePrefabs (missing entries are treated as 0).")]
    public float[] growthStageYOffsets;
    [Tooltip("Same idea as growthStageYOffsets, but for deadStagePrefab specifically.")]
    public float deadStageYOffset;

    // Returns the configured Y correction for growthStagePrefabs[index], or 0 if none was set
    // (covers a null/short array so adding new growth stages later never throws an out-of-range
    // error just because nobody filled in a matching offset entry).
    public float GetGrowthStageYOffset(int index)
    {
        if (growthStageYOffsets == null || index < 0 || index >= growthStageYOffsets.Length)
        {
            return 0f;
        }
        return growthStageYOffsets[index];
    }
}