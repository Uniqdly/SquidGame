using UnityEngine;

public class GlassController : MonoBehaviour
{
    public GlassPiece leftGlass;
    public GlassPiece rightGlass;

    // флаг — выбор закреплён (после первого касания)
    private bool locked = false;

    void Awake()
    {
        // safety: если ссылки не выставлены вручную, попробуем найти дочерние GlassPiece
        if (leftGlass == null || rightGlass == null)
        {
            var pieces = GetComponentsInChildren<GlassPiece>();
            if (pieces.Length >= 2)
            {
                if (leftGlass == null) leftGlass = pieces[0];
                if (rightGlass == null) rightGlass = pieces[1];
            }
        }

        // установим back-reference (чтобы плитка знала контроллер)
        if (leftGlass != null) leftGlass.parentController = this;
        if (rightGlass != null) rightGlass.parentController = this;
    }

    void Start()
    {
        // Если уже закреплено — не рандомизируем (на всякий случай)
        if (locked) return;

        RandomizeFragile();
    }

    public void RandomizeFragile()
    {
        if (locked) return; // если выбор закреплён — не менять

        int breakIndex = Random.Range(0, 2);

        if (breakIndex == 0)
        {
            AssignFragile(true, false);
        }
        else
        {
            AssignFragile(false, true);
        }
    }

    // Удобный метод для назначения
    public void AssignFragile(bool leftIsFragile, bool rightIsFragile)
    {
        if (leftGlass != null) leftGlass.isBreakable = leftIsFragile;
        if (rightGlass != null) rightGlass.isBreakable = rightIsFragile;
    }

    // Вызывается плиткой при её первом контакте с игроком
    public void OnGlassTouched(GlassPiece touched)
    {
        // если уже закреплено — ничего не делаем
        if (locked) return;

        // закрепляем выбор: если тронутая плитка ломаемая -> она остаётся ломаемой
        // иначе — она безопасная и другая должна остаться ломаемой (если была)
        if (touched == leftGlass)
        {
            // если left ломаемая — закрепляем left ломаемой
            // если left безопасная — закрепляем right ломаемой (если right был ломаемым)
            if (leftGlass.isBreakable)
                AssignFragile(true, false);
            else
                AssignFragile(false, true);
        }
        else if (touched == rightGlass)
        {
            if (rightGlass.isBreakable)
                AssignFragile(false, true);
            else
                AssignFragile(true, false);
        }
        else
        {
            // если тронута какая-то неожиданная плитка — ничего не меняем, но закрепим текущее состояние
            // (можно логировать)
        }

        locked = true;
        Debug.Log($"GlassController: selection locked. LeftIsFragile={leftGlass?.isBreakable} RightIsFragile={rightGlass?.isBreakable}");
    }

    // (опционально) сбросить выбор — если нужна перезапуск раунда
    public void UnlockAndRandomize()
    {
        locked = false;
        RandomizeFragile();
    }
}
