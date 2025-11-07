using UnityEngine;

public class ContourCollision : MonoBehaviour
{
    public StampContour contour;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Needle"))
        {
            contour?.OnPointTouched(transform);
        }
    }
}
