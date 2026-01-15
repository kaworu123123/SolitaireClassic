using System.Collections;
using UnityEngine;

public class ButtonPulse : MonoBehaviour
{
    [SerializeField] RectTransform target; // BG ‚Ì RectTransform
    [SerializeField] float scale = 0.96f;
    [SerializeField] float dur = 0.06f;

    public void Pulse() => StartCoroutine(Co());

    IEnumerator Co()
    {
        if (target == null) target = (RectTransform)transform;
        Vector3 a = Vector3.one;
        Vector3 b = Vector3.one * scale;
        float t = 0f;
        while (t < 1f) { t += Time.unscaledDeltaTime / dur; target.localScale = Vector3.Lerp(a, b, t); yield return null; }
        t = 0f;
        while (t < 1f) { t += Time.unscaledDeltaTime / dur; target.localScale = Vector3.Lerp(b, a, t); yield return null; }
        target.localScale = a;
    }
}
