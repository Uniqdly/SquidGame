using UnityEngine;
public class LookAtPlayer : MonoBehaviour
{
    public Transform cameraTransform;
    void Start()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }
    void Update()
    {
        if (cameraTransform) transform.LookAt(cameraTransform);
    }
}
