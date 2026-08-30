using System.Collections.Generic;
using UnityEngine;

public class PlantDatabase : MonoBehaviour
{
    public static PlantDatabase Instance { get; private set; }

    [Header("All Plant Data Assets")]
    public PlantData[] allPlantData;

    private Dictionary<SeedType, PlantData> lookup;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        lookup = new Dictionary<SeedType, PlantData>();

        foreach (PlantData data in allPlantData)
        {
            if (data == null)
            {
                continue;
            }

            if (lookup.ContainsKey(data.seedType))
            {
                Debug.LogWarning("PlantDatabase: Duplicate PlantData entry for seed type " + data.seedType);
                continue;
            }

            lookup.Add(data.seedType, data);
        }
    }

    public PlantData GetPlantData(SeedType seedType)
    {
        if (lookup == null)
        {
            return null;
        }

        lookup.TryGetValue(seedType, out PlantData data);
        return data;
    }
}