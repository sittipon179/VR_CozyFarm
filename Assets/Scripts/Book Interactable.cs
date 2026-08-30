using UnityEngine;

public class BookInteractable : MonoBehaviour
{
    [Header("References")]
    public BookUIController bookUI;
    public Camera playerCamera;
    public float interactDistance = 3f;

    void Update()
    {
        if (UIStateManager.Instance != null && UIStateManager.Instance.IsAnyUIOpen)
        {
            return;
        }

        if (playerCamera == null || bookUI == null)
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
                    bookUI.OpenBook();
                }
            }
        }
    }
}