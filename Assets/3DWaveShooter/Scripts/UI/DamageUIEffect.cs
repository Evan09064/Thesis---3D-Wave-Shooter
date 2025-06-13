using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DamageUIEffect : MonoBehaviour
{
    public static DamageUIEffect inst;
    public Image bloodOverlay;
    public float fadeInTime  = 0.1f;
    public float fadeOutTime = 0.5f;

    void Awake()
    {
        inst = this;
        SetAlpha(0f);
    }

    public void FlashBlood()
    {
        StopAllCoroutines();
        StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        yield return StartCoroutine(Fade(0f, 0.6f, fadeInTime));
        yield return StartCoroutine(Fade(0.6f, 0f, fadeOutTime));
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t/dur);
            SetAlpha(a);
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float a)
    {
        var c = bloodOverlay.color;
        c.a = a;
        bloodOverlay.color = c;
    }
}
