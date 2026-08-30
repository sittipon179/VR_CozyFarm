using UnityEngine;

public class ShopInteractable : MonoBehaviour
{
    [Header("References")]
    public ShopUIController shopUI;
    public Camera playerCamera;
    public float interactDistance = 3f;

    void Update()
    {
        if (UIStateManager.Instance != null && UIStateManager.Instance.IsAnyUIOpen)
        {
            return;
        }

        if (playerCamera == null || shopUI == null)
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
                    shopUI.OpenShop();
                }
            }
        }
    }
}