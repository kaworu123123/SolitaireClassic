using UnityEngine;
using UnityEngine.UI;

public class ButtonPlacer : MonoBehaviour
{
    [SerializeField] private RectTransform buttonRect;  // Inspector でこのボタンをアサイン
    [SerializeField] private Canvas canvas;             // Canvas もアサインしておく

    void Start()
    {
        var canvasRt = canvas.GetComponent<RectTransform>();
        float h = canvasRt.rect.height;

        // アンカーを画面中央下寄せに設定
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);

        // Y を「下から 1/3 の高さ」に
        buttonRect.anchoredPosition = new Vector2(0f, h / 3f);
    }
}
