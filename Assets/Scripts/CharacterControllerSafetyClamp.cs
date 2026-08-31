using UnityEngine;

// Safety net for the VR rig's CharacterController. XR Origin automatically resizes the
// CharacterController's height/center every frame to match the tracked camera's position
// above the floor (so a real player crouching in a headset gets a correctly-sized collision
// capsule). That built-in sync has no bounds check: if the tracked/simulated head position
// ever moves far outside the normal range (e.g. a debug "move device" input, or a tracking
// glitch), the computed height/center can go to invalid values (zero, negative, or a huge
// offset on any axis). A CharacterController in that state has no meaningful collision volume,
// so the player can pass straight through floors/walls/stairs, or fall out of the world.
// This clamps height back into a sane range and re-centers the capsule every frame, after
// XR Origin's own update, so collision never actually breaks. It also catches the "fell out
// of the world" failure mode directly: if the rig ends up far below where any floor exists,
// it gets moved back to a safe fallback point instead of free-falling forever.
[DefaultExecutionOrder(32000)]
public class CharacterControllerSafetyClamp : MonoBehaviour
{
    public CharacterController characterController;
    public float minHeight = 0.5f;
    public float maxHeight = 2.2f;

    [Header("Fell-out-of-world recovery")]
    public Transform fallbackRecoveryPoint;
    public float minSafeWorldY = -20f;

    void Reset()
    {
        characterController = GetComponent<CharacterController>();
    }

    void LateUpdate()
    {
        if (characterController == null)
        {
            return;
        }

        float height = Mathf.Clamp(characterController.height, minHeight, maxHeight);
        if (!Mathf.Approximately(height, characterController.height))
        {
            characterController.height = height;
        }

        Vector3 center = characterController.center;
        Vector3 expectedCenter = new Vector3(0f, height * 0.5f, 0f);
        if (center != expectedCenter)
        {
            characterController.center = expectedCenter;
        }

        if (fallbackRecoveryPoint != null && transform.position.y < minSafeWorldY)
        {
            characterController.enabled = false;
            transform.SetPositionAndRotation(fallbackRecoveryPoint.position, fallbackRecoveryPoint.rotation);
            characterController.enabled = true;
        }
    }
}
