using UnityEngine;

// Shows a held 3D model of the currently equipped tool (Hoe / Watering Can) attached to the
// right controller's visual model, so equipping a tool from the Inventory actually looks like
// picking something up, instead of the hand staying empty. Hidden again for Seed/None -- seeds
// and bare hands don't get a held prop, the player just points and clicks/presses.
//
// Each tool gets its own placement offset (position/rotation/scale) since the two source
// models are very different shapes (a long-handled hoe vs. a compact watering can) and need
// different tuning to look natural in the hand -- a single shared offset can't fit both.
//
// Both prefabs are instantiated once at Start() as children of handAttachPoint and simply
// toggled active/inactive on EquipmentManager.OnEquipmentChanged, rather than instantiated and
// destroyed per equip -- cheaper, and avoids a one-frame pop when switching tools.
[System.Serializable]
public class HeldToolPlacement
{
    public GameObject prefab;
    public Vector3 localPositionOffset = Vector3.zero;
    [Tooltip("Local rotation offset in Euler degrees.")]
    public Vector3 localEulerOffset = Vector3.zero;
    public float localScale = 1f;
}

public class EquippedToolVisual : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform the held tool models are parented to, e.g. the right controller's " +
        "visual model (XR Origin Hands (XR Rig)/Camera Offset/Right Controller/Right Controller Visual).")]
    public Transform handAttachPoint;

    [Header("Hoe")]
    public HeldToolPlacement hoe = new HeldToolPlacement();

    [Header("Watering Can")]
    public HeldToolPlacement wateringCan = new HeldToolPlacement();

    private GameObject hoeInstance;
    private GameObject wateringCanInstance;

    void Start()
    {
        if (handAttachPoint != null)
        {
            if (hoe.prefab != null)
            {
                hoeInstance = SpawnAttached(hoe);
            }
            if (wateringCan.prefab != null)
            {
                wateringCanInstance = SpawnAttached(wateringCan);
            }
        }

        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.OnEquipmentChanged += Refresh;
        }

        Refresh();
    }

    GameObject SpawnAttached(HeldToolPlacement placement)
    {
        GameObject instance = Instantiate(placement.prefab, handAttachPoint);
        instance.transform.localPosition = placement.localPositionOffset;
        instance.transform.localEulerAngles = placement.localEulerOffset;
        instance.transform.localScale = Vector3.one * placement.localScale;

        // Held visual props aren't physical/interactive -- strip colliders so they can never
        // block the player's own interaction raycasts or physically shove other objects.
        foreach (Collider col in instance.GetComponentsInChildren<Collider>())
        {
            Destroy(col);
        }

        instance.SetActive(false);
        return instance;
    }

    void Refresh()
    {
        if (EquipmentManager.Instance == null)
        {
            return;
        }

        EquippedType equipped = EquipmentManager.Instance.CurrentEquipped;

        if (hoeInstance != null)
        {
            hoeInstance.SetActive(equipped == EquippedType.Hoe);
        }
        if (wateringCanInstance != null)
        {
            wateringCanInstance.SetActive(equipped == EquippedType.WateringCan);
        }
    }

    void OnDestroy()
    {
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.OnEquipmentChanged -= Refresh;
        }
    }
}
