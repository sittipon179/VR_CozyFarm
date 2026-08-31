using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach this to any GameObject that already has an XR Grab Interactable
/// (or any XRBaseInteractable) to give it a gentle "you can pick this up"
/// visual cue: a small scale pulse plus a warm color tint while a hand or
/// ray is hovering over it. Reusable later on tools, seeds, watering cans.
///
/// Handles multi-material meshes correctly (e.g. a bed built from separate
/// wood/pillow/mattress/sheet materials): each renderer + material slot is
/// tracked and restored independently, so hovering doesn't flatten every
/// part to the same tint color and lose the object's original look.
///
/// Requires XR Interaction Toolkit 3.x (Unity 6 default). If your project
/// uses an older XRI version (pre-3.0) and Unity reports
/// "CS0246: XRBaseInteractable could not be found", remove the
/// ".Interactables" using line below and keep only
/// "using UnityEngine.XR.Interaction.Toolkit;".
/// </summary>
public class InteractableHoverFeedback : MonoBehaviour
{
    [Header("Scale Feedback")]
    [SerializeField] private float hoverScaleMultiplier = 1.08f;
    [SerializeField] private float scaleLerpSpeed = 10f;

    [Header("Color Feedback (requires a URP/Lit material)")]
    [SerializeField] private Color hoverTintColor = new Color(1f, 0.93f, 0.75f);

    private XRBaseInteractable _interactable;
    private Vector3 _baseScale;
    private Vector3 _targetScale;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // One entry per renderer+materialIndex pair, so a multi-material mesh (wood frame,
    // pillow, mattress, sheet, etc. all on one Renderer) keeps each part's own original
    // color instead of every submesh being forced to the same tint via a single shared
    // MaterialPropertyBlock (which is what Renderer.SetPropertyBlock(block) with no index
    // does - it applies to every material slot on that renderer).
    private class RendererSlot
    {
        public Renderer renderer;
        public int materialIndex;
        public Color baseColor;
        public MaterialPropertyBlock propBlock;
    }

    private List<RendererSlot> _slots;

    private void Awake()
    {
        _interactable = GetComponent<XRBaseInteractable>();

        _baseScale = transform.localScale;
        _targetScale = _baseScale;

        _slots = new List<RendererSlot>();
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];
                Color baseColor = Color.white;
                if (mat != null && mat.HasProperty(BaseColorId))
                {
                    baseColor = mat.GetColor(BaseColorId);
                }

                RendererSlot slot = new RendererSlot();
                slot.renderer = renderer;
                slot.materialIndex = i;
                slot.baseColor = baseColor;
                slot.propBlock = new MaterialPropertyBlock();
                _slots.Add(slot);
            }
        }
    }

    private void OnEnable()
    {
        if (_interactable == null)
        {
            Debug.LogWarning($"{nameof(InteractableHoverFeedback)} on '{name}' needs an XRBaseInteractable (e.g. XR Grab Interactable) on the same GameObject.", this);
            return;
        }

        _interactable.hoverEntered.AddListener(OnHoverEntered);
        _interactable.hoverExited.AddListener(OnHoverExited);
    }

    private void OnDisable()
    {
        if (_interactable == null) return;

        _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        _interactable.hoverExited.RemoveListener(OnHoverExited);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        _targetScale = _baseScale * hoverScaleMultiplier;
        TintAll(hoverTintColor);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        _targetScale = _baseScale;
        RestoreAll();
    }

    private void Update()
    {
        if (transform.localScale == _targetScale) return;
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * scaleLerpSpeed);
    }

    private void TintAll(Color color)
    {
        if (_slots == null) return;
        for (int i = 0; i < _slots.Count; i++)
        {
            RendererSlot slot = _slots[i];
            if (slot.renderer == null) continue;
            slot.renderer.GetPropertyBlock(slot.propBlock, slot.materialIndex);
            slot.propBlock.SetColor(BaseColorId, color);
            slot.renderer.SetPropertyBlock(slot.propBlock, slot.materialIndex);
        }
    }

    private void RestoreAll()
    {
        if (_slots == null) return;
        for (int i = 0; i < _slots.Count; i++)
        {
            RendererSlot slot = _slots[i];
            if (slot.renderer == null) continue;
            slot.renderer.GetPropertyBlock(slot.propBlock, slot.materialIndex);
            slot.propBlock.SetColor(BaseColorId, slot.baseColor);
            slot.renderer.SetPropertyBlock(slot.propBlock, slot.materialIndex);
        }
    }
}
