using UnityEngine;

public class UIStateManager : MonoBehaviour
{
    public static UIStateManager Instance { get; private set; }

    [Header("VR Locomotion (optional)")]
    [Tooltip("Locomotion providers (e.g. DynamicMoveProvider, ContinuousMoveProvider, " +
        "GrabMoveProvider, TeleportationProvider) that get disabled while any UI opened via " +
        "RegisterUIOpened is open (Shop, Book, and any panel with blocksMovementWhileOpen = " +
        "true, like Inventory when that flag is turned on) -- without this, the desktop-era " +
        "Cursor.lockState trick below did nothing to stop VR locomotion, so the player could " +
        "keep walking away with a panel open. Turn providers are deliberately left out of this " +
        "list so the player can still turn/look around with the joystick to aim at the UI while " +
        "movement is locked.")]
    public Behaviour[] moveProvidersToLock;

    private int openUICount = 0;
    private int cursorNeededCount = 0;

    public bool IsAnyUIOpen => openUICount > 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Blocks player movement AND unlocks the cursor. Use this for UI that should pause the
    // player while it's open (Shop, Book, and similar full-screen panels).
    public void RegisterUIOpened()
    {
        openUICount++;
        UpdateCursorState();
        UpdateLocomotionState();
    }

    public void RegisterUIClosed()
    {
        openUICount = Mathf.Max(0, openUICount - 1);
        UpdateCursorState();
        UpdateLocomotionState();
    }

    // Unlocks the cursor WITHOUT blocking player movement -- for UI (like the Inventory
    // grid used to be) that's meant to stay open while the player keeps walking around, but
    // still needs a real, positionable OS cursor for its own buttons to receive clicks. Kept as
    // a separate counter from openUICount so it never affects IsAnyUIOpen / movement blocking.
    public void RegisterCursorNeeded()
    {
        cursorNeededCount++;
        UpdateCursorState();
    }

    public void RegisterCursorReleased()
    {
        cursorNeededCount = Mathf.Max(0, cursorNeededCount - 1);
        UpdateCursorState();
    }

    void UpdateCursorState()
    {
        if (openUICount > 0 || cursorNeededCount > 0)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void UpdateLocomotionState()
    {
        if (moveProvidersToLock == null)
        {
            return;
        }

        bool locomotionLocked = openUICount > 0;
        foreach (Behaviour provider in moveProvidersToLock)
        {
            if (provider != null)
            {
                provider.enabled = !locomotionLocked;
            }
        }
    }
}
