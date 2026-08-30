using UnityEngine;

public class BedInteractable : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public float interactDistance = 3f;

    void Update()
    {
        if (UIStateManager.Instance != null && UIStateManager.Instance.IsAnyUIOpen)
        {
            return;
        }

        if (SleepManager.Instance == null || !SleepManager.Instance.CanSleepNow)
        {
            return;
        }

        if (playerCamera == null)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    SleepManager.Instance.TryToSleep();
                }
            }
        }
    }
}