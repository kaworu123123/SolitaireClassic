using UnityEngine;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class ScorePopup : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI text;
    [SerializeField] float moveUpSpeed = 50f;
    [SerializeField] float fadeDuration = 0.9f;

    float life;
    CanvasGroup cg;

    void Awake() { cg = GetComponent<CanvasGroup>(); life = fadeDuration; }

    public void Setup(int delta)
    {
        text.text = (delta >= 0 ? "+" : "") + delta;
        text.color = delta >= 0 ? Color.yellow : Color.red;
    }

    public void Setup(int delta, Color color)
    {
        text.text = (delta >= 0 ? "+" : "") + delta;
        text.color = color;
    }
/*
    void Update()
    {
        // 上方向にふわっと & フェード
        transform.Translate(Vector3.up * moveUpSpeed * Time.unscaledDeltaTime);
        life -= Time.unscaledDeltaTime;
        cg.alpha = Mathf.Clamp01(life / fadeDuration);
        if (life <= 0) Destroy(gameObject);
    }
*/
    void Update()
    {
        // 生存時間の進捗 (0→1)
        float ratio = 1f - (life / fadeDuration);

        // 上昇
        transform.Translate(Vector3.up * moveUpSpeed * Time.unscaledDeltaTime);

        // スケールアニメーション（最初に1.2倍 → 1倍に戻る）
        float scale = Mathf.Lerp(1.2f, 1f, ratio * 2f);
        transform.localScale = Vector3.one * scale;

        // フェード
        life -= Time.unscaledDeltaTime;
        cg.alpha = Mathf.Clamp01(life / fadeDuration);

        if (life <= 0) Destroy(gameObject);
    }

}
