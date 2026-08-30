using UnityEngine;

public class WorldSpaceBillboard : MonoBehaviour
{
    private Camera targetCamera;

    void Start()
    {
        targetCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }
        }

        transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.transform.position);
    }
}