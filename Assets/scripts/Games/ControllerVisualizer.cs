using UnityEngine;

[DisallowMultipleComponent]
public class ControllerVisualizer : MonoBehaviour
{
    [Header("Assign XR controller transforms (Left/Right)")]
    public Transform leftHand;
    public Transform rightHand;

    [Header("Optional: prefab to instantiate as visual. If null, simple sphere will be used.")]
    public GameObject visualPrefab;

    [Header("Local transform of visual on the hand")]
    public Vector3 localPos = Vector3.zero;
    public Vector3 localEuler = Vector3.zero;
    public Vector3 localScale = new Vector3(0.08f, 0.08f, 0.08f);

    GameObject leftInstance;
    GameObject rightInstance;

    void Start()
    {
        // create visuals
        if (leftHand != null)
            leftInstance = CreateVisual("LeftVisual", leftHand);
        if (rightHand != null)
            rightInstance = CreateVisual("RightVisual", rightHand);
    }

    GameObject CreateVisual(string name, Transform parent)
    {
        GameObject go;
        if (visualPrefab != null)
        {
            go = Instantiate(visualPrefab, parent);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(go.GetComponent<Collider>()); // remove collider if created
            go.transform.SetParent(parent, false);
        }

        go.name = name;
        go.transform.localPosition = localPos;
        go.transform.localEulerAngles = localEuler;
        go.transform.localScale = localScale;

        // optional color for quick recognition
        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Standard"));
            rend.material.color = (name.Contains("Left")) ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.5f, 0.2f);
        }
        return go;
    }

    // cleanup in editor when script removed (optional)
    void OnDestroy()
    {
        if (Application.isPlaying)
        {
            if (leftInstance != null) Destroy(leftInstance);
            if (rightInstance != null) Destroy(rightInstance);
        }
    }
}
