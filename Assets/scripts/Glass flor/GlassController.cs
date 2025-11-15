using UnityEngine;

public class GlassController : MonoBehaviour
{
    public GlassPiece leftGlass;
    public GlassPiece rightGlass;

    void Start()
    {
        // Рандомное число: 0 или 1
        int breakIndex = Random.Range(0, 2);

        if (breakIndex == 0)
        {
            leftGlass.isBreakable = true;
            rightGlass.isBreakable = false;
        }
        else
        {
            leftGlass.isBreakable = false;
            rightGlass.isBreakable = true;
        }
    }
}
