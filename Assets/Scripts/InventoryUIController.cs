using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Renders EquipmentManager's slot grid as a fullscreen, Minecraft-style grid of clickable
// slots. Everything (grid, locked-slot padlock icon) is built at runtime from primitive UI
// components -- no prefab, sprite asset, or manual scene wiring is required.
// On Start() it also removes whatever old child UI (buttons/labels) was previously placed
// under inventoryPanel, so only the new grid + the existing status texts remain.
// Slot unlocking is intentionally NOT controllable from here -- it is purchased from
// ShopUIController only, so there is a single place in the game where capacity is bought.
public class InventoryUIController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject inventoryPanel;
    [Tooltip("Parent that will hold the slot grid. Leave empty to auto-create one under inventoryPanel.")]
    public RectTransform slotContainer;

    [Header("Behavior")]
    [Tooltip("If true, opening the inventory blocks player movement/look, like the Book and Shop do. Off by default so the player can keep walking around while managing items. Either way, the cursor always unlocks while open so the slot buttons are clickable.")]
    public bool blocksMovementWhileOpen = false;

    [Header("Slot Visuals")]
    [Tooltip("Slot cells are auto-sized at runtime to fill slotContainer as much as possible; this is only used as a floor so slots never shrink below a usable size on very small containers.")]
    public Vector2 minSlotSize = new Vector2(64f, 64f);
    public Vector2 slotSpacing = new Vector2(16f, 16f);
    public int columns = 9;
    public Color emptySlotColor = new Color(1f, 1f, 1f, 0.15f);
    public Color filledSlotColor = new Color(1f, 1f, 1f, 0.35f);
    [Tooltip("Tint for slots holding a harvested crop (kept distinct from tool/seed slots since crops are sellable, not equippable).")]
    public Color cropSlotColor = new Color(0.85f, 0.55f, 0.2f, 0.55f);
    public Color equippedSlotColor = new Color(1f, 0.85f, 0.35f, 0.85f);
    public Color lockedSlotColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
    [Tooltip("Optional padlock sprite for locked slots. If left empty, a simple padlock shape is drawn from plain UI rectangles instead (no font glyph / sprite asset needed).")]
    public Sprite lockIcon;
    [Tooltip("Alpha the inventory panel's own background is forced to, so it reads as a thin overlay instead of a solid box. Its RGB color is left as whatever it already is.")]
    [Range(0f, 1f)]
    public float panelBackgroundAlpha = 0.35f;

    [Header("Status Display")]
    public TextMeshProUGUI equippedText;

    [Header("Currency")]
    public TextMeshProUGUI coinsText;

    [Header("Book Shortcut")]
    [Tooltip("Optional. Lets the player open the plant book directly from the Inventory panel via a dedicated button, without it taking up an inventory slot.")]
    public BookUIController bookUI;

    // Hidden until the player actually picks up the physical book prop in the house
    // (BookCollectionManager.HasBook) -- see CreateBookShortcutButton / HandleBookCollected.
    private GameObject bookShortcutButtonGO;

    private struct SlotUI
    {
        public Image background;
        public Button button;
        public TextMeshProUGUI label;
        public TextMeshProUGUI countText;
        public Image lockSpriteImage;
        public GameObject composedLock;
    }

    private SlotUI[] slotUIs;
    private bool isOpen = false;
    private Vector2 currentCellSize;

    [Header("VR Input")]
    [Tooltip("Optional. Assign a controller button (e.g. the left controller's Menu/Primary button from XRI Default Input Actions) to toggle the inventory in VR. The Tab/I keyboard shortcut below still works as a harmless desktop-testing fallback either way.")]
    public UnityEngine.InputSystem.InputActionReference toggleInventoryAction;

    void Start()
    {
        inventoryPanel.SetActive(false);

        CleanupLegacyChildren();
        StylePanelFullscreen();
        EnsureSlotContainer();
        BuildSlotGrid();

        CreateBookShortcutButton();

        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.OnEquipmentChanged += RefreshUI;
        }
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged += HandleCoinsChanged;
        }
        if (BookCollectionManager.Instance != null)
        {
            BookCollectionManager.Instance.OnBookCollected += HandleBookCollected;
        }

        RefreshUI();
    }

    void Update()
    {
        bool toggleKeyPressed = Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I);
        bool toggleButtonPressed = toggleInventoryAction != null && toggleInventoryAction.action != null
            && toggleInventoryAction.action.WasPressedThisFrame();

        if (!toggleKeyPressed && !toggleButtonPressed)
        {
            return;
        }

        if (isOpen)
        {
            CloseInventory();
        }
        else if (UIStateManager.Instance != null && !UIStateManager.Instance.IsAnyUIOpen)
        {
            OpenInventory();
        }
    }

    void OpenInventory()
    {
        isOpen = true;
        inventoryPanel.SetActive(true);

        if (UIStateManager.Instance != null)
        {
            if (blocksMovementWhileOpen)
            {
                UIStateManager.Instance.RegisterUIOpened();
            }
            else
            {
                // Movement stays on, but the cursor still needs to unlock -- otherwise it's stuck
                // locked to screen-center from first-person look, and the slot buttons can never
                // actually receive a click.
                UIStateManager.Instance.RegisterCursorNeeded();
            }
        }

        RefreshUI();
    }

    void CloseInventory()
    {
        isOpen = false;
        inventoryPanel.SetActive(false);

        if (UIStateManager.Instance != null)
        {
            if (blocksMovementWhileOpen)
            {
                UIStateManager.Instance.RegisterUIClosed();
            }
            else
            {
                UIStateManager.Instance.RegisterCursorReleased();
            }
        }
    }

    void HandleCoinsChanged(int newAmount)
    {
        RefreshUI();
    }

    // --- One-time cleanup of whatever old UI used to live under inventoryPanel ---

    void CleanupLegacyChildren()
    {
        HashSet<Transform> keep = new HashSet<Transform>();
        AddAncestorChain(equippedText != null ? equippedText.transform : null, keep);
        AddAncestorChain(coinsText != null ? coinsText.transform : null, keep);

        List<Transform> toDestroy = new List<Transform>();
        foreach (Transform child in inventoryPanel.transform)
        {
            if (!keep.Contains(child))
            {
                toDestroy.Add(child);
            }
        }

        foreach (Transform t in toDestroy)
        {
            Destroy(t.gameObject);
        }
    }

    void AddAncestorChain(Transform leaf, HashSet<Transform> keep)
    {
        Transform current = leaf;
        while (current != null && current != inventoryPanel.transform)
        {
            keep.Add(current);
            current = current.parent;
        }
    }

    void StylePanelFullscreen()
    {
        RectTransform panelRect = inventoryPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }

        Image panelImage = inventoryPanel.GetComponent<Image>();
        if (panelImage == null)
        {
            // The panel itself had no background image (the old dark box was likely a
            // separate child that CleanupLegacyChildren just removed) -- add a plain one so
            // the fullscreen panel still reads as a thin overlay instead of being invisible.
            panelImage = inventoryPanel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 1f);
        }

        Color c = panelImage.color;
        panelImage.color = new Color(c.r, c.g, c.b, panelBackgroundAlpha);
    }

    // --- Runtime grid construction ---

    void EnsureSlotContainer()
    {
        if (slotContainer != null)
        {
            return;
        }

        GameObject containerGO = new GameObject("SlotGrid", typeof(RectTransform));
        containerGO.transform.SetParent(inventoryPanel.transform, false);
        slotContainer = containerGO.GetComponent<RectTransform>();
        slotContainer.anchorMin = new Vector2(0.03f, 0.05f);
        slotContainer.anchorMax = new Vector2(0.97f, 0.82f);
        slotContainer.offsetMin = Vector2.zero;
        slotContainer.offsetMax = Vector2.zero;
    }

    void BuildSlotGrid()
    {
        if (EquipmentManager.Instance == null)
        {
            slotUIs = new SlotUI[0];
            return;
        }

        GridLayoutGroup grid = slotContainer.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = slotContainer.gameObject.AddComponent<GridLayoutGroup>();
        }
        grid.spacing = slotSpacing;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);

        int count = EquipmentManager.Instance.MaxCapacity;
        slotUIs = new SlotUI[count];

        // Size each slot to actually fill slotContainer, instead of using a fixed pixel size --
        // a fixed cellSize leaves the grid looking tiny inside a large fullscreen container.
        // Force a layout pass first so slotContainer.rect reflects its real screen-space size.
        Canvas.ForceUpdateCanvases();
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / Mathf.Max(1, columns)));
        float availableWidth = slotContainer.rect.width;
        float availableHeight = slotContainer.rect.height;
        float cellWidth = (availableWidth - slotSpacing.x * (Mathf.Max(1, columns) - 1)) / Mathf.Max(1, columns);
        float cellHeight = (availableHeight - slotSpacing.y * (rows - 1)) / rows;
        float cellDimension = Mathf.Max(minSlotSize.x, Mathf.Min(cellWidth, cellHeight));
        currentCellSize = new Vector2(cellDimension, cellDimension);
        grid.cellSize = currentCellSize;

        for (int i = 0; i < count; i++)
        {
            slotUIs[i] = CreateSlot(i);
        }
    }

    SlotUI CreateSlot(int index)
    {
        GameObject root = new GameObject("Slot_" + index, typeof(RectTransform));
        root.transform.SetParent(slotContainer, false);

        Image background = root.AddComponent<Image>();
        background.color = emptySlotColor;

        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;

        // Click-to-equip and press-and-hold-to-drag-and-reorder both go through this one handler
        // (see InventorySlotHandler below) instead of Button.onClick, so a drag gesture never also
        // fires a click on release.
        InventorySlotHandler dragHandler = root.AddComponent<InventorySlotHandler>();
        dragHandler.controller = this;
        dragHandler.slotIndex = index;

        float labelFontSize = Mathf.Clamp(currentCellSize.x * 0.16f, 15f, 46f);
        float countFontSize = Mathf.Clamp(currentCellSize.x * 0.19f, 16f, 52f);

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(root.transform, false);
        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.fontSize = labelFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.enableWordWrapping = true;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = labelFontSize;
        StretchInset(labelGO.GetComponent<RectTransform>(), 0.08f);

        GameObject countGO = new GameObject("Count", typeof(RectTransform));
        countGO.transform.SetParent(root.transform, false);
        TextMeshProUGUI countText = countGO.AddComponent<TextMeshProUGUI>();
        countText.fontSize = countFontSize;
        countText.alignment = TextAlignmentOptions.BottomRight;
        countText.color = Color.white;
        StretchInset(countGO.GetComponent<RectTransform>(), 0.05f);

        GameObject lockSpriteGO = new GameObject("LockSprite", typeof(RectTransform));
        lockSpriteGO.transform.SetParent(root.transform, false);
        StretchFull(lockSpriteGO.GetComponent<RectTransform>());
        Image lockSpriteImage = lockSpriteGO.AddComponent<Image>();
        lockSpriteImage.enabled = false;

        GameObject composedLock = BuildLockIcon(root.transform);
        composedLock.SetActive(false);

        return new SlotUI
        {
            background = background,
            button = button,
            label = label,
            countText = countText,
            lockSpriteImage = lockSpriteImage,
            composedLock = composedLock
        };
    }

    // Draws a simple padlock (a body rectangle + a U-shaped shackle + a keyhole dot) out of
    // plain colored UI rectangles, so a locked slot always shows a recognizable padlock even
    // though the project has no padlock sprite/font-glyph support yet.
    GameObject BuildLockIcon(Transform parent)
    {
        GameObject root = new GameObject("LockIconComposed", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        StretchInset(root.GetComponent<RectTransform>(), 0.2f);

        Color shackleColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        Color bodyColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        Color keyholeColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        CreateIconRect(root.transform, "ShackleLeft", new Vector2(0.22f, 0.42f), new Vector2(0.36f, 0.85f), shackleColor);
        CreateIconRect(root.transform, "ShackleRight", new Vector2(0.64f, 0.42f), new Vector2(0.78f, 0.85f), shackleColor);
        CreateIconRect(root.transform, "ShackleTop", new Vector2(0.22f, 0.72f), new Vector2(0.78f, 0.88f), shackleColor);
        CreateIconRect(root.transform, "Body", new Vector2(0.08f, 0f), new Vector2(0.92f, 0.55f), bodyColor);
        CreateIconRect(root.transform, "Keyhole", new Vector2(0.42f, 0.14f), new Vector2(0.58f, 0.34f), keyholeColor);

        return root;
    }

    static void CreateIconRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = go.AddComponent<Image>();
        image.color = color;
    }

    static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void StretchInset(RectTransform rect, float inset)
    {
        rect.anchorMin = new Vector2(inset, inset);
        rect.anchorMax = new Vector2(1f - inset, 1f - inset);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void CreateBookShortcutButton()
    {
        if (bookUI == null)
        {
            return;
        }

        GameObject btnGO = new GameObject("BookShortcutButton", typeof(RectTransform));
        btnGO.transform.SetParent(inventoryPanel.transform, false);
        btnGO.transform.SetAsLastSibling();

        RectTransform rect = btnGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.84f, 0.88f);
        rect.anchorMax = new Vector2(0.97f, 0.98f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = btnGO.AddComponent<Image>();
        bg.color = new Color(0.5f, 0.32f, 0.14f, 0.9f);

        Button button = btnGO.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(OpenBookFromInventory);

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(btnGO.transform, false);
        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = "Book";
        label.fontSize = 22f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 22f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        StretchInset(labelGO.GetComponent<RectTransform>(), 0.1f);

        bookShortcutButtonGO = btnGO;

        // Stays hidden until the player actually picks up the physical book in the house.
        bool alreadyHasBook = BookCollectionManager.Instance != null && BookCollectionManager.Instance.HasBook;
        bookShortcutButtonGO.SetActive(alreadyHasBook);
    }

    void HandleBookCollected()
    {
        if (bookShortcutButtonGO != null)
        {
            bookShortcutButtonGO.SetActive(true);
        }
    }

    void OpenBookFromInventory()
    {
        if (bookUI == null)
        {
            return;
        }

        if (UIStateManager.Instance != null && UIStateManager.Instance.IsAnyUIOpen)
        {
            return;
        }

        bookUI.OpenBook();
    }

    // --- Interaction ---

    public void HandleSlotClick(int index)
    {
        if (EquipmentManager.Instance == null || !EquipmentManager.Instance.IsSlotUnlocked(index))
        {
            return;
        }

        // Harvested crop stacks are not equippable/plantable -- clicking one is a no-op here
        // (EquipmentManager.EquipSlot already guards this too; this just avoids the call).
        InventorySlot clicked = EquipmentManager.Instance.GetSlot(index);
        if (clicked.isCrop)
        {
            return;
        }

        EquipmentManager.Instance.EquipSlot(index);
    }

    // --- Press-and-hold drag reordering ---
    //
    // Dragging one slot onto another swaps their contents (tool <-> seed stack <-> crop stack,
    // whatever each holds) via EquipmentManager.SwapSlots. A floating "ghost" copy of the slot
    // (background tint + label + count) follows the cursor while dragging so there's always
    // visual feedback, even though the project has no icon/sprite assets yet.

    private int dragSourceIndex = -1;
    private RectTransform dragGhost;

    public void BeginSlotDrag(int index, PointerEventData eventData)
    {
        if (EquipmentManager.Instance == null || !EquipmentManager.Instance.IsSlotUnlocked(index))
        {
            return;
        }

        InventorySlot slot = EquipmentManager.Instance.GetSlot(index);
        bool hasItem = slot.isTool || slot.isCrop || slot.seed != SeedType.None;
        if (!hasItem)
        {
            return;
        }

        dragSourceIndex = index;
        CreateDragGhost(slotUIs[index]);
        UpdateSlotDrag(eventData);
    }

    public void UpdateSlotDrag(PointerEventData eventData)
    {
        if (dragGhost == null)
        {
            return;
        }

        dragGhost.position = eventData.position;
    }

    public void EndSlotDrag()
    {
        dragSourceIndex = -1;

        if (dragGhost != null)
        {
            Destroy(dragGhost.gameObject);
            dragGhost = null;
        }
    }

    public void HandleSlotDrop(int fromIndex, int toIndex)
    {
        if (EquipmentManager.Instance == null || fromIndex == toIndex)
        {
            return;
        }

        if (!EquipmentManager.Instance.IsSlotUnlocked(fromIndex) || !EquipmentManager.Instance.IsSlotUnlocked(toIndex))
        {
            return;
        }

        EquipmentManager.Instance.SwapSlots(fromIndex, toIndex);
    }

    void CreateDragGhost(SlotUI sourceUI)
    {
        GameObject ghostGO = new GameObject("DragGhost", typeof(RectTransform));
        // Parented under the same panel as the grid (so it's on the same Canvas) and pushed to
        // the last sibling so it always draws on top of every slot cell while dragging.
        ghostGO.transform.SetParent(inventoryPanel.transform, false);
        ghostGO.transform.SetAsLastSibling();

        dragGhost = ghostGO.GetComponent<RectTransform>();
        dragGhost.sizeDelta = currentCellSize;
        dragGhost.pivot = new Vector2(0.5f, 0.5f);

        CanvasGroup cg = ghostGO.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; // never itself becomes a raycast/drop target
        cg.alpha = 0.85f;

        Image bg = ghostGO.AddComponent<Image>();
        bg.color = sourceUI.background.color;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(ghostGO.transform, false);
        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = sourceUI.label.text;
        label.fontSize = sourceUI.label.fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        StretchInset(labelGO.GetComponent<RectTransform>(), 0.08f);

        GameObject countGO = new GameObject("Count", typeof(RectTransform));
        countGO.transform.SetParent(ghostGO.transform, false);
        TextMeshProUGUI count = countGO.AddComponent<TextMeshProUGUI>();
        count.text = sourceUI.countText.text;
        count.fontSize = sourceUI.countText.fontSize;
        count.alignment = TextAlignmentOptions.BottomRight;
        count.color = Color.white;
        StretchInset(countGO.GetComponent<RectTransform>(), 0.05f);
    }

    // --- Refresh ---

    void RefreshUI()
    {
        if (EquipmentManager.Instance == null || slotUIs == null)
        {
            return;
        }

        for (int i = 0; i < slotUIs.Length; i++)
        {
            RefreshSlot(i);
        }

        RefreshEquippedLabel();

        if (CurrencyManager.Instance != null && coinsText != null)
        {
            coinsText.text = CurrencyManager.Instance.CurrentCoins + " coins";
        }
    }

    void RefreshSlot(int index)
    {
        SlotUI ui = slotUIs[index];
        bool unlocked = EquipmentManager.Instance.IsSlotUnlocked(index);

        if (!unlocked)
        {
            ui.background.color = lockedSlotColor;
            ui.button.interactable = false;
            ui.label.text = "";
            ui.countText.text = "";

            if (lockIcon != null)
            {
                ui.lockSpriteImage.enabled = true;
                ui.lockSpriteImage.sprite = lockIcon;
                ui.composedLock.SetActive(false);
            }
            else
            {
                ui.lockSpriteImage.enabled = false;
                ui.composedLock.SetActive(true);
            }
            return;
        }

        ui.lockSpriteImage.enabled = false;
        ui.composedLock.SetActive(false);
        ui.button.interactable = true;

        InventorySlot slot = EquipmentManager.Instance.GetSlot(index);
        bool hasItem = slot.isTool || slot.isCrop || slot.seed != SeedType.None;
        bool isEquipped = IsSlotCurrentlyEquipped(slot);

        if (isEquipped)
        {
            ui.background.color = equippedSlotColor;
        }
        else if (slot.isCrop)
        {
            ui.background.color = cropSlotColor;
        }
        else
        {
            ui.background.color = hasItem ? filledSlotColor : emptySlotColor;
        }

        if (slot.isTool)
        {
            ui.label.text = slot.tool == ToolType.Hoe ? "Hoe" : "Watering Can";
            ui.countText.text = "";
        }
        else if (slot.isCrop)
        {
            // Harvested crop stack -- sellable only, not equippable/plantable. The reduced-
            // quality flag (set when the plant took Critical damage before harvest) is shown
            // right on the slot since it changes the sell price.
            ui.label.text = slot.seed.ToString() + (slot.reducedQuality ? "\n(Reduced)" : "");
            ui.countText.text = slot.count.ToString();
        }
        else if (slot.seed != SeedType.None)
        {
            ui.label.text = slot.seed.ToString();
            ui.countText.text = slot.count.ToString();
        }
        else
        {
            ui.label.text = "";
            ui.countText.text = "";
        }
    }

    bool IsSlotCurrentlyEquipped(InventorySlot slot)
    {
        // Crop stacks are never equipped -- without this, a harvested crop slot could
        // false-positive as "equipped" whenever a real seed stack of the same SeedType
        // happens to be equipped elsewhere in the grid, since both share the `seed` field.
        if (slot.isCrop)
        {
            return false;
        }

        EquippedType equipped = EquipmentManager.Instance.CurrentEquipped;

        if (slot.isTool)
        {
            return (slot.tool == ToolType.Hoe && equipped == EquippedType.Hoe)
                || (slot.tool == ToolType.WateringCan && equipped == EquippedType.WateringCan);
        }

        return equipped == EquippedType.Seed && slot.seed == EquipmentManager.Instance.CurrentSeed;
    }

    void RefreshEquippedLabel()
    {
        if (equippedText == null)
        {
            return;
        }

        string equippedLabel = "None";
        switch (EquipmentManager.Instance.CurrentEquipped)
        {
            case EquippedType.Hoe:
                equippedLabel = "Hoe";
                break;
            case EquippedType.WateringCan:
                equippedLabel = "Watering Can";
                break;
            case EquippedType.Seed:
                equippedLabel = "Seed: " + EquipmentManager.Instance.CurrentSeed;
                break;
        }
        equippedText.text = "Equipped: " + equippedLabel;
    }

    void OnDestroy()
    {
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.OnEquipmentChanged -= RefreshUI;
        }
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
        }
        if (BookCollectionManager.Instance != null)
        {
            BookCollectionManager.Instance.OnBookCollected -= HandleBookCollected;
        }
    }
}

// Per-slot pointer handler: a plain click equips/uses the slot, while a press-and-hold-then-move
// gesture drags its contents onto another slot to swap/reorder them. Kept as its own small
// component (one per slot GameObject, created in InventoryUIController.CreateSlot) so each slot
// carries its own index and forwards every gesture back to the controller, which owns the actual
// grid data and visuals.
public class InventorySlotHandler : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public InventoryUIController controller;
    public int slotIndex;

    public void OnPointerClick(PointerEventData eventData)
    {
        // eventData.dragging is true once OnBeginDrag has fired for this same press-release
        // gesture -- checking it here is what stops a drag from also equipping the slot on release.
        if (eventData.dragging || controller == null)
        {
            return;
        }

        controller.HandleSlotClick(slotIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller != null)
        {
            controller.BeginSlotDrag(slotIndex, eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (controller != null)
        {
            controller.UpdateSlotDrag(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (controller != null)
        {
            controller.EndSlotDrag();
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (controller == null || eventData.pointerDrag == null)
        {
            return;
        }

        InventorySlotHandler source = eventData.pointerDrag.GetComponent<InventorySlotHandler>();
        if (source != null)
        {
            controller.HandleSlotDrop(source.slotIndex, slotIndex);
        }
    }
}
