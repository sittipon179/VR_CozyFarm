using System;
using System.Collections.Generic;
using UnityEngine;

public enum EquippedType
{
    None,
    Hoe,
    WateringCan,
    Seed
}

public enum SeedType
{
    None,
    Tomato,
    Cabbage,
    Carrot,
    Strawberry,
    Orchid
}

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }

    [Header("Starting Seed Counts")]
    public int startingTomatoSeeds = 10;
    public int startingCabbageSeeds = 10;
    public int startingCarrotSeeds = 10;
    public int startingStrawberrySeeds = 5;
    public int startingOrchidSeeds = 2;

    public EquippedType CurrentEquipped { get; private set; } = EquippedType.None;
    public SeedType CurrentSeed { get; private set; } = SeedType.None;

    public Dictionary<SeedType, int> SeedCounts { get; private set; } = new Dictionary<SeedType, int>();

    public event Action OnEquipmentChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SeedCounts[SeedType.Tomato] = startingTomatoSeeds;
        SeedCounts[SeedType.Cabbage] = startingCabbageSeeds;
        SeedCounts[SeedType.Carrot] = startingCarrotSeeds;
        SeedCounts[SeedType.Strawberry] = startingStrawberrySeeds;
        SeedCounts[SeedType.Orchid] = startingOrchidSeeds;
    }

    public void EquipHoe()
    {
        CurrentEquipped = EquippedType.Hoe;
        CurrentSeed = SeedType.None;
        OnEquipmentChanged?.Invoke();
    }

    public void EquipWateringCan()
    {
        CurrentEquipped = EquippedType.WateringCan;
        CurrentSeed = SeedType.None;
        OnEquipmentChanged?.Invoke();
    }

    public void EquipSeed(SeedType type)
    {
        if (!SeedCounts.ContainsKey(type) || SeedCounts[type] <= 0)
        {
            Debug.Log("No seeds left for: " + type);
            return;
        }
        CurrentEquipped = EquippedType.Seed;
        CurrentSeed = type;
        OnEquipmentChanged?.Invoke();
    }

    public void UnequipAll()
    {
        CurrentEquipped = EquippedType.None;
        CurrentSeed = SeedType.None;
        OnEquipmentChanged?.Invoke();
    }

    public bool UseSeed(SeedType type)
    {
        if (!SeedCounts.ContainsKey(type) || SeedCounts[type] <= 0)
        {
            return false;
        }
        SeedCounts[type]--;
        OnEquipmentChanged?.Invoke();

        if (SeedCounts[type] <= 0 && CurrentSeed == type)
        {
            UnequipAll();
        }
        return true;
    }

    public void AddSeeds(SeedType type, int amount)
    {
        if (amount <= 0 || type == SeedType.None)
        {
            return;
        }

        if (!SeedCounts.ContainsKey(type))
        {
            SeedCounts[type] = 0;
        }

        SeedCounts[type] += amount;
        OnEquipmentChanged?.Invoke();
    }
}