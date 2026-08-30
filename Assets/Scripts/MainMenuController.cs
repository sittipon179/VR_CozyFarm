using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;

    [Header("Main Menu Buttons")]
    public Button startGameButton;
    public Button settingsButton;
    public Button exitButton;

    [Header("Settings Panel")]
    public Button settingsCloseButton;

    [Header("Player Reference")]
    public FirstPersonController firstPersonController;

    void Start()
    {
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);

        startGameButton.onClick.AddListener(OnStartGame);
        settingsButton.onClick.AddListener(OnOpenSettings);
        exitButton.onClick.AddListener(OnExitGame);
        settingsCloseButton.onClick.AddListener(OnCloseSettings);

        if (firstPersonController != null)
        {
            firstPersonController.enabled = false;
        }

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.RegisterUIOpened();
        }
    }

    void OnStartGame()
    {
        mainMenuPanel.SetActive(false);

        if (firstPersonController != null)
        {
            firstPersonController.enabled = true;
        }

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.StartTime();
        }

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.RegisterUIClosed();
        }
    }

    void OnOpenSettings()
    {
        settingsPanel.SetActive(true);
    }

    void OnCloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    void OnExitGame()
    {
        Debug.Log("Exit Game requested");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}