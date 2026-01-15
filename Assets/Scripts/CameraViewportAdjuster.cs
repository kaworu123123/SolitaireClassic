using UnityEngine;

public class CameraViewportAdjuster : MonoBehaviour
{
    [Tooltip("ã•”‚É‚ ‚é AdContainer ‚Ì RectTransform")]
    [SerializeField] RectTransform adContainer;

    [Tooltip("ã•”‚É‚ ‚é StatsBar ‚Ì RectTransform")]
    [SerializeField] RectTransform statsBar;

    void Start()
    {
        // UI ‚Ì‚‚³‚ğ‘«‚µ‡‚í‚¹
        float uiHeight = adContainer.rect.height + statsBar.rect.height;

        // ƒJƒƒ‰‚Ì•`‰æ—Ìˆæ‚ğã•” UI •ª‚¾‚¯‰º‚É‚¸‚ç‚·
        Camera cam = GetComponent<Camera>();
        cam.pixelRect = new Rect(
            0,
            0,
            Screen.width,
            Screen.height - uiHeight
        );
    }
}
