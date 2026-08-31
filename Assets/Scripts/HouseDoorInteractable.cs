using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

[RequireComponent(typeof(XRSimpleInteractable))]
public class HouseDoorInteractable : MonoBehaviour
{
    [Header("References")]
    public Transform xrRig;
    public Transform houseRoot;

    [Header("Spawn Points")]
    public Transform exteriorSpawnPoint;
    public Transform interiorSpawnPoint;

    [Header("Interior Bounds (house-local X/Z)")]
    public Vector2 interiorLocalMin = new Vector2(-1.4f, -1.9f);
    public Vector2 interiorLocalMax = new Vector2(1.0f, 2.5f);

    [Header("Fade Timing")]
    public float fadeOutWaitTimeout = 2f;

    private XRSimpleInteractable _interactable;
    private XROrigin _xrOrigin;
    private bool isTransitioning;

    private const float SelectCooldownDuration = 1f;
    private float selectCooldownEndTime;

    private readonly List<LocomotionProvider> _lockedProviders = new List<LocomotionProvider>();

    void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
        if (houseRoot == null)
        {
            GameObject houseGO = GameObject.Find("House");
            if (houseGO != null) houseRoot = houseGO.transform;
        }

        if (xrRig != null)
        {
            _xrOrigin = xrRig.GetComponent<XROrigin>();
        }
    }

    void OnEnable()
    {
        if (_interactable != null) _interactable.selectEntered.AddListener(OnSelectEntered);
    }

    void OnDisable()
    {
        if (_interactable != null) _interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    // The XR rig's root transform (xrRig.position) is NOT reliable for "where is the player really
    // standing". The XR Interaction Simulator's default FPS-style walk (WASD with no manipulate key
    // held) moves the camera/controllers via an accumulating LOCAL device-space offset - it does not
    // move the rig root at all. That means after walking around, xrRig.position can still read as
    // basically the rig's original spawn position while the player is visually standing somewhere
    // else entirely (e.g. right at the door). Always use the actual camera position (which reflects
    // that accumulated offset) as "the player's world position" instead.
    private Vector3 GetPlayerWorldPosition()
    {
        if (_xrOrigin != null && _xrOrigin.Camera != null)
        {
            return _xrOrigin.Camera.transform.position;
        }

        if (xrRig != null) return xrRig.position;
        return Vector3.zero;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        string interactorName = (args != null && args.interactorObject != null && args.interactorObject.transform != null)
            ? args.interactorObject.transform.name
            : "(unknown/reflection-invoked)";
        Vector3 playerPos = GetPlayerWorldPosition();
        Debug.Log("[HouseDoor] OnSelectEntered fired by interactor='" + interactorName + "' playerPos=" + playerPos.ToString("F3") + " isTransitioning=" + isTransitioning);

        if (isTransitioning)
        {
            Debug.Log("[HouseDoor] Ignored: already transitioning.");
            return;
        }

        if (Time.time < selectCooldownEndTime)
        {
            Debug.Log("[HouseDoor] Ignored: still in post-transition cooldown (" + (selectCooldownEndTime - Time.time).ToString("F2") + "s left).");
            return;
        }

        if (UIStateManager.Instance != null && UIStateManager.Instance.IsAnyUIOpen)
        {
            Debug.Log("[HouseDoor] Ignored: a UI panel is open.");
            return;
        }

        if (ScreenFadeController.Instance == null || xrRig == null)
        {
            Debug.Log("[HouseDoor] Ignored: missing ScreenFadeController or xrRig reference.");
            return;
        }

        if (exteriorSpawnPoint == null || interiorSpawnPoint == null)
        {
            Debug.Log("[HouseDoor] Ignored: missing spawn point reference(s).");
            return;
        }

        bool playerCurrentlyInside = IsInsideHouse(playerPos);
        Transform destination = playerCurrentlyInside ? exteriorSpawnPoint : interiorSpawnPoint;
        Debug.Log("[HouseDoor] playerCurrentlyInside=" + playerCurrentlyInside + " -> destination='" + destination.name + "' at " + destination.position.ToString("F3"));

        StartCoroutine(TransitionRoutine(destination));
    }

    private bool IsInsideHouse(Vector3 worldPosition)
    {
        if (houseRoot == null)
        {
            float distToInterior = Vector3.Distance(worldPosition, interiorSpawnPoint.position);
            float distToExterior = Vector3.Distance(worldPosition, exteriorSpawnPoint.position);
            return distToInterior < distToExterior;
        }

        Vector3 local = houseRoot.InverseTransformPoint(worldPosition);
        return local.x >= interiorLocalMin.x && local.x <= interiorLocalMax.x
            && local.z >= interiorLocalMin.y && local.z <= interiorLocalMax.y;
    }

    private IEnumerator TransitionRoutine(Transform destination)
    {
        isTransitioning = true;
        LockLocomotion();

        if (TimeManager.Instance != null) TimeManager.Instance.StopTime();

        ScreenFadeController.Instance.FadeToBlack();

        float elapsed = 0f;
        while (ScreenFadeController.Instance.CurrentFadeAlpha < 0.99f && elapsed < fadeOutWaitTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        TeleportRig(destination);

        ScreenFadeController.Instance.FadeFromBlack();

        elapsed = 0f;
        while (ScreenFadeController.Instance.CurrentFadeAlpha > 0.01f && elapsed < fadeOutWaitTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (TimeManager.Instance != null) TimeManager.Instance.StartTime();

        UnlockLocomotion();
        isTransitioning = false;
        selectCooldownEndTime = Time.time + SelectCooldownDuration;
        Debug.Log("[HouseDoor] Transition complete, player now at " + GetPlayerWorldPosition().ToString("F3") + " (rig root at " + (xrRig != null ? xrRig.position.ToString("F3") : "NULL") + "). Ignoring new selects for " + SelectCooldownDuration + "s.");
    }

    private void LockLocomotion()
    {
        _lockedProviders.Clear();
        if (xrRig == null) return;

        LocomotionProvider[] providers = xrRig.GetComponentsInChildren<LocomotionProvider>(true);
        foreach (LocomotionProvider provider in providers)
        {
            if (provider.enabled)
            {
                provider.enabled = false;
                _lockedProviders.Add(provider);
            }
        }
    }

    private void UnlockLocomotion()
    {
        foreach (LocomotionProvider provider in _lockedProviders)
        {
            if (provider != null) provider.enabled = true;
        }
        _lockedProviders.Clear();
    }

    private void TeleportRig(Transform destination)
    {
        CharacterController cc = xrRig.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Vector3 targetPosition = destination.position;

        RaycastHit groundHit;
        if (Physics.Raycast(targetPosition + Vector3.up * 1f, Vector3.down, out groundHit, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            targetPosition.y = groundHit.point.y + 0.02f;
        }

        if (_xrOrigin != null)
        {
            // Simply setting xrRig's root transform is not enough: the camera can be sitting at a large
            // LOCAL offset from the rig root (built up from walking in the Simulator's FPS-style move,
            // or from real headset tracking drift), and that offset would carry straight through a plain
            // SetPositionAndRotation, landing the player far from the intended spot. XROrigin's own
            // teleport helpers correctly account for that offset:
            //  1) Rotate the origin around the camera's current position so the camera ends up facing
            //     the destination's forward direction.
            //  2) Move the origin so the camera itself (not just the root) ends up exactly above the
            //     ground-snapped target position, preserving the player's current eye height.
            _xrOrigin.MatchOriginUpCameraForward(Vector3.up, destination.forward);

            float eyeHeight = _xrOrigin.CameraInOriginSpaceHeight;
            Vector3 desiredCameraWorldPos = new Vector3(targetPosition.x, targetPosition.y + eyeHeight, targetPosition.z);
            _xrOrigin.MoveCameraToWorldLocation(desiredCameraWorldPos);
        }
        else
        {
            xrRig.SetPositionAndRotation(targetPosition, destination.rotation);
        }

        if (cc != null) cc.enabled = true;
    }
}
