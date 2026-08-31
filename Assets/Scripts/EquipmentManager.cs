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
    Pumpkin,
    Sunflower
}

public enum ToolType
{
    None,
    Hoe,
    WateringCan
}

// One grid slot: either a tool (isTool = true, non-stackable, count is unused/always 1),
// a seed stack (isTool = false, isCrop = false, seed != SeedType.None, count = how many are held),
// or a harvested-crop stack (isCrop = true, seed identifies the crop type, reducedQuality flags
// whether this stack sells at the reduced-quality price, count = how many are held). A crop stack
// and a seed stack of the same SeedType are always kept in separate slots so they never mix.
// An empty slot has isTool = false, isCrop = false, and seed = SeedType.None.
[Serializable]
public struct InventorySlot
{
    public bool isTool;
    public ToolType tool;
    public SeedType seed;
    public int count;
    public bool isCrop;
    public bool reducedQuality;
}

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }

    [Header("Starting Seed Counts")]
    public int startingTomatoSeeds = 10;
    public int startingCabbageSeeds = 10;
    public int startingCarrotSeeds = 10;
    public int startingPumpkinSeeds = 5;
    public int startingSunflowerSeeds = 2;

    [Header("Inventory Grid")]
    [Tooltip("Total number of slots the grid can ever hold.")]
    public int maxCapacity = 27;
    [Tooltip("How many of those slots are unlocked from the start (starting from index 0).")]
    public int startingUnlockedSlots = 9;

    [Header("Slot Unlock Cost")]
    [Tooltip("Coin cost to unlock the first locked slot.")]
    public int baseUnlockCost = 100;
    [Tooltip("How much the unlock cost increases for each slot unlocked after the first.")]
    public int unlockCostIncrement = 50;

    public EquippedType CurrentEquipped { get; private set; } = EquippedType.None;
    public SeedType CurrentSeed { get; private set; } = SeedType.None;

    public int UnlockedSlotCount { get; private set; }

    private InventorySlot[] slots;

    public event Action OnEquipmentChanged;

    // Kept for backward compatibility with any code that reads seed totals as a dictionary.
    // Computed from the slot grid on every access rather than stored separately, so there is
    // only one source of truth (the slots array).
    public Dictionary<SeedType, int> SeedCounts
    {
        get
        {
            Dictionary<SeedType, int> result = new Dictionary<SeedType, int>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].isTool || slots[i].seed == SeedType.None)
                {
                    continue;
                }

                if (result.ContainsKey(slots[i].seed))
                {
                    result[slots[i].seed] += slots[i].count;
                }
                else
                {
                    result[slots[i].seed] = slots[i].count;
                }
            }
            return result;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        slots = new InventorySlot[Mathf.Max(maxCapacity, startingUnlockedSlots)];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new InventorySlot { isTool = false, tool = ToolType.None, seed = SeedType.None, count = 0 };
        }

        slots[0] = new InventorySlot { isTool = true, tool = ToolType.Hoe, seed = SeedType.None, count = 1 };
        slots[1] = new InventorySlot { isTool = true, tool = ToolType.WateringCan, seed = SeedType.None, count = 1 };
        slots[2] = new InventorySlot { isTool = false, tool = ToolType.None, seed = SeedType.Tomato, count = startingTomatoSeeds };
        slots[3] = new InventorySlot { isTool = false, tool = ToolType.None, seed = SeedType.Cabbage, count = startingCabbageSeeds };
        slots[4] = new InventorySlot { isTool = false, tool = ToolType.None, seed = SeedType.Carrot, count = startingCarrotSeeds };
        slots[5] = new InventorySlot { isTool = false, tool = ToolType.None, seed = SeedType.Pumpkin, count = startingPumpkinSeeds };
        slots[6] = new InventorySlot { isTool = false, tool = ToolType.None, seed = SeedType.Sunflower, count = startingSunflowerSeeds };

        UnlockedSlotCount = Mathf.Clamp(startingUnlockedSlots, 0, slots.Length);
    }

    public int MaxCapacity => slots.Length;

    public bool IsSlotUnlocked(int index)
    {
        return index >= 0 && index < UnlockedSlotCount;
    }

    public InventorySlot GetSlot(int index)
    {
        if (index < 0 || index >= slots.Length)
        {
            return new InventorySlot();
        }
        return slots[index];
    }

    public void EquipSlot(int index)
    {
        if (!IsSlotUnlocked(index))
        {
            return;
        }

        InventorySlot slot = slots[index];

        if (slot.isTool)
        {
            if (slot.tool == ToolType.Hoe)
            {
                EquipHoe();
            }
            else if (slot.tool == ToolType.WateringCan)
            {
                EquipWateringCan();
            }
        }
        else if (!slot.isCrop && slot.seed != SeedType.None)
        {
            // Harvested crop stacks (isCrop) are not equippable/plantable -- only real seed
            // stacks can be equipped. Crops can only be sold (see SellAllCrops).
            EquipSeed(slot.seed);
        }
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
        int index = FindSeedSlotIndex(type);
        if (index < 0 || slots[index].count <= 0)
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
        int index = FindSeedSlotIndex(type);
        if (index < 0 || slots[index].count <= 0)
        {
            return false;
        }

        slots[index].count--;

        // Once a seed stack runs out, clear the slot back to fully empty instead of leaving a
        // lingering "Tomato x0" stack sitting in the grid -- this frees the slot up visually and
        // for future items right away.
        bool emptied = slots[index].count <= 0;
        if (emptied)
        {
            slots[index] = new InventorySlot { isTool = false, tool = ToolType.None, seed = SeedType.None, count = 0, isCrop = false, reducedQuality = false };
        }

        OnEquipmentChanged?.Invoke();

        if (emptied && CurrentSeed == type)
        {
            UnequipAll();
        }
        return true;
    }

    // Swaps the contents of two slots (used by the Inventory UI's press-and-hold drag reordering).
    // CurrentEquipped/CurrentSeed track by tool/seed type rather than slot index, so swapping which
    // slot holds the currently equipped item is safe and doesn't change what's equipped.
    public void SwapSlots(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= slots.Length || indexB < 0 || indexB >= slots.Length || indexA == indexB)
        {
            return;
        }

        InventorySlot temp = slots[indexA];
        slots[indexA] = slots[indexB];
        slots[indexB] = temp;

        OnEquipmentChanged?.Invoke();
    }

    public void AddSeeds(SeedType type, int amount)
    {
        if (amount <= 0 || type == SeedType.None)
        {
            return;
        }

        int index = FindSeedSlotIndex(type);
        if (index < 0)
        {
            index = FindNextEmptyUnlockedSlot();
            if (index < 0)
            {
                Debug.LogWarning("EquipmentManager: no empty unlocked inventory slot available to add seed: " + type);
                return;
            }
            slots[index] = new InventorySlot { isTool = false, tool = ToolType.None, seed = type, count = 0 };
        }

        slots[index].count += amount;
        OnEquipmentChanged?.Invoke();
    }

    // --- Slot unlock system ---

    public bool IsMaxCapacity => UnlockedSlotCount >= slots.Length;

    public int NextUnlockCost => IsMaxCapacity ? -1 : (baseUnlockCost + unlockCostIncrement * (UnlockedSlotCount - startingUnlockedSlots));

    public bool TryUnlockNextSlot()
    {
        if (IsMaxCapacity || CurrencyManager.Instance == null)
        {
            return false;
        }

        int cost = NextUnlockCost;
        if (!CurrencyManager.Instance.TrySpendCoins(cost))
        {
            return false;
        }

        UnlockedSlotCount++;
        OnEquipmentChanged?.Invoke();
        return true;
    }

    // --- Harvested crop storage (merged into this grid; see TryAddHarvestedCrop/SellAllCrops) ---

    // Adds harvested crop(s) to a matching crop stack (same type + same quality flag) if one
    // exists, otherwise claims the next empty unlocked slot as a new crop stack. Returns false
    // if there is no matching stack and no empty unlocked slot, so Ground Plot.Harvest() can
    // fail gracefully (inventory full) the same way it used to when the backpack was full.
    public bool TryAddHarvestedCrop(SeedType type, bool reducedQuality, int amount = 1)
    {
        if (amount <= 0 || type == SeedType.None)
        {
            return false;
        }

        int index = FindCropSlotIndex(type, reducedQuality);
        if (index < 0)
        {
            index = FindNextEmptyUnlockedSlot();
            if (index < 0)
            {
                return false;
            }
            slots[index] = new InventorySlot { isTool = false, tool = ToolType.None, seed = type, count = 0, isCrop = true, reducedQuality = reducedQuality };
        }

        slots[index].count += amount;
        OnEquipmentChanged?.Invoke();
        return true;
    }

    public bool HasAnyCrops
    {
        get
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].isCrop && slots[i].count > 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    // Sells every crop stack currently in the grid and clears those slots. Returns total coins earned.
    public int SellAllCrops()
    {
        if (PlantDatabase.Instance == null || CurrencyManager.Instance == null)
        {
            return 0;
        }

        int totalValue = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].isCrop || slots[i].count <= 0)
            {
                continue;
            }

            PlantData data = PlantDatabase.Instance.GetPlantData(slots[i].seed);
            if (data != null)
            {
                float multiplier = slots[i].reducedQuality ? data.reducedQualitySellMultiplier : 1f;
                totalValue += Mathf.RoundToInt(data.sellPricePerItem * multiplier) * slots[i].count;
            }

            slots[i] = new InventorySlot { isTool = false, tool = ToolType.None, seed = SeedType.None, count = 0, isCrop = false, reducedQuality = false };
        }

        if (totalValue > 0)
        {
            CurrencyManager.Instance.AddCoins(totalValue);
        }

        OnEquipmentChanged?.Invoke();
        return totalValue;
    }

    // --- Internal helpers ---

    private int FindSeedSlotIndex(SeedType type)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].isTool && !slots[i].isCrop && slots[i].seed == type)
            {
                return i;
            }
        }
        return -1;
    }

    private int FindCropSlotIndex(SeedType type, bool reducedQuality)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].isCrop && slots[i].seed == type && slots[i].reducedQuality == reducedQuality)
            {
                return i;
            }
        }
        return -1;
    }

    private int FindNextEmptyUnlockedSlot()
    {
        for (int i = 0; i < UnlockedSlotCount; i++)
        {
            if (!slots[i].isTool && !slots[i].isCrop && slots[i].seed == SeedType.None)
            {
                return i;
            }
        }
        return -1;
    }
}
