using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("メニュー UI")]
    public Button startButton;
    public Button quitButton;

    private const string BestScoreKey = "BestScore";
    private const string GameSceneName = "MainScene";

    private void Awake()
    {
        if (startButton == null || quitButton == null)
            Debug.LogError("MainMenuController: ボタンがアサインされていません");

        // ボタンにリスナ登録
        startButton.onClick.AddListener(OnStartGame);
        quitButton.onClick.AddListener(OnQuit);
    }

    private void OnStartGame()
    {
        // ここでゲームシーン名を指定（例: "MainScene"）
        SceneManager.LoadScene("MainScene");
    }

    private void OnQuit()
    {
        // エディタ中は停止、ビルド後はアプリ終了
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    [Header("High Score Display")]
    public TextMeshProUGUI highScoreText;  // TMP を使う場合

    void Start()
    {
        BestScoreUtil.MigrateAll();
        int best = BestScoreUtil.GetUnifiedBest();
        highScoreText.text = $"ベストスコア: {best:n0}";
    }

    /// <summary>
    /// 現在のハイスコアを UI に反映します。
    /// </summary>
    public void UpdateHighScoreText()
    {
        int best = PlayerPrefs.GetInt(BestScoreKey, 0);
        highScoreText.text = best.ToString();
    }

    /// <summary>
    /// ハイスコアをリセットし、UI もすぐ更新します。
    /// </summary>
    public void ResetHighScore()
    {
        PlayerPrefs.DeleteKey(BestScoreKey);
        PlayerPrefs.Save();
        UpdateHighScoreText();
    }

#if UNITY_EDITOR
    // エディタ上で右クリックメニューからも呼べるようにしておくと便利
    [ContextMenu("Reset High Score (Editor)")]
    private void ResetHighScoreEditor()
    {
        ResetHighScore();
        UnityEditor.EditorUtility.SetDirty(highScoreText);
    }
#endif
}
