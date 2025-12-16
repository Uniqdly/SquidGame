using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class CandySelectable : MonoBehaviour
{
    private bool selected = false;

    private void Awake()
    {
        var grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnGrabbed);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (selected) return;
        selected = true;

        if (CandyGameManager.Instance != null)
        {
            CandyGameManager.Instance.SelectCandy(gameObject);
        }
    }
}
