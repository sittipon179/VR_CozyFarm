using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// VR conversion: this used to raycast from playerCamera on a mouse click. It now reacts to
// an XRSimpleInteractable's selectEntered event, which fires the same way whether the player
// pokes the bed up close or selects it at range with a ray interactor. Same gating logic as
// before (UIStateManager / SleepManager), just triggered by XRI instead of Input.GetMouseButtonDown.
[RequireComponent(typeof(XRSimpleInteractable))]
public class BedInteractable : MonoBehaviour
{
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

        if (SleepManager.Instance == null || !SleepManager.Instance.CanSleepNow)
        {
            return;
        }

        SleepManager.Instance.TryToSleep();
    }
}
