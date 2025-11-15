using UnityEngine;

[DisallowMultipleComponent]
public class ContourPoint : MonoBehaviour
{
    public enum PointType { Main, Inner, Outer }

    [Tooltip("“ип этой точки: Main Ч по ней надо вести иглу; Inner/Outer Ч прикосновение = промах")]
    public PointType pointType = PointType.Main;

    [Tooltip("¬изуальна€ подсветка при косании (Main -> green, miss -> red)")]
    public Color mainTouchedColor = Color.green;
    public Color missColor = Color.red;

    // удобно Ч ссылка на Renderer, если есть визуал
    private Renderer rend;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
    }

    public void MarkTouchedAsMain()
    {
        if (rend != null)
        {
            rend.material = new Material(rend.material);
            rend.material.color = mainTouchedColor;
        }
    }

    public void MarkAsMissed()
    {
        if (rend != null)
        {
            rend.material = new Material(rend.material);
            rend.material.color = missColor;
        }
    }
}
