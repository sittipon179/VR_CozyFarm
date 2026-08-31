using System;
using UnityEngine;

// Tracks whether the player has picked up the physical encyclopedia/book prop from the house yet.
// The book itself is never a real inventory slot item -- picking it up just flips this flag once,
// which InventoryUIController listens for to reveal its dedicated (non-slot) Book shortcut icon.
// See BookInteractable (on the InteriorTableBook prop) for where CollectBook() is called.
public class BookCollectionManager : MonoBehaviour
{
    public static BookCollectionManager Instance { get; private set; }

    public bool HasBook { get; private set; } = false;

    public event Action OnBookCollected;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void CollectBook()
    {
        if (HasBook)
        {
            return;
        }

        HasBook = true;
        OnBookCollected?.Invoke();
    }
}
