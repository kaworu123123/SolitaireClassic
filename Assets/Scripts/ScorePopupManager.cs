using UnityEngine;

public class ScorePopupManager : MonoBehaviour
{
    public static ScorePopupManager Instance;

    [SerializeField] GameObject popupPrefab;      // TextMeshProUGUI + CanvasGroup を持つプレハブ
    [SerializeField] Canvas canvas;               // Screen Space - Overlay 推奨

    // ★ 追加：スコア表示(Text/TMP)の RectTransform をアサイン
    [SerializeField] RectTransform scoreAnchor;
    [SerializeField] Vector2 scoreAnchorOffset = new Vector2(0f, 24f); // 少し上にずらす
    [SerializeField] bool mirrorToScoreUI = true; // ON ならスコア欄にも出す

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void Show(int delta, Vector3 worldPos)
    {
        if (popupPrefab == null || canvas == null) return;
        //var screen = Camera.main.WorldToScreenPoint(worldPos);
        var screen = Camera.main != null
        ? Camera.main.WorldToScreenPoint(worldPos)
        : (Vector3)worldPos; // 念のため

        var go = Instantiate(popupPrefab, canvas.transform);
        go.transform.position = screen;
        go.GetComponent<ScorePopup>().Setup(delta); // 既存の色分け（±で黄色/赤）

        // ★ ミラー表示：スコアUIにも
        if (mirrorToScoreUI) ShowAtScore(delta);
    }

    public void Show(int delta, Vector3 worldPos, Color color)
    {
        if (popupPrefab == null || canvas == null) return;
        //var screen = Camera.main.WorldToScreenPoint(worldPos);

        var screen = Camera.main != null
        ? Camera.main.WorldToScreenPoint(worldPos)
        : (Vector3)worldPos;

        var go = Instantiate(popupPrefab, canvas.transform);
        go.transform.position = screen;
        go.GetComponent<ScorePopup>().Setup(delta, color);

        if (mirrorToScoreUI) ShowAtScore(delta, color);
    }

    // ★ 追加：スコア欄のUIアンカーに出す
    public void ShowAtScore(int delta)
    {
        if (popupPrefab == null || canvas == null || scoreAnchor == null) return;

        Camera cam = null;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) cam = canvas.worldCamera;
        
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, scoreAnchor.position);
        screen += scoreAnchorOffset;

        var go = Instantiate(popupPrefab, canvas.transform);
        go.transform.position = screen;

        // ★ スコア欄用にサイズを大きめに
        go.transform.localScale = Vector3.one * 1.5f;

        go.GetComponent<ScorePopup>().Setup(delta);
    }

    // 色付きのスコア欄版（色分けを使っている場合だけ）
    public void ShowAtScore(int delta, Color color)
    {
        if (popupPrefab == null || canvas == null || scoreAnchor == null) return;

        Camera cam = null;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) cam = canvas.worldCamera;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, scoreAnchor.position);
        screen += scoreAnchorOffset;

        var go = Instantiate(popupPrefab, canvas.transform);
        go.transform.position = screen;

        // ★ スコア欄用にサイズを大きめに
        go.transform.localScale = Vector3.one * 1.5f;

        go.GetComponent<ScorePopup>().Setup(delta, color);
    }
}
