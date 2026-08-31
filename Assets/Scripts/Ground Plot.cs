using UnityEngine;

public enum PlotState
{
    Empty,
    Planted
}

public enum PlantHealthState
{
    Healthy,
    Thirsty,
    Critical,
    Dead
}

public class GroundPlot : MonoBehaviour
{
    [Header("References")]
    public GameObject seedVisual;
    public Renderer plotRenderer;
    public GameObject healthIconObject;
    [Tooltip("Sickle icon shown above the plant once it's fully ready to harvest. Takes priority over healthIconObject -- once a plant is ready, the water-drop icon hides even if it's also thirsty, since harvesting is the actionable next step either way.")]
    public GameObject harvestReadyIconObject;

    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 1f, 0.4f, 1f);

    [Header("Growth Visual Sizing")]
    [Tooltip("Max footprint (world units, X/Z) a growth-stage model is allowed to occupy before it gets uniformly scaled down. Plots are spaced 1 unit apart, so keeping this comfortably under 1 leaves a visible gap between neighboring plots instead of their crops visually crowding/touching.")]
    public float maxVisualFootprint = 0.7f;

    [Header("Health Icon (bobs above the plant when it needs water)")]
    [Tooltip("How far above the current growth-stage model's top the icon hovers, in world units.")]
    public float healthIconHeightAboveVisual = 0.15f;
    [Tooltip("How far up/down the icon bobs each bounce cycle, in world units.")]
    public float healthIconBounceAmplitude = 0.08f;
    [Tooltip("Bounce cycles per second.")]
    public float healthIconBounceSpeed = 2f;

    [Header("Health Colors")]
    public Color healthyColor = new Color(0.3f, 0.8f, 0.3f, 1f);
    public Color thirstyColor = new Color(0.85f, 0.8f, 0.3f, 1f);
    public Color criticalColor = new Color(0.7f, 0.45f, 0.15f, 1f);
    public Color deadColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    public PlotState State { get; private set; } = PlotState.Empty;
    public SeedType PlantedSeed { get; private set; } = SeedType.None;
    public PlantHealthState HealthState { get; private set; } = PlantHealthState.Healthy;
    public bool HadCriticalDamage { get; private set; }
    public bool IsReadyToHarvest { get; private set; }

    public bool IsEmpty => State == PlotState.Empty;
    public bool IsDead => HealthState == PlantHealthState.Dead;

    private Color originalColor;
    private bool isCurrentlyHighlighted;
    private PlantData plantData;

    // The currently instantiated growth-stage (or dead) model, if any. seedVisual is kept inactive
    // as before and now only serves as a position/rotation marker for where this spawns -- the
    // actual visible model is whichever PlantData.growthStagePrefabs entry matches the plant's
    // current growth progress.
    private GameObject currentStageVisual;
    private int currentStageIndex = -1;

    private int daysSincePlanted;
    private bool wateredToday;
    private bool wateredMorningToday;
    private bool wateredEveningToday;
    private bool wateredInStrictWindowToday;
    private float overdueSinceRealTime = -1f;

    void Awake()
    {
        if (plotRenderer != null)
        {
            originalColor = MaterialColorHelper.GetColor(plotRenderer);
        }

        if (seedVisual != null)
        {
            seedVisual.SetActive(false);
        }

        if (healthIconObject != null)
        {
            // healthIconObject (a world-space Canvas) sits under this plot's Transform in the
            // prefab, but the plot's Transform is squashed flat on Y to look like a thin ground
            // tile -- the same problem the growth-stage models had. Rather than fight that squash
            // for a UI element that also needs to float and bounce well above the tile, detach it
            // once at spawn and drive its world position directly every frame (see
            // UpdateHealthIconPosition). worldPositionStays=false keeps its current local numbers,
            // which (with no parent left to apply the squash) become its true, correct world scale.
            healthIconObject.transform.SetParent(null, false);
            healthIconObject.SetActive(false);
        }

        if (harvestReadyIconObject != null)
        {
            harvestReadyIconObject.transform.SetParent(null, false);
            harvestReadyIconObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnNewDay += HandleNewDay;
        }
    }

    void OnDisable()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnNewDay -= HandleNewDay;
        }
    }

    void OnDestroy()
    {
        // healthIconObject was detached to the scene root in Awake (see there for why), so it
        // won't be cleaned up automatically as a child of this plot -- destroy it explicitly to
        // avoid leaving an orphaned icon behind if a plot is ever removed.
        if (healthIconObject != null)
        {
            Destroy(healthIconObject);
        }

        if (harvestReadyIconObject != null)
        {
            Destroy(harvestReadyIconObject);
        }
    }

    void Update()
    {
        if (healthIconObject != null && healthIconObject.activeSelf)
        {
            UpdateIconPosition(healthIconObject);
        }

        if (harvestReadyIconObject != null && harvestReadyIconObject.activeSelf)
        {
            UpdateIconPosition(harvestReadyIconObject);
        }

        if (State != PlotState.Planted)
        {
            return;
        }

        if (HealthState == PlantHealthState.Healthy || HealthState == PlantHealthState.Dead)
        {
            return;
        }

        if (plantData == null || overdueSinceRealTime < 0f)
        {
            return;
        }

        float elapsedSeconds = Time.time - overdueSinceRealTime;
        float thirstyToCriticalSeconds = plantData.thirstyToCriticalHours * 3600f;
        float criticalToDeadSeconds = plantData.criticalToDeadHours * 3600f;

        if (HealthState == PlantHealthState.Thirsty && elapsedSeconds >= thirstyToCriticalSeconds)
        {
            HadCriticalDamage = true;
            SetHealthState(PlantHealthState.Critical);
        }
        else if (HealthState == PlantHealthState.Critical && elapsedSeconds >= thirstyToCriticalSeconds + criticalToDeadSeconds)
        {
            SetHealthState(PlantHealthState.Dead);
        }
    }

    // Floats the given icon (water-drop or harvest-ready) just above whatever model is currently
    // showing on this plot (the seed, a mid-growth stage, or the full-grown crop -- whatever
    // GetVisualTopWorldPosition finds), with a gentle sine-wave bob layered on top so it reads as
    // an active prompt rather than a static label. Shared by both icons since they're never both
    // visible at once (see RefreshIcons).
    private void UpdateIconPosition(GameObject icon)
    {
        Vector3 basePos = GetVisualTopWorldPosition();
        float bounce = Mathf.Sin(Time.time * healthIconBounceSpeed) * healthIconBounceAmplitude;
        icon.transform.position = basePos + Vector3.up * (healthIconHeightAboveVisual + bounce);
    }

    // Top-center of whatever's currently visible on this plot, in world space. Falls back to a
    // fixed height above the plot's own position if there's no stage model yet (e.g. no
    // growthStagePrefabs configured for this plant).
    private Vector3 GetVisualTopWorldPosition()
    {
        if (currentStageVisual != null)
        {
            Bounds bounds = GetRendererBounds(currentStageVisual);
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        return transform.position + Vector3.up * 0.5f;
    }

    // Combined world-space bounds of every renderer under the given object (many of the imported
    // crop prefabs are made of several sub-meshes -- stem, head, leaves -- rather than one).
    private Bounds GetRendererBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(go.transform.position, Vector3.zero);
        }

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combined.Encapsulate(renderers[i].bounds);
        }
        return combined;
    }

    public void Plant(SeedType seedType)
    {
        if (State != PlotState.Empty)
        {
            return;
        }

        State = PlotState.Planted;
        PlantedSeed = seedType;

        plantData = PlantDatabase.Instance != null ? PlantDatabase.Instance.GetPlantData(seedType) : null;
        if (plantData == null)
        {
            Debug.LogWarning("No PlantData found for seed type: " + seedType + ". Watering rules will not apply.");
        }

        daysSincePlanted = 0;
        HadCriticalDamage = false;
        IsReadyToHarvest = false;
        overdueSinceRealTime = -1f;
        ResetDailyWateringFlags();

        SetHealthState(PlantHealthState.Healthy);

        UpdateStageVisual(ComputeStageIndex());

        Debug.Log("Planted " + seedType + " on plot: " + gameObject.name);
    }

    public void Water()
    {
        if (State != PlotState.Planted || HealthState == PlantHealthState.Dead)
        {
            return;
        }

        wateredToday = true;

        if (TimeManager.Instance != null)
        {
            if (TimeManager.Instance.CurrentTimeOfDay == TimeManager.TimeOfDay.Morning)
            {
                wateredMorningToday = true;
            }
            else if (TimeManager.Instance.CurrentTimeOfDay == TimeManager.TimeOfDay.Evening)
            {
                wateredEveningToday = true;
            }

            if (plantData != null && plantData.wateringRule == WateringRule.StrictTimeWindow &&
                TimeManager.Instance.IsWithinTimeWindow(plantData.strictWindowHour, plantData.strictWindowMinute, plantData.strictWindowToleranceMinutes))
            {
                wateredInStrictWindowToday = true;
            }
        }

        if (HealthState == PlantHealthState.Thirsty || HealthState == PlantHealthState.Critical)
        {
            SetHealthState(PlantHealthState.Healthy);
            overdueSinceRealTime = -1f;
        }

        Debug.Log("Watered plot: " + gameObject.name);
    }

    public void Uproot()
    {
        if (HealthState != PlantHealthState.Dead)
        {
            return;
        }

        State = PlotState.Empty;
        PlantedSeed = SeedType.None;
        plantData = null;
        daysSincePlanted = 0;
        HadCriticalDamage = false;
        IsReadyToHarvest = false;
        overdueSinceRealTime = -1f;
        ResetDailyWateringFlags();

        SetHealthState(PlantHealthState.Healthy);

        ClearStageVisual();

        Debug.Log("Uprooted dead plant on plot: " + gameObject.name);
    }

    // Harvests a ready plant into the player's inventory grid. Works even while the plant is
    // Critical (rescuing it before it dies) � the harvested item is flagged as reduced
    // quality if this plant ever took Critical damage. Returns false if not ready,
    // dead, or if there is no room in the inventory grid.
    public bool Harvest()
    {
        if (State != PlotState.Planted || !IsReadyToHarvest || HealthState == PlantHealthState.Dead)
        {
            return false;
        }

        if (EquipmentManager.Instance == null)
        {
            Debug.LogWarning("Ground_Plot: No EquipmentManager found in scene.");
            return false;
        }

        bool added = EquipmentManager.Instance.TryAddHarvestedCrop(PlantedSeed, HadCriticalDamage);
        if (!added)
        {
            Debug.Log("Inventory is full. Cannot harvest right now.");
            return false;
        }

        Debug.Log("Harvested " + PlantedSeed + " from plot: " + gameObject.name);

        State = PlotState.Empty;
        PlantedSeed = SeedType.None;
        plantData = null;
        daysSincePlanted = 0;
        HadCriticalDamage = false;
        IsReadyToHarvest = false;
        overdueSinceRealTime = -1f;
        ResetDailyWateringFlags();

        SetHealthState(PlantHealthState.Healthy);

        ClearStageVisual();

        return true;
    }

    public void SetHighlight(bool isHighlighted)
    {
        isCurrentlyHighlighted = isHighlighted;
        ApplyRendererColor();
    }

    private void HandleNewDay(int newDay)
    {
        if (State != PlotState.Planted || HealthState == PlantHealthState.Dead)
        {
            return;
        }

        bool requirementMet = CheckWateringRequirementMet();

        if (requirementMet)
        {
            if (HealthState != PlantHealthState.Healthy)
            {
                SetHealthState(PlantHealthState.Healthy);
            }
            overdueSinceRealTime = -1f;
        }
        else if (HealthState == PlantHealthState.Healthy)
        {
            overdueSinceRealTime = Time.time;
            SetHealthState(PlantHealthState.Thirsty);
        }

        if (HealthState == PlantHealthState.Healthy || HealthState == PlantHealthState.Thirsty)
        {
            daysSincePlanted++;
            CheckHarvestReady();
            RefreshIcons();
            UpdateStageVisual(ComputeStageIndex());
        }

        ResetDailyWateringFlags();
    }

    private bool CheckWateringRequirementMet()
    {
        if (plantData == null)
        {
            return true;
        }

        switch (plantData.wateringRule)
        {
            case WateringRule.OncePerDay:
                return wateredToday;
            case WateringRule.MorningAndEvening:
                return wateredMorningToday && wateredEveningToday;
            case WateringRule.StrictTimeWindow:
                return wateredInStrictWindowToday;
            default:
                return true;
        }
    }

    private void ResetDailyWateringFlags()
    {
        wateredToday = false;
        wateredMorningToday = false;
        wateredEveningToday = false;
        wateredInStrictWindowToday = false;
    }

    private void CheckHarvestReady()
    {
        if (plantData == null)
        {
            return;
        }

        IsReadyToHarvest = daysSincePlanted >= plantData.minHarvestDays;
    }

    private void SetHealthState(PlantHealthState newState)
    {
        HealthState = newState;
        ApplyRendererColor();
        RefreshIcons();

        if (newState == PlantHealthState.Dead)
        {
            ShowDeadVisual();
        }
    }

    // Single source of truth for which overhead icon (if any) should be showing right now.
    // harvestReadyIconObject wins whenever the plant is ready to harvest -- even if it's also
    // thirsty, harvesting is the next actionable step, so there's no point nagging for water too.
    // Called from SetHealthState (health just changed) and again after CheckHarvestReady (harvest
    // readiness just changed) since those two conditions can each flip independently.
    private void RefreshIcons()
    {
        bool ready = IsReadyToHarvest && HealthState != PlantHealthState.Dead;
        bool thirsty = !ready && (HealthState == PlantHealthState.Thirsty || HealthState == PlantHealthState.Critical);

        if (healthIconObject != null)
        {
            healthIconObject.SetActive(thirsty);
        }

        if (harvestReadyIconObject != null)
        {
            harvestReadyIconObject.SetActive(ready);
        }
    }

    // Maps daysSincePlanted -> an index into plantData.growthStagePrefabs, spreading the
    // available stages evenly across the plant's minHarvestDays growth window. Stage 0 always
    // plays at the moment of planting (looks like a seed); the last stage is always shown once
    // the plant is ready to harvest, regardless of exactly how many days that took. Returns -1
    // if this plant has no growth-stage models configured.
    private int ComputeStageIndex()
    {
        if (plantData == null || plantData.growthStagePrefabs == null || plantData.growthStagePrefabs.Length == 0)
        {
            return -1;
        }

        int stageCount = plantData.growthStagePrefabs.Length;

        if (IsReadyToHarvest)
        {
            return stageCount - 1;
        }

        int growthWindow = Mathf.Max(plantData.minHarvestDays, 1);
        float progress = Mathf.Clamp01((float)daysSincePlanted / growthWindow);
        int index = Mathf.FloorToInt(progress * (stageCount - 1));
        return Mathf.Clamp(index, 0, stageCount - 1);
    }

    // Instantiates the growth-stage prefab for the given index (spawned at seedVisual's
    // position/rotation, which stays inactive and now only serves as a spawn anchor), replacing
    // whatever stage model is currently shown. No-ops if the index is already showing. Passing a
    // negative index (no growth-stage data available) just clears any existing model.
    private void UpdateStageVisual(int stageIndex)
    {
        if (stageIndex < 0 || plantData == null || plantData.growthStagePrefabs == null || stageIndex >= plantData.growthStagePrefabs.Length)
        {
            ClearStageVisual();
            return;
        }

        if (stageIndex == currentStageIndex && currentStageVisual != null)
        {
            return;
        }

        GameObject prefab = plantData.growthStagePrefabs[stageIndex];
        if (prefab == null)
        {
            ClearStageVisual();
            return;
        }

        if (currentStageVisual != null)
        {
            // Destroy() only actually removes the object at the end of this frame, so without
            // this it's possible (e.g. right when a growth stage swaps) for the old and new
            // models to both render for one frame -- deactivating immediately guarantees only
            // ever one visible model at a time, no matter how the timing lines up.
            currentStageVisual.SetActive(false);
            Destroy(currentStageVisual);
            currentStageVisual = null;
        }

        Transform anchor = seedVisual != null ? seedVisual.transform : transform;
        currentStageVisual = Instantiate(prefab, anchor.position, anchor.rotation, transform);
        NormalizeVisualScale(currentStageVisual);
        ClampVisualFootprint(currentStageVisual);
        CenterVisualOnPlot(currentStageVisual);
        ApplyGroundYOffset(currentStageVisual, plantData.GetGrowthStageYOffset(stageIndex));
        currentStageIndex = stageIndex;
    }

    // A handful of imported growth-stage prefabs have their mesh pivot authored somewhere other
    // than the base of the plant (e.g. the full-grown sunflower's pivot sits up near the flower
    // head instead of the stem base), which makes them render sunken into the plot tile even
    // though NormalizeVisualScale/CenterVisualOnPlot have already fixed their scale and X/Z
    // position. Rather than guessing at a universal fix (which risks disturbing packs that
    // deliberately show a bit of stem/root below the tile surface, like the carrot), this simply
    // nudges the model up/down by whatever correction PlantData has recorded for that specific
    // stage -- 0 by default, so any prefab that already looks right is completely unaffected.
    private void ApplyGroundYOffset(GameObject visual, float yOffset)
    {
        if (visual == null || Mathf.Approximately(yOffset, 0f))
        {
            return;
        }

        visual.transform.position += Vector3.up * yOffset;
    }

    // The imported crop packs vary a lot in how wide their models are (a full-grown tomato measures
    // nearly as wide as the plot tile itself), so with plots spaced 1 unit apart, neighboring plants
    // can end up visually touching/overlapping even though each plot is only ever showing its own
    // single, correctly-centered model -- it just reads as "one clump" from a distance. This measures
    // the model's actual rendered footprint (after NormalizeVisualScale has undone the plot's squash)
    // and, only if it's wider than maxVisualFootprint, scales the whole model down uniformly (X/Y/Z
    // together, so it doesn't distort) until it fits -- leaving a clear gap to the next plot over.
    // Smaller models (most growth stages) are left completely untouched.
    private void ClampVisualFootprint(GameObject visual)
    {
        if (visual == null || maxVisualFootprint <= 0f)
        {
            return;
        }

        Bounds bounds = GetRendererBounds(visual);
        float widestAxis = Mathf.Max(bounds.size.x, bounds.size.z);
        if (widestAxis <= maxVisualFootprint)
        {
            return;
        }

        float scaleFactor = maxVisualFootprint / widestAxis;
        visual.transform.localScale *= scaleFactor;
    }

    // The imported crop prefabs come from three different asset packs with inconsistent mesh
    // pivots -- some are centered on their own model, some (like the Sunflower family) have their
    // pivot off to one side, which put them noticeably off-center on the plot tile. This measures
    // the model's actual rendered footprint and nudges it sideways (X/Z only -- height stays at
    // the seedVisual anchor) so it's centered on the plot regardless of where its pivot happens to
    // be, working the same way for every pack without needing per-prefab tuning.
    private void CenterVisualOnPlot(GameObject visual)
    {
        if (visual == null)
        {
            return;
        }

        Bounds bounds = GetRendererBounds(visual);
        Vector3 plotCenter = transform.position;
        Vector3 offset = new Vector3(plotCenter.x - bounds.center.x, 0f, plotCenter.z - bounds.center.z);
        visual.transform.position += offset;
    }

    // The plot's own root Transform is squashed flat on the Y axis (its cube mesh is scaled down
    // to look like a thin ground tile), so any growth-stage model instantiated as its child would
    // otherwise inherit that squash and render as a near-flat sliver. This restores each model's
    // own authored scale by cancelling out the parent plot's (non-uniform) lossy scale, so a
    // model that's ready to plug into this system at scale (1,1,1) actually shows at (1,1,1)
    // regardless of how thin the plot tile underneath it is.
    private void NormalizeVisualScale(GameObject visual)
    {
        if (visual == null)
        {
            return;
        }

        Vector3 authoredScale = visual.transform.localScale;
        Vector3 parentLossyScale = transform.lossyScale;

        visual.transform.localScale = new Vector3(
            !Mathf.Approximately(parentLossyScale.x, 0f) ? authoredScale.x / parentLossyScale.x : authoredScale.x,
            !Mathf.Approximately(parentLossyScale.y, 0f) ? authoredScale.y / parentLossyScale.y : authoredScale.y,
            !Mathf.Approximately(parentLossyScale.z, 0f) ? authoredScale.z / parentLossyScale.z : authoredScale.z);
    }

    // Destroys the currently shown growth-stage/dead model, if any, and resets tracking so the
    // next UpdateStageVisual call always spawns fresh.
    private void ClearStageVisual()
    {
        if (currentStageVisual != null)
        {
            // Destroy() only actually removes the object at the end of this frame, so without
            // this it's possible (e.g. right when a growth stage swaps) for the old and new
            // models to both render for one frame -- deactivating immediately guarantees only
            // ever one visible model at a time, no matter how the timing lines up.
            currentStageVisual.SetActive(false);
            Destroy(currentStageVisual);
            currentStageVisual = null;
        }
        currentStageIndex = -1;
    }

    // Swaps in the wilted/dead model (if this plant's PlantData has one configured) when the
    // plant dies from neglect. Falls back to just clearing the model (relying on the plot
    // tile's dark color tint alone) if no deadStagePrefab is set.
    private void ShowDeadVisual()
    {
        if (plantData == null || plantData.deadStagePrefab == null)
        {
            ClearStageVisual();
            return;
        }

        if (currentStageVisual != null)
        {
            // Destroy() only actually removes the object at the end of this frame, so without
            // this it's possible (e.g. right when a growth stage swaps) for the old and new
            // models to both render for one frame -- deactivating immediately guarantees only
            // ever one visible model at a time, no matter how the timing lines up.
            currentStageVisual.SetActive(false);
            Destroy(currentStageVisual);
            currentStageVisual = null;
        }

        Transform anchor = seedVisual != null ? seedVisual.transform : transform;
        currentStageVisual = Instantiate(plantData.deadStagePrefab, anchor.position, anchor.rotation, transform);
        NormalizeVisualScale(currentStageVisual);
        ClampVisualFootprint(currentStageVisual);
        CenterVisualOnPlot(currentStageVisual);
        ApplyGroundYOffset(currentStageVisual, plantData.deadStageYOffset);
        currentStageIndex = -2; // sentinel: showing the dead model, not a growth-stage index
    }

    private void ApplyRendererColor()
    {
        if (plotRenderer == null)
        {
            return;
        }

        Color baseColor = GetCurrentBaseColor();
        MaterialColorHelper.SetColor(plotRenderer, isCurrentlyHighlighted ? highlightColor : baseColor);
    }

    private Color GetCurrentBaseColor()
    {
        if (State == PlotState.Empty)
        {
            return originalColor;
        }

        switch (HealthState)
        {
            case PlantHealthState.Healthy:
                return healthyColor;
            case PlantHealthState.Thirsty:
                return thirstyColor;
            case PlantHealthState.Critical:
                return criticalColor;
            case PlantHealthState.Dead:
                return deadColor;
            default:
                return originalColor;
        }
    }
}