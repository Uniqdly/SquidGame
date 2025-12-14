using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class CandyPickLimiter : MonoBehaviour
{
    // та сама€ "выбранна€" карамель
    private static CandyPickLimiter selectedCandy = null;

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
        // если ещЄ Ќ» ј јя карамель не выбрана
        if (selectedCandy == null)
        {
            selectedCandy = this;
            DestroyOtherCandies();
            return;
        }

        // если берут “” ∆≈ —јћ”ё карамель Ч ничего не делаем
        if (selectedCandy == this)
        {
            return;
        }

        // если берут другую карамель Ч уничтожаем еЄ
        Destroy(gameObject);
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
