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
}