using UnityEngine;

public class BuildPlantController : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public GameObject plotPreviewObject;
    public GameObject groundPlotPrefab;

    [Header("Layers")]
    public LayerMask groundLayer;
    public LayerMask obstacleLayer;
    public LayerMask plotLayer;

    [Header("Placement Settings")]
    public float maxInteractDistance = 10f;
    public float plotCheckRadius = 0.5f;
    public float plotYOffset = 0.05f;

    [Header("Preview Colors")]
    public Color validColor = new Color(0.3f, 1f, 0.3f, 0.6f);
    public Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.6f);

    private Renderer previewRenderer;
    private bool isValidPlacement;
    private GroundPlot currentHoveredPlot;

    void Start()
    {
        if (plotPreviewObject != null)
        {
            previewRenderer = plotPreviewObject.GetComponentInChildren<Renderer>();
            plotPreviewObject.SetActive(false);
        }
    }

    void Update()
    {
        if (EquipmentManager.Instance == null)
        {
            return;
        }

        if (UIStateManager.Instance != null && UIStateManager.Instance.IsAnyUIOpen)
        {
            SetPreviewActive(false);
            ClearHover();
            return;
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
                HandleHarvestMode();
                break;
            default:
                SetPreviewActive(false);
                ClearHover();
                break;
        }
    }

    void HandleHoeMode()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        int combinedMask = groundLayer | obstacleLayer | plotLayer;

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

        bool blocked = Physics.CheckSphere(hit.point, plotCheckRadius, obstacleLayer)
                     || Physics.CheckSphere(hit.point, plotCheckRadius, plotLayer);
        isValidPlacement = !blocked;

        MaterialColorHelper.SetColor(previewRenderer, isValidPlacement ? validColor : invalidColor);

        if (isValidPlacement && Input.GetMouseButtonDown(0))
        {
            Instantiate(groundPlotPrefab, spawnPos, Quaternion.identity);
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

        if (Input.GetMouseButtonDown(0))
        {
            plot.Uproot();
            ClearHover();
        }
    }

    void HandleSeedMode()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        GroundPlot targetPlot = null;

        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractDistance, plotLayer))
        {
            GroundPlot plot = hit.collider.GetComponent<GroundPlot>();
            if (plot != null && plot.IsEmpty)
            {
                targetPlot = plot;

                if (Input.GetMouseButtonDown(0))
                {
                    bool used = EquipmentManager.Instance.UseSeed(EquipmentManager.Instance.CurrentSeed);
                    if (used)
                    {
                        plot.Plant(EquipmentManager.Instance.CurrentSeed);
                        targetPlot = null;
                    }
                }
            }
        }

        UpdateHover(targetPlot);
    }

    void HandleWateringMode()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        GroundPlot targetPlot = null;

        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractDistance, plotLayer))
        {
            GroundPlot plot = hit.collider.GetComponent<GroundPlot>();
            if (plot != null && !plot.IsEmpty && !plot.IsDead)
            {
                targetPlot = plot;

                if (Input.GetMouseButtonDown(0))
                {
                    plot.Water();
                }
            }
        }

        UpdateHover(targetPlot);
    }

    void HandleHarvestMode()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        GroundPlot targetPlot = null;

        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractDistance, plotLayer))
        {
            GroundPlot plot = hit.collider.GetComponent<GroundPlot>();
            if (plot != null && plot.IsReadyToHarvest)
            {
                targetPlot = plot;

                if (Input.GetMouseButtonDown(0))
                {
                    plot.Harvest();
                    targetPlot = null;
                }
            }
        }

        UpdateHover(targetPlot);
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