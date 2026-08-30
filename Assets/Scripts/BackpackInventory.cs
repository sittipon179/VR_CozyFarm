using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct HarvestedItem
{
    public SeedType type;
    public bool reducedQuality;

    public HarvestedItem(SeedType type, bool reducedQuality)
    {
        this.type = type;
        this.reducedQuality = reducedQuality;
    }
}

public class BackpackInventory : MonoBehaviour
{
    public static BackpackInventory Instance { get; private set; }

    [Header("Capacity Tiers (slot count per tier)")]
    public int[] capacityTiers = new int[] { 12, 24, 48, 96 };

    [Header("Coin Cost To Reach Each Tier (index 0 = cost to go from tier 0 to tier 1)")]
    public int[] upgradeCosts = new int[] { 500, 2000, 8000 };

    private List<HarvestedItem> items = new List<HarvestedItem>();

    public int CurrentTierIndex { get; private set; } = 0;
    public int Capacity => capacityTiers[CurrentTierIndex];
    public int Count => items.Count;
    public bool IsFull => Count >= Capacity;
    public bool IsMaxTier => CurrentTierIndex >= capacityTiers.Length - 1;
    public int NextUpgradeCost => IsMaxTier ? -1 : upgradeCosts[CurrentTierIndex];
    public int NextTierCapacity => IsMaxTier ? Capacity : capacityTiers[CurrentTierIndex + 1];

    public event Action OnBackpackChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public bool TryAddItem(SeedType type, bool reducedQuality)
    {
        if (IsFull)
        {
            return false;
        }

        items.Add(new HarvestedItem(type, reducedQuality));
        OnBackpackChanged?.Invoke();
        return true;
    }

    public int GetCountOfType(SeedType type)
    {
        int count = 0;
        foreach (HarvestedItem item in items)
        {
            if (item.type == type)
            {
                count++;
            }
        }
        return count;
    }

    // Sells every item currently in the backpack and clears it. Returns total coins earned.
    public int SellAll()
    {
        if (items.Count == 0 || PlantDatabase.Instance == null || CurrencyManager.Instance == null)
        {
            return 0;
        }

        int totalValue = 0;

        foreach (HarvestedItem item in items)
        {
            PlantData data = PlantDatabase.Instance.GetPlantData(item.type);
            if (data == null)
            {
                continue;
            }

            float multiplier = item.reducedQuality ? data.reducedQualitySellMultiplier : 1f;
            totalValue += Mathf.RoundToInt(data.sellPricePerItem * multiplier);
        }

        items.Clear();
        CurrencyManager.Instance.AddCoins(totalValue);
        OnBackpackChanged?.Invoke();

        return totalValue;
    }

    public bool TryUpgrade()
    {
        if (IsMaxTier || CurrencyManager.Instance == null)
        {
            return false;
        }

        int cost = upgradeCosts[CurrentTierIndex];

        if (!CurrencyManager.Instance.TrySpendCoins(cost))
        {
            return false;
        }

        CurrentTierIndex++;
        OnBackpackChanged?.Invoke();
        return true;
    }
}