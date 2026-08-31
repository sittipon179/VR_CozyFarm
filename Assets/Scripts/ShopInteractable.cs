using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// VR conversion: was a raycast-from-camera + mouse-click check (with an IsChildOf test so a
// click on any part of the stall counted). Now driven by XRSimpleInteractable.selectEntered --
// the interactable lives on the same GameObject the old raycast checked against
// (ShopCounter), so any poke/ray select on its collider already covers the "any part of the
// stall" intent as long as the collider covers the whole stall.
[RequireComponent(typeof(XRSimpleInteractable))]
public class ShopInteractable : MonoBehaviour
{
    [Header("References")]
    public ShopUIController shopUI;

    private XRSimpleInteractable _interactable;

    void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
    }

    void OnEnable()
    {
        if (_interactable != null)
        {
            _interactable.selectEntered.AddListener(OnSelectEntered);
        }
    }

    void OnDisable()
    {
        if (_interactable != null)
        {
            _interactable.selectEntered.RemoveListener(OnSelectEntered);
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (UIStateManager.Instance != null && UIStateManager.Instance.IsAnyUIOpen)
        {
            return;
        }

        if (shopUI == null)
        {
            return;
        }

        shopUI.OpenShop();
    }
}
