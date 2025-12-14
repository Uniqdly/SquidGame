using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class CandyPickLimiter : MonoBehaviour
{
    private static bool candyAlreadyPicked = false;

    private XRGrabInteractable grab;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
    }

    void OnEnable()
    {
        if (grab != null)
            grab.selectEntered.AddListener(OnPicked);
    }

    void OnDisable()
    {
        if (grab != null)
            grab.selectEntered.RemoveListener(OnPicked);
    }

    void OnPicked(SelectEnterEventArgs args)
    {
        // если уже взяли другую карамель — удаляем эту
        if (candyAlreadyPicked)
        {
            Destroy(gameObject);
            return;
        }

        // первая выбранная карамель
        candyAlreadyPicked = true;

        // уничтожаем все остальные карамели
        DestroyOtherCandies();
    }

    void DestroyOtherCandies()
    {
        var allCandies = FindObjectsOfType<CandyPickLimiter>();

        foreach (var candy in allCandies)
        {
            if (candy != this)
                Destroy(candy.gameObject);
        }
    }
}
