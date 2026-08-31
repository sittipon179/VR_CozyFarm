using System.Collections.Generic;
using UnityEngine;

public class BuildPlantController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Desktop-testing fallback ray source. Used only when interactionRayOrigin is left empty.")]
    public Camera playerCamera;
    [Tooltip("VR conversion: assign this to the dominant-hand ray/near-far interactor's Transform " +
        "(e.g. 'Right Controller/Near-Far Interactor' or 'Right Hand/Near-Far Interactor') for VR play. " +
        "If left empty, falls back to playerCamera so desktop testing keeps working unchanged.")]
    public Transform interactionRayOrigin;
    public GameObject plotPreviewObject;
    public GameObject groundPlotPrefab;

    [Header("VR Input (optional -- keyboard/mouse below still work as a fallback either way)")]
    [Tooltip("Controller button for till/plant/water/uproot (equivalent to the left mouse button). E.g. XRI Right Interaction/Select or Activate.")]
    public UnityEngine.InputSystem.InputActionReference interactAction;
    [Tooltip("Controller button for harvest (equivalent to the E key).")]
    public UnityEngine.InputSystem.InputActionReference harvestAction;
    [Tooltip("Controller button/axis held to spin the drag grid (equivalent to holding the R key).")]
    public UnityEngine.InputSystem.InputActionReference rotateHoldAction;

    [Header("Layers")]
    public LayerMask groundLayer;
    public LayerMask obstacleLayer;
    public LayerMask plotLayer;
    [Tooltip("Layer that holds each plot's tall interaction hitbox (the 'InteractionZone' child under GroundPlot -- separate from the thin ground-plate collider on plotLayer). Used only by the seed/water/harvest interaction raycasts below, via RaycastAll, so a grown plant blocking the view of a plot behind it never prevents reaching that plot -- the closest ELIGIBLE plot along the ray wins, not just the closest hit.")]
    public LayerMask plotInteractLayer;

    [Header("Placement Settings")]
    public float maxInteractDistance = 10f;
    public float plotCheckRadius = 0.5f;
    public float plotYOffset = 0.05f;

    [Header("Drag Placement (hold and drag to till a patch)")]
    [Tooltip("Max plot columns (world X axis) a single click-drag can create at once, so a drag can never till an enormous area.")]
    public int maxDragColumns = 6;
    [Tooltip("Max plot rows (world Z axis) a single click-drag can create at once.")]
    public int maxDragRows = 6;
    [Tooltip("How far above each drag cell to start the downward ground-probe ray, so uneven terrain still finds the real surface underneath. Increase if your terrain has taller bumps/slopes than this.")]
    public float groundProbeHeight = 5f;

    [Header("Preview Colors")]
    public Color validColor = new Color(0.3f, 1f, 0.3f, 0.6f);
    public Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.6f);

    [Header("Rotation (hold R to spin the plot grid freely)")]
    [Tooltip("Rotation speed in degrees/second right when you start holding R.")]
    public float minRotationSpeed = 60f;
    [Tooltip("Rotation speed in degrees/second once R has been held for rotationAccelerationTime seconds or more.")]
    public float maxRotationSpeed = 420f;
    [Tooltip("How many seconds of holding R it takes to ramp up from minRotationSpeed to maxRotationSpeed.")]
    public float rotationAccelerationTime = 1.5f;

    private Renderer previewRenderer;
    private bool isValidPlacement;
    private GroundPlot currentHoveredPlot;

    // Drag-to-till state. Dragging always starts from a single valid hover point (same rules as
    // the old single-click placement) and grows a grid of plot-sized cells towards the current
    // mouse position, one plot tile per cell -- each cell is checked independently for soil/
    // obstacles, so a bad cell is simply skipped rather than blocking the whole drag.
    private bool isDragging;
    private Vector3 dragStartPoint;
    private float tileSizeX = 1f;
    private float tileSizeZ = 1f;
    private readonly List<GameObject> dragPreviewPool = new List<GameObject>();

    // Current plot-grid orientation, spun freely (any angle, not snapped) by holding R. Deliberately
    // not reset when a drag starts/ends -- once the player picks an orientation it stays selected
    // for the next plot(s) too, same as how the currently equipped seed stays equipped between plants.
    private float dragRotationDegrees;

    // How many consecutive seconds R has been held down -- feeds the accel ramp in HandleHoeMode
    // (the longer you hold, the faster it spins), and resets the instant R is released.
    private float rotationHoldDuration;

    void Start()
    {
        if (plotPreviewObject != null)
        {
            previewRenderer = plotPreviewObject.GetComponentInChildren<Renderer>();
            plotPreviewObject.SetActive(false);
        }

        ComputeTileSize();
    }

    // Reads the real footprint of groundPlotPrefab (mesh bounds x prefab scale) instead of
    // hardcoding a tile size, so the drag grid always matches whatever the plot prefab actually
    // measures, even if it's resized later.
    void ComputeTileSize()
    {
        if (groundPlotPrefab == null)
        {
            return;
        }

        MeshFilter meshFilter = groundPlotPrefab.GetComponentInChildren<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        Vector3 meshSize = meshFilter.sharedMesh.bounds.size;
        Vector3 scale = meshFilter.transform.lossyScale;
        tileSizeX = Mathf.Max(0.1f, meshSize.x * scale.x);
        tileSizeZ = Mathf.Max(0.1f, meshSize.z * scale.z);
    }

    // VR conversion: the ray this whole controller acts on. Prefers interactionRayOrigin (a VR
    // ray/near-far interactor's Transform) when assigned, otherwise falls back to playerCamera --
    // none of the eligibility/placement logic below needed to change, only where the ray comes from.
    Ray GetInteractionRay()
    {
        Transform origin = interactionRayOrigin != null ? interactionRayOrigin : (playerCamera != null ? playerCamera.transform : transform);
        return new Ray(origin.position, origin.forward);
    }

    // VR conversion: till/plant/water/uproot "click". True on the frame a controller button
    // bound to interactAction is pressed, OR the left mouse button -- either works.
    bool InteractPressed()
    {
        return (interactAction != null && interactAction.action != null && interactAction.action.WasPressedThisFrame())
            || Input.GetMouseButtonDown(0);
    }

    bool InteractReleased()
    {
        return (interactAction != null && interactAction.action != null && interactAction.action.WasReleasedThisFrame())
            || Input.GetMouseButtonUp(0);
    }

    // VR conversion: harvest "click". True on the frame a controller button bound to
    // harvestAction is pressed, OR the E key -- either works.
    bool HarvestPressed()
    {
        return (harvestAction != null && harvestAction.action != null && harvestAction.action.WasPressedThisFrame())
            || Input.GetKeyDown(KeyCode.E);
    }

    // VR conversion: held-down check for spinning the drag grid. True while a controller button
    // bound to rotateHoldAction is held, OR the R key -- either works.
    bool RotateHeld()
    {
        return (rotateHoldAction != null && rotateHoldAction.action != null && rotateHoldAction.action.IsPressed())
            || Input.GetKey(KeyCode.R);
    }

    void Update()
    {
        if (EquipmentManager.Instance == null)
        {
            return;
        }

        if (UIStateManager.Instance != null && UIStateManager.Instance.IsAnyUIOpen)
        {
            CancelDragIfActive();
            SetPreviewActive(false);
            ClearHover();
            return;
        }

        if (EquipmentManager.Instance.CurrentEquipped != EquippedType.Hoe)
        {
            CancelDragIfActive();
        }

        switch (EquipmentManager.Instance.CurrentEquipped)
        {
            case EquippedType.Hoe:
                HandleHoeMode();
                break;
            case EquippedType.Seed:
                SetPreviewActive(false);
                HandleSeedMode();
                break;
            case EquippedType.WateringCan:
                SetPreviewActive(false);
                HandleWateringMode();
                break;
            case EquippedType.None:
                SetPreviewActive(false);
                break;
            default:
                SetPreviewActive(false);
                ClearHover();
                break;
        }

        // Harvesting with E works no matter what's currently equipped -- previously this only ran
        // in the "None" (bare-hands) branch above, but there's no way for the player to actually
        // get back to bare-hands once a tool/seed is equipped (nothing ever calls
        // EquipmentManager.UnequipAll() except a seed stack running out), so E effectively never
        // worked in practice. Checking it unconditionally every frame, after the equipped-tool
        // mode above, means E always harvests a ready plot you're looking at, the same way most
        // farming games let you interact with crops regardless of what's in your hand.
        HandleHarvestMode();
    }

    // Casts through every collider on plotInteractLayer along the ray (not just the closest one)
    // and returns the nearest plot that satisfies isEligible, skipping over any plot in front of it
    // that doesn't. This is what lets the player reach a plot standing behind a neighboring plant's
    // model -- a single ordinary Raycast would just stop at that neighbor's hitbox and never see
    // what's behind it, even though the neighbor itself isn't what the player wants to interact
    // with right now.
    private GroundPlot FindEligiblePlot(Ray ray, System.Func<GroundPlot, bool> isEligible)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxInteractDistance, plotInteractLayer);
        if (hits.Length == 0)
        {
            return null;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // The interaction hitbox lives on a child ("InteractionZone") under the plot, not on
            // the same GameObject as the GroundPlot script, so GetComponentInParent is required
            // here (GetComponent would always come back null).
            GroundPlot plot = hit.collider.GetComponentInParent<GroundPlot>();
            if (plot != null && isEligible(plot))
            {
                return plot;
            }
        }

        return null;
    }

    void HandleHoeMode()
    {
        // Holding R spins the plot grid freely (full 360, not snapped to 90-degree steps).
        // The longer R stays held, the faster it spins -- rotationHoldDuration ramps from
        // minRotationSpeed up to maxRotationSpeed over rotationAccelerationTime seconds, then
        // resets the instant R is released so the next hold always starts slow again. Works both
        // before a drag starts (to set your orientation ahead of time) and live while dragging.
        if (RotateHeld())
        {
            rotationHoldDuration += Time.deltaTime;
            float speedT = rotationAccelerationTime > 0f ? Mathf.Clamp01(rotationHoldDuration / rotationAccelerationTime) : 1f;
            float currentSpeed = Mathf.Lerp(minRotationSpeed, maxRotationSpeed, speedT);
            dragRotationDegrees = Mathf.Repeat(dragRotationDegrees + currentSpeed * Time.deltaTime, 360f);
        }
        else
        {
            rotationHoldDuration = 0f;
        }

        Ray ray = GetInteractionRay();
        int combinedMask = groundLayer | obstacleLayer | plotLayer;

        if (isDragging)
        {
            UpdateDrag(ray, combinedMask);
            return;
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, maxInteractDistance, combinedMask))
        {
            SetPreviewActive(false);
            ClearHover();
            return;
        }

        bool hitGround = ((1 << hit.collider.gameObject.layer) & groundLayer) != 0;
        bool hitPlot = ((1 << hit.collider.gameObject.layer) & plotLayer) != 0;

        if (hitPlot)
        {
            SetPreviewActive(false);
            HandleHoeOnExistingPlot(hit);
            return;
        }

        ClearHover();

        if (!hitGround)
        {
            SetPreviewActive(false);
            return;
        }

        Vector3 spawnPos = hit.point + Vector3.up * plotYOffset;

        SetPreviewActive(true);
        plotPreviewObject.transform.position = spawnPos;
        plotPreviewObject.transform.rotation = Quaternion.Euler(0f, dragRotationDegrees, 0f);

        isValidPlacement = !IsCellBlocked(hit.point);

        MaterialColorHelper.SetColor(previewRenderer, isValidPlacement ? validColor : invalidColor);

        // A plain click with no drag still works exactly like before (single tile) -- dragging is
        // just an extension: the drag grid always starts at 1x1 and only grows once the mouse
        // actually moves away from this point (see ComputeDragGridCells).
        if (isValidPlacement && InteractPressed())
        {
            BeginDrag(hit.point);
        }
    }

    void BeginDrag(Vector3 startPoint)
    {
        isDragging = true;
        dragStartPoint = startPoint;
        SetPreviewActive(false);
    }

    void UpdateDrag(Ray ray, int combinedMask)
    {
        Vector3 currentPoint = dragStartPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractDistance, combinedMask))
        {
            currentPoint = hit.point;
        }

        List<Vector3> cellCenters = ComputeDragGridCells(dragStartPoint, currentPoint);
        ShowDragPreview(cellCenters);

        if (InteractReleased())
        {
            FinalizeDrag(cellCenters);
            EndDrag();
        }
    }

    void EndDrag()
    {
        isDragging = false;
        HideDragPreview();
    }

    void CancelDragIfActive()
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        HideDragPreview();
    }

    // Builds the grid of candidate plot-cell centers between the drag start and the current
    // mouse point, clamped to maxDragColumns/maxDragRows so a drag can never till more than that
    // many tiles in either direction. The grid axes follow dragRotationDegrees (set by the Y key)
    // instead of always being world X/Z -- the mouse delta is measured in that rotated frame, so
    // spinning the orientation reshapes which way the patch grows, and each cell offset is then
    // rotated back into world space to get its actual position.
    List<Vector3> ComputeDragGridCells(Vector3 start, Vector3 end)
    {
        Quaternion rotation = Quaternion.Euler(0f, dragRotationDegrees, 0f);
        Vector3 localDelta = Quaternion.Inverse(rotation) * (end - start);
        float dx = localDelta.x;
        float dz = localDelta.z;

        int columns = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(dx) / tileSizeX) + 1, 1, Mathf.Max(1, maxDragColumns));
        int rows = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(dz) / tileSizeZ) + 1, 1, Mathf.Max(1, maxDragRows));

        float signX = dx >= 0f ? 1f : -1f;
        float signZ = dz >= 0f ? 1f : -1f;

        List<Vector3> cells = new List<Vector3>(columns * rows);
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector3 localOffset = new Vector3(signX * col * tileSizeX, 0f, signZ * row * tileSizeZ);
                Vector3 worldOffset = rotation * localOffset;
                cells.Add(new Vector3(start.x + worldOffset.x, start.y, start.z + worldOffset.z));
            }
        }
        return cells;
    }

    // Re-finds the real ground surface under a cell with a fresh downward probe (rather than
    // trusting the drag's rough start-height) so sloped/uneven terrain still lands each plot on
    // its actual surface, and so "must be soil" is enforced per cell, not just at the drag start.
    bool TryGetGroundCell(Vector3 approxWorldPoint, out Vector3 groundPoint)
    {
        Vector3 probeOrigin = new Vector3(approxWorldPoint.x, approxWorldPoint.y + groundProbeHeight, approxWorldPoint.z);
        if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, groundProbeHeight * 2f, groundLayer))
        {
            groundPoint = hit.point;
            return true;
        }

        groundPoint = approxWorldPoint;
        return false;
    }

    bool IsCellBlocked(Vector3 groundPoint)
    {
        return Physics.CheckSphere(groundPoint, plotCheckRadius, obstacleLayer)
            || Physics.CheckSphere(groundPoint, plotCheckRadius, plotLayer);
    }

    void ShowDragPreview(List<Vector3> approxCenters)
    {
        for (int i = 0; i < approxCenters.Count; i++)
        {
            GameObject cell = GetOrCreateDragPreviewCell(i);
            bool isGround = TryGetGroundCell(approxCenters[i], out Vector3 groundPoint);
            bool valid = isGround && !IsCellBlocked(groundPoint);

            cell.transform.position = groundPoint + Vector3.up * plotYOffset;
            cell.transform.rotation = Quaternion.Euler(0f, dragRotationDegrees, 0f);
            cell.SetActive(true);

            Renderer cellRenderer = cell.GetComponentInChildren<Renderer>();
            MaterialColorHelper.SetColor(cellRenderer, valid ? validColor : invalidColor);
        }

        for (int i = approxCenters.Count; i < dragPreviewPool.Count; i++)
        {
            if (dragPreviewPool[i] != null)
            {
                dragPreviewPool[i].SetActive(false);
            }
        }
    }

    GameObject GetOrCreateDragPreviewCell(int index)
    {
        while (dragPreviewPool.Count <= index)
        {
            GameObject clone = Instantiate(plotPreviewObject, plotPreviewObject.transform.parent);
            clone.name = "DragPreviewCell_" + dragPreviewPool.Count;
            clone.SetActive(false);
            dragPreviewPool.Add(clone);
        }
        return dragPreviewPool[index];
    }

    void HideDragPreview()
    {
        foreach (GameObject cell in dragPreviewPool)
        {
            if (cell != null)
            {
                cell.SetActive(false);
            }
        }
    }

    // Places a real GroundPlot on every cell that's actually clear soil, and silently skips the
    // rest (blocked by an obstacle, another plot, or just not solid ground) -- one bad cell in a
    // big drag shouldn't throw away the whole patch.
    //
    // Validation happens as its own pass over ALL cells BEFORE any plot is instantiated. Doing
    // the check-then-place in a single combined pass would make each newly placed plot block its
    // own neighbors: adjacent tile centers are exactly one tile-width apart, so a plot's collider
    // reaches right up to (and touches) the next cell's center, and IsCellBlocked's plotLayer
    // check would then see that brand-new neighbor and wrongly skip the cell next to it. Checking
    // everything against the pre-drag world first avoids that entirely.
    void FinalizeDrag(List<Vector3> approxCenters)
    {
        List<Vector3> validSpawnPositions = new List<Vector3>();

        foreach (Vector3 approx in approxCenters)
        {
            bool isGround = TryGetGroundCell(approx, out Vector3 groundPoint);
            if (!isGround || IsCellBlocked(groundPoint))
            {
                continue;
            }

            validSpawnPositions.Add(groundPoint + Vector3.up * plotYOffset);
        }

        Quaternion spawnRotation = Quaternion.Euler(0f, dragRotationDegrees, 0f);
        foreach (Vector3 spawnPos in validSpawnPositions)
        {
            Instantiate(groundPlotPrefab, spawnPos, spawnRotation);
        }

        if (validSpawnPositions.Count == 0)
        {
            Debug.Log("BuildPlantController: drag placement produced no valid plots (all cells were blocked or not soil).");
        }
        else
        {
            Debug.Log("BuildPlantController: placed " + validSpawnPositions.Count + " / " + approxCenters.Count + " plots from drag.");
        }
    }

    void HandleHoeOnExistingPlot(RaycastHit hit)
    {
        GroundPlot plot = hit.collider.GetComponent<GroundPlot>();

        if (plot == null || !plot.IsDead)
        {
            ClearHover();
            return;
        }

        UpdateHover(plot);

        if (InteractPressed())
        {
            plot.Uproot();
            ClearHover();
        }
    }

    void HandleSeedMode()
    {
        Ray ray = GetInteractionRay();
        GroundPlot targetPlot = FindEligiblePlot(ray, p => p.IsEmpty);

        if (targetPlot != null && InteractPressed())
        {
            bool used = EquipmentManager.Instance.UseSeed(EquipmentManager.Instance.CurrentSeed);
            if (used)
            {
                targetPlot.Plant(EquipmentManager.Instance.CurrentSeed);
                targetPlot = null;
            }
        }

        UpdateHover(targetPlot);
    }

    void HandleWateringMode()
    {
        Ray ray = GetInteractionRay();
        GroundPlot targetPlot = FindEligiblePlot(ray, p => !p.IsEmpty && !p.IsDead);

        if (targetPlot != null && InteractPressed())
        {
            targetPlot.Water();
        }

        UpdateHover(targetPlot);
    }

    void HandleHarvestMode()
    {
        Ray ray = GetInteractionRay();
        GroundPlot targetPlot = FindEligiblePlot(ray, p => p.IsReadyToHarvest);

        if (targetPlot == null)
        {
            // Deliberately do NOT call UpdateHover(null) here. This runs unconditionally every
            // frame after whatever tool mode is currently equipped (see Update()), so if we always
            // cleared the hover here, it would immediately wipe out a highlight that mode just set
            // for its own purpose this same frame (e.g. Hoe mode highlighting a dead plot as
            // uprootable) -- that was the cause of the "yellow highlight disappears" bug. When
            // there's nothing to harvest, just leave whatever hover the active mode already decided
            // on alone.
            return;
        }

        UpdateHover(targetPlot);

        if (HarvestPressed())
        {
            targetPlot.Harvest();
            // The plot is empty now -- clear its highlight explicitly. Otherwise, since a null
            // result above deliberately leaves the existing hover alone (see the comment there),
            // this now-empty plot would stay lit up indefinitely with nothing to correct it.
            ClearHover();
        }
    }

    void UpdateHover(GroundPlot newTarget)
    {
        if (newTarget == currentHoveredPlot)
        {
            return;
        }

        if (currentHoveredPlot != null)
        {
            currentHoveredPlot.SetHighlight(false);
        }

        if (newTarget != null)
        {
            newTarget.SetHighlight(true);
        }

        currentHoveredPlot = newTarget;
    }

    void ClearHover()
    {
        UpdateHover(null);
    }

    void SetPreviewActive(bool active)
    {
        if (plotPreviewObject != null && plotPreviewObject.activeSelf != active)
        {
            plotPreviewObject.SetActive(active);
        }
    }
}
