using UnityEngine;
using System.Collections;

public class DollController : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Transform dollHead;
    public float turnDuration = 1f;
    public float minRedTime = 2f;
    public float maxRedTime = 4f;
    public float minGreenTime = 2f;
    public float maxGreenTime = 4f;

    [Header("Audio Clips")]
    public AudioClip greenClip;   // Когда кукла оборачивается к игроку
    public AudioClip redClip;     // Когда отворачивается

    private AudioSource audioSource;

    bool lookingAtPlayer = false;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        StartCoroutine(DollRoutine());
    }

    IEnumerator DollRoutine()
    {
        while (true)
        {
            // 1. Кукла смотрит на игрока (GREEN)
            yield return LookAtPlayer();
            PlayGreenSound();

            float greenTime = Random.Range(minGreenTime, maxGreenTime);
            yield return new WaitForSeconds(greenTime);

            // 2. Кукла отворачивается (RED)
            yield return LookAway();
            PlayRedSound();

            float redTime = Random.Range(minRedTime, maxRedTime);
            yield return new WaitForSeconds(redTime);
        }
    }

    IEnumerator LookAtPlayer()
    {
        lookingAtPlayer = true;

        Quaternion startRot = dollHead.rotation;
        Quaternion endRot = Quaternion.Euler(0, 0, 0);

        float t = 0;
        while (t < turnDuration)
        {
            t += Time.deltaTime;
            float f = t / turnDuration;
            dollHead.rotation = Quaternion.Slerp(startRot, endRot, f);
            yield return null;
        }
    }

    IEnumerator LookAway()
    {
        lookingAtPlayer = false;

        Quaternion startRot = dollHead.rotation;
        Quaternion endRot = Quaternion.Euler(0, 180, 0);

        float t = 0;
        while (t < turnDuration)
        {
            t += Time.deltaTime;
            float f = t / turnDuration;
            dollHead.rotation = Quaternion.Slerp(startRot, endRot, f);
            yield return null;
        }
    }

    void PlayGreenSound()
    {
        if (greenClip == null) return;
        audioSource.Stop();
        audioSource.clip = greenClip;
        audioSource.Play();
    }

    void PlayRedSound()
    {
        if (redClip == null) return;
        audioSource.Stop();
        audioSource.clip = redClip;
        audioSource.Play();
    }

    public bool IsLookingAtPlayer()
    {
        return lookingAtPlayer;
    }
}
