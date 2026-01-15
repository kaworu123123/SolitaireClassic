using UnityEngine;
using TMPro;

public class BestScoreLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        int best = 0;

        // 新（GameBestの累計）
        if (PlayerPrefs.HasKey("AllBestScore"))
            best = Mathf.Max(best, PlayerPrefs.GetInt("AllBestScore", 0));

        // 現行統一キー
        if (PlayerPrefs.HasKey(CardFactory.HighScoreKey))
            best = Mathf.Max(best, PlayerPrefs.GetInt(CardFactory.HighScoreKey, 0));

        // 旧キー（互換）
        if (PlayerPrefs.HasKey("HighScore"))
            best = Mathf.Max(best, PlayerPrefs.GetInt("HighScore", 0));

        if (label != null) label.text = $"Best: {best:n0}";
    }
}