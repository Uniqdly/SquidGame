using UnityEngine;
using System.Collections;

public class GameController_WithLight : MonoBehaviour
{
    public DollBehavior doll;
    public RedLightGreenLightController playerChecker;
    public Light directionalLight; // ваша Directional Light
    public Color greenColor = new Color(0.7f, 1f, 0.7f);
    public Color redColor = new Color(1f, 0.6f, 0.6f);
    public float greenIntensity = 1.2f;
    public float redIntensity = 0.8f;

    public float greenTime = 7f;
    public float redTime = 4f;
    public float startDelay = 2f;
    public float lightBlendDuration = 0.6f;

    Coroutine lightBlendCoroutine;

    void Start()
    {
        if (directionalLight == null)
            directionalLight = RenderSettings.sun; // пытаемс€ вз€ть Sun
        // «апускаем игровой цикл, он будет жить независимо от blend-корутин
        StartCoroutine(GameLoop());
    }

    IEnumerator GameLoop()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            // GREEN
            SetGreenState(true, greenTime);
            yield return new WaitForSeconds(greenTime);

            // RED
            SetGreenState(false, redTime);
            yield return new WaitForSeconds(redTime);
        }
    }

    void SetGreenState(bool green, float timer)
    {
        if (doll != null) doll.SetWatching(!green); // green==true -> doll отворачиваетс€
        if (playerChecker != null) playerChecker.SetGreen(green, timer);

        // —вет: запустим (и при необходимости остановим) только корутину blend'а
        if (directionalLight != null)
        {
            if (lightBlendCoroutine != null)
                StopCoroutine(lightBlendCoroutine);
            Color targetColor = green ? greenColor : redColor;
            float targetIntensity = green ? greenIntensity : redIntensity;
            lightBlendCoroutine = StartCoroutine(BlendLight(targetColor, targetIntensity, lightBlendDuration));
        }
    }

    IEnumerator BlendLight(Color targetColor, float targetIntensity, float duration)
    {
        if (directionalLight == null) yield break;
        Color startColor = directionalLight.color;
        float startIntensity = directionalLight.intensity;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / duration);
            directionalLight.color = Color.Lerp(startColor, targetColor, f);
            directionalLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, f);
            yield return null;
        }
        directionalLight.color = targetColor;
        directionalLight.intensity = targetIntensity;
        lightBlendCoroutine = null;
    }

    // ƒл€ удобства тестировани€ в редакторе
    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.G))
        {
            SetGreenState(true, greenTime);
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            SetGreenState(false, redTime);
        }
#endif
    }
}
