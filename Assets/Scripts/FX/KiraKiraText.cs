using System.Collections;
using UnityEngine;
using TMPro;

#if DOTWEEN_ENABLED
using DG.Tweening;
#endif

[RequireComponent(typeof(TMP_Text))]
public class KiraKiraText : MonoBehaviour
{
    [Header("Scale Punch")]
    [SerializeField] float punchScale = 0.5f;    // ↑派手に
    [SerializeField] float punchDuration = 0.45f;
    [SerializeField] int vibrato = 12;
    [SerializeField] float elasticity = 0.7f;

    [Header("Color Flash")]
    [SerializeField] Color flashColor = new Color(1f, 0.9f, 0.2f, 1f); // 金っぽい
    [SerializeField] float flashUp = 0.05f;
    [SerializeField] float flashDown = 0.35f;

    [Header("Outline Flash (TMP)")]
    [SerializeField] float outlinePeak = 0.25f;   // 0〜1
    [SerializeField] float outlineDown = 0.25f;

    TMP_Text _tmp;
    Color _baseColor;
    float _baseOutline;

    void Awake()
    {
        _tmp = GetComponent<TMP_Text>();
        _baseColor = _tmp.color;
        _baseOutline = _tmp.outlineWidth;
    }

    public void Play()
    {
        StopAllCoroutines();
        StartCoroutine(CoPlay());       // 直接呼び出し用
    }

    public void PlayNextFrame()
    {
        StopAllCoroutines();
        StartCoroutine(CoPlayNextFrame()); // 次フレームで確実に描画後に再生
    }

    IEnumerator CoPlayNextFrame()
    {
        yield return null; // 1フレーム待つ
        yield return CoPlay();
    }

    IEnumerator CoPlay()
    {
#if DOTWEEN_ENABLED
        var rt = (RectTransform)transform;
        rt.DOKill();
        rt.DOPunchScale(Vector3.one * punchScale, punchDuration, vibrato, elasticity)
          .SetUpdate(true);
#else
        var rt = (RectTransform)transform;
        Vector3 start = Vector3.one;
        Vector3 peak = Vector3.one * (1f + punchScale);
        float t = 0f;
        while (t < punchDuration * 0.4f)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(start, peak, t / (punchDuration * 0.4f));
            yield return null;
        }
        t = 0f;
        while (t < punchDuration * 0.6f)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(peak, start, t / (punchDuration * 0.6f));
            yield return null;
        }
        rt.localScale = start;
#endif
        // Face color flash
        _tmp.color = _baseColor;
        float t2 = 0f;
        while (t2 < flashUp)
        {
            t2 += Time.unscaledDeltaTime;
            _tmp.color = Color.Lerp(_baseColor, flashColor, t2 / flashUp);
            yield return null;
        }
        _tmp.color = flashColor;

        // Outline flash
        _tmp.outlineWidth = outlinePeak;
        t2 = 0f;
        while (t2 < outlineDown)
        {
            t2 += Time.unscaledDeltaTime;
            _tmp.color = Color.Lerp(flashColor, _baseColor, t2 / outlineDown);
            _tmp.outlineWidth = Mathf.Lerp(outlinePeak, _baseOutline, t2 / outlineDown);
            yield return null;
        }
        _tmp.color = _baseColor;
        _tmp.outlineWidth = _baseOutline;
    }
}
