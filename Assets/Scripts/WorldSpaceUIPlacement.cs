using UnityEngine;

// Placement modes:
//  - Head-Locked (Main Menu): pins the panel a fixed distance in front of the player, HUD-style.
//  - Follow (Inventory / Book): re-centers on the player as they walk/turn.
//  - Fixed Anchor (Shop): pinned to a world object (the shop counter), ignores the player.
//
// Both Head-Locked and Follow use a THRESHOLD re-center: the panel only starts moving/rotating
// once the player's view has diverged from the panel's current facing by more than
// recenterAngleThreshold. Below that threshold the panel holds perfectly still, so aiming the
// VR ray at any part of it -- a corner, a diagonal slot -- never drags the panel out from under
// the cursor. Once the player genuinely turns past the threshold (walking around, turning to
// face a new direction), it smoothly catches back up to face them again.
public class WorldSpaceUIPlacement : MonoBehaviour
{
    [Header("Head-Locked Mode (HUD-style panels: Main Menu)")]
    [Tooltip("If true, this panel head-locks (pins in front of the player at headLockDistance) subject to the same threshold re-center as Follow mode below. Overrides Follow/Anchor modes.")]
    public bool headLocked = false;
    public float headLockDistance = 1.4f;

    [Header("Follow Mode (player-following panels: Inventory / Book)")]
    public float distance = 2f;
    public float heightOffset = -0.05f;
    public float followSpeed = 4f;
    public bool continuousFollow = true;
    public bool continuousRotate = false;

    [Header("Threshold Re-center")]
    [Tooltip("Degrees the player's view must diverge from the panel's current facing before it starts catching up. Keeps casual aiming (even at a diagonal corner) from dragging the panel away, while a genuine turn-away still brings it back in front of you.")]
    public float recenterAngleThreshold = 45f;

    [Header("Anchor Mode (fixed-location panels: Shop)")]
    public bool useFixedAnchor = false;
    public Transform anchorTransform;
    public Vector3 anchorLocalOffset = new Vector3(0f, 1.4f, 2f);
    public float anchorYRotationOffset = 180f;

    public Vector3 canvasScale = new Vector3(0.0015f, 0.0015f, 0.0015f);

    private bool recentering = false;

    void OnEnable()
    {
        recentering = false;
        SnapToFront();
    }

    void Update()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        if (canvas.transform.localScale != canvasScale) canvas.transform.localScale = canvasScale;
        Transform canvasTransform = canvas.transform;

        if (useFixedAnchor)
        {
            if (anchorTransform != null) ApplyAnchorPlacement(canvasTransform);
            return;
        }

        bool moves = headLocked || continuousFollow;
        bool rotates = headLocked || continuousRotate;
        if (!moves && !rotates) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 flatForward = GetFlatForward(cam.transform);
        float dist = headLocked ? headLockDistance : distance;
        Vector3 targetPos = ComputeTargetPosition(cam.transform, flatForward, dist);
        Quaternion targetRot = Quaternion.LookRotation(flatForward, Vector3.up);

        float angle = Vector3.Angle(canvasTransform.forward, flatForward);
        if (angle > recenterAngleThreshold)
        {
            recentering = true;
        }
        if (!recentering) return;

        float catchUpSpeed = headLocked ? followSpeed * 2f : followSpeed;
        float t = 1f - Mathf.Exp(-catchUpSpeed * Time.deltaTime);

        if (moves) canvasTransform.position = Vector3.Lerp(canvasTransform.position, targetPos, t);
        if (rotates) canvasTransform.rotation = Quaternion.Slerp(canvasTransform.rotation, targetRot, t);

        bool posSettled = !moves || Vector3.Distance(canvasTransform.position, targetPos) < 0.01f;
        bool rotSettled = !rotates || Vector3.Angle(canvasTransform.forward, flatForward) < 0.5f;
        if (posSettled && rotSettled)
        {
            recentering = false;
        }
    }

    void SnapToFront()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Transform canvasTransform = canvas.transform;
        canvasTransform.localScale = canvasScale;

        if (useFixedAnchor)
        {
            if (anchorTransform != null) ApplyAnchorPlacement(canvasTransform);
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 flatForward = GetFlatForward(cam.transform);
        float dist = headLocked ? headLockDistance : distance;
        canvasTransform.position = ComputeTargetPosition(cam.transform, flatForward, dist);
        canvasTransform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
    }

    void ApplyAnchorPlacement(Transform canvasTransform)
    {
        canvasTransform.position = anchorTransform.TransformPoint(anchorLocalOffset);
        canvasTransform.rotation = anchorTransform.rotation * Quaternion.Euler(0f, anchorYRotationOffset, 0f);
    }

    Vector3 GetFlatForward(Transform t)
    {
        Vector3 flatForward = t.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) { flatForward = t.up; flatForward.y = 0f; }
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        flatForward.Normalize();
        return flatForward;
    }

    Vector3 ComputeTargetPosition(Transform camTransform, Vector3 flatForward, float dist)
    {
        Vector3 targetPos = camTransform.position + flatForward * dist;
        targetPos.y = camTransform.position.y + heightOffset;
        return targetPos;
    }
}
