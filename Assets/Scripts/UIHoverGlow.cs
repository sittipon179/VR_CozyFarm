using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Renders a soft yellow "aura" glow behind whichever UI element this is attached to (typically
// a Buy button) whenever the pointer / VR ray is hovering over it -- gives the player a clear
// highlight cue while aiming at an item, right before pressing Trigger/UI Press to buy it.
// Works with both mouse (Screen Space) and TrackedDeviceGraphicRaycaster + XR UI Input Module
// (World Space VR ray) setups, since both route through the same IPointerEnterHandler /
// IPointerExitHandler interfaces.
public class UIHoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("The glow Image to fade in/out. Should be larger than the button it surrounds so it reads as a halo around the edges.")]
    public Image glowImage;
    [Tooltip("Optional. If set, the glow only appears while this button is interactable (e.g. the player can actually afford the item).")]
    public Button button;
    public Color glowColor = new Color(1f, 0.85f, 0.15f, 0.85f);
    public float pulseSpeed = 5f;
    public float pulseAmount = 0.12f;

    private bool hovering = false;

    void Update()
    {
        if (!hovering || glowImage == null)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
        glowImage.rectTransform.localScale = Vector3.one * pulse;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (glowImage == null)
        {
            return;
        }

        bool allowed = button == null || button.IsInteractable();
        Color c = glowColor;
        glowImage.color = allowed ? c : new Color(c.r, c.g, c.b, 0f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        if (glowImage == null)
        {
            return;
        }

        Color c = glowColor;
        glowImage.color = new Color(c.r, c.g, c.b, 0f);
        glowImage.rectTransform.localScale = Vector3.one;
    }
}
