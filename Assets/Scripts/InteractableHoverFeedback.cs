using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach this to any GameObject that already has an XR Grab Interactable
/// (or any XRBaseInteractable) to give it a gentle "you can pick this up"
/// visual cue: a small scale pulse plus a warm color tint while a hand or
/// ray is hovering over it. Reusable later on tools, seeds, watering cans.
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
    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;
    private Vector3 _baseScale;
    private Vector3 _targetScale;
    private Color _baseColor;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        _interactable = GetComponent<XRBaseInteractable>();
        _renderer = GetComponentInChildren<Renderer>();
        _propBlock = new MaterialPropertyBlock();

        _baseScale = transform.localScale;
        _targetScale = _baseScale;

        if (_renderer != null && _renderer.sharedMaterial != null &&
            _renderer.sharedMaterial.HasProperty(BaseColorId))
        {
            _baseColor = _renderer.sharedMaterial.GetColor(BaseColorId);
        }
        else
        {
            _baseColor = Color.white;
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
        Tint(hoverTintColor);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        _targetScale = _baseScale;
        Tint(_baseColor);
    }

    private void Update()
    {
        if (transform.localScale == _targetScale) return;
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * scaleLerpSpeed);
    }

    private void Tint(Color color)
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(BaseColorId, color);
        _renderer.SetPropertyBlock(_propBlock);
    }
}