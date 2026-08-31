using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Pickup interaction for the physical book prop on the table in the house. Selecting it once
// ""collects"" the encyclopedia: the physical prop disappears from the world, BookCollectionManager
// marks it as owned (revealing the dedicated Book shortcut icon in the Inventory panel -- see
// InventoryUIController), and the book opens once immediately as pickup confirmation. The book is
// never a real inventory slot item, so after this the encyclopedia is only reachable from that
// shortcut icon.
[RequireComponent(typeof(XRSimpleInteractable))]
public class BookInteractable : MonoBehaviour
{
    [Header("References")]
    public BookUIController bookUI;

    private XRSimpleInteractable _interactable;
    private bool _collected;

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
        if (_collected)
        {
            return;
        }

        if (UIStateManager.Instance != null && UIStateManager.Instance.IsAnyUIOpen)
        {
            return;
        }

        _collected = true;

        if (BookCollectionManager.Instance != null)
        {
            BookCollectionManager.Instance.CollectBook();
        }

        if (bookUI != null)
        {
            bookUI.OpenBook();
        }

        gameObject.SetActive(false);
    }
}
