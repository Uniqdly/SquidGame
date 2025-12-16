using UnityEngine;

public class ContourPoint : MonoBehaviour
{
    public enum PointType
    {
        Allowed,
        Forbidden
    }

    public PointType pointType;

    private Renderer rend;
    private bool touched;

    void Awake()
    {
        rend = GetComponent<Renderer>();
    }

    public void MarkAllowed()
    {
        if (touched) return;
        touched = true;

        if (rend != null)
            rend.material.color = Color.green;
    }

    public void MarkForbidden()
    {
        if (touched) return;
        touched = true;

        if (rend != null)
            rend.material.color = Color.red;
    }
}
