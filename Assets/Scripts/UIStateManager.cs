using UnityEngine;

public class UIStateManager : MonoBehaviour
{
    public static UIStateManager Instance { get; private set; }

    private int openUICount = 0;

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

    public void RegisterUIOpened()
    {
        openUICount++;
        UpdateCursorState();
    }

    public void RegisterUIClosed()
    {
        openUICount = Mathf.Max(0, openUICount - 1);
        UpdateCursorState();
    }

    void UpdateCursorState()
    {
        if (IsAnyUIOpen)
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
}