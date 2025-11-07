using UnityEngine;

public class NeedleCollisionLogger : MonoBehaviour
{
    [Tooltip("—лой, на котором находитс€ Stamp (укажи StampLayer)")]
    public LayerMask stampLayer;
    [Tooltip("—сылка на компонент CutChecker на штампе (опционально)")]
    public CutChecker debugCutChecker; // можем дать вручную или находить в OnTriggerStay

    void Start()
    {
        Debug.Log("NeedleCollisionLogger: Start on " + gameObject.name);
    }

    void OnTriggerEnter(Collider other)
    {
        CheckCollider(other, "OnTriggerEnter");
    }

    void OnTriggerStay(Collider other)
    {
        CheckCollider(other, "OnTriggerStay");
    }

    void OnTriggerExit(Collider other)
    {
        CheckCollider(other, "OnTriggerExit");
    }

    void CheckCollider(Collider other, string ev)
    {
        // ѕровер€ем Ч тот ли это слой, на котором лежит штамп
        if (((1 << other.gameObject.layer) & stampLayer.value) == 0)
        {
            // не штамп Ч игнорируем (но можно логировать дл€ отладки)
            // Debug.Log($"NeedleCollisionLogger: {ev} with {other.name} (ignored, layer={LayerMask.LayerToName(other.gameObject.layer)})");
            return;
        }

        // найден объект на слое штампа Ч вычисл€ем точку ближайшую на коллайдере к позиции иглы
        Vector3 contactPoint = other.ClosestPoint(transform.position);

        Debug.Log($"NeedleCollisionLogger: {ev} -> STAMP hit: {other.name} contactPoint={contactPoint}");

        // ѕопробуем найти CutChecker в родител€х этого коллайдера
        CutChecker cc = debugCutChecker;
        if (cc == null)
        {
            cc = other.GetComponentInParent<CutChecker>();
        }

        if (cc != null)
        {
            // ѕередаЄм мировую точку попадани€
            cc.ProcessWorldHit(contactPoint);
        }
        else
        {
            Debug.LogWarning("NeedleCollisionLogger: CutChecker not found on Stamp parent!");
        }
    }

    void Update()
    {
        Debug.DrawRay(transform.position, transform.forward * 0.12f, Color.cyan);
    }
}
