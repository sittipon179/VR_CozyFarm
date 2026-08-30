using UnityEngine;

public class ScreenFadeController : MonoBehaviour
{
    public static ScreenFadeController Instance { get; private set; }

    [Header("Fade Image (solid black, used for sleep transitions)")]
    public CanvasGroup fadeCanvasGroup;

    [Header("Fatigue Overlay (dark warning before forced pass out)")]
    public CanvasGroup fatigueCanvasGroup;

    [Header("Fade Speed")]
    [Tooltip("How fast alpha moves toward its target, in alpha units per second.")]
    public float fadeSpeed = 1.5f;

    [Header("Fatigue Overlay Settings")]
    [Tooltip("Maximum alpha the fatigue overlay reaches right before a forced pass out.")]
    [Range(0f, 1f)]
    public float maxFatigueAlpha = 0.55f;

    public float CurrentFadeAlpha => fadeCanvasGroup != null ? fadeCanvasGroup.alpha : 0f;
    public float CurrentFatigueAlpha => fatigueCanvasGroup != null ? fatigueCanvasGroup.alpha : 0f;

    private float targetFadeAlpha = 0f;
    private float targetFatigueAlpha = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
        }

        if (fatigueCanvasGroup != null)
        {
            fatigueCanvasGroup.alpha = 0f;
            fatigueCanvasGroup.blocksRaycasts = false;
            fatigueCanvasGroup.interactable = false;
        }
    }

    void Update()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = Mathf.MoveTowards(fadeCanvasGroup.alpha, targetFadeAlpha, fadeSpeed * Time.deltaTime);
            fadeCanvasGroup.blocksRaycasts = fadeCanvasGroup.alpha > 0.99f;
        }

        if (fatigueCanvasGroup != null)
        {
            fatigueCanvasGroup.alpha = Mathf.MoveTowards(fatigueCanvasGroup.alpha, targetFatigueAlpha, fadeSpeed * Time.deltaTime);
        }
    }

    public void FadeToBlack()
    {
        targetFadeAlpha = 1f;
    }

    public void FadeFromBlack()
    {
        targetFadeAlpha = 0f;
    }

    public void SetFatigueWarning(float normalizedAmount)
    {
        targetFatigueAlpha = Mathf.Clamp01(normalizedAmount) * maxFatigueAlpha;
    }
}