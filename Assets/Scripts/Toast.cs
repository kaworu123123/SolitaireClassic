using UnityEngine;
using TMPro;

public class Toast : MonoBehaviour
{
    public static Toast Instance;

    [Header("Refs")]
    public CanvasGroup canvasGroup;
    public TMP_Text text;
    public float showSeconds = 1.6f;
    public float fadeSeconds = 0.25f;

    float _until = -1f;

    void Awake()
    {
        Instance = this;
        if (canvasGroup) canvasGroup.alpha = 0f;
    }

    void Update()
    {
        if (!canvasGroup) return;

        // フェードアウト
        if (_until > 0f && Time.unscaledTime > _until)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.unscaledDeltaTime / fadeSeconds);
            if (canvasGroup.alpha <= 0.001f) _until = -1f;
        }
    }

    public void Show(string msg, float seconds = -1f)
    {
        if (!canvasGroup || !text) return;
        if (seconds <= 0f) seconds = showSeconds;

        text.text = msg;
        canvasGroup.alpha = 1f;
        _until = Time.unscaledTime + seconds;
    }
}
