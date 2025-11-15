using UnityEngine;

public class GlassPiece : MonoBehaviour
{
    public bool isBreakable = false;
    public float fallDelay = 0.2f;   // задержка перед падением

    private bool used = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (used) return;

        if (collision.collider.CompareTag("Player"))
        {
            used = true;

            if (isBreakable)
            {
                // Если ломается ? падает вниз
                Invoke(nameof(Fall), fallDelay);
            }
            else
            {
                // Если прочное — не падает
                Debug.Log(name + " is SAFE");
            }
        }
    }

    void Fall()
    {
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 2f;
    }
}
