using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class RestartButtonHandler : MonoBehaviour
{
    [Header("UI ボタン")]
    [Tooltip("メインのリスタートボタン")]
    public Button restartButton;

    [Header("確認ダイアログ")]
    [Tooltip("リスタート確認用パネル (ConfirmRestartPanel)")]
    public GameObject confirmRestartPanel;
    [Tooltip("確認ダイアログの Yes ボタン")]
    public Button retryButton;
    [Tooltip("確認ダイアログの No ボタン")]
    public Button cancelButton;
    [Tooltip("確認ダイアログの End ボタン")]
    public Button endButton;


    private CanvasGroup _confirmCg;

    private void Awake()
    {
        // 各参照がセットされているかチェック
        if (restartButton == null || confirmRestartPanel == null
            || retryButton == null || cancelButton == null|| endButton == null)
        {
            Debug.LogError("RestartButtonHandler: ボタン or パネルがアサインされていません");
            enabled = false;
            return;
        }

        // CanvasGroup をキャッシュ（なければ追加）
        _confirmCg = confirmRestartPanel.GetComponent<CanvasGroup>();
        if (_confirmCg == null)
            _confirmCg = confirmRestartPanel.AddComponent<CanvasGroup>();

        // 最初は非表示かつレイキャストもカット
        confirmRestartPanel.SetActive(false);
        _confirmCg.blocksRaycasts = false;

        // リスナー登録
        restartButton.onClick.AddListener(OnRestartClicked);
        retryButton.onClick.AddListener(OnConfirmYes);
        cancelButton.onClick.AddListener(OnConfirmNo);
        endButton.onClick.AddListener(OnConfirmEnd);
    }

    // スタートボタン押下 → 確認パネルを出す
    private void OnRestartClicked()
    {
        // ① Canvas 内で「つねに最前面」に
        //confirmRestartPanel.transform.SetAsLastSibling();
        if (GameState.IsUiBlocked) return;   // VictoryOpen || AutoCollecting

        // ② そのうえで表示
        confirmRestartPanel.SetActive(true);

        Time.timeScale = 0f;
        var stm = FindObjectOfType<ScoreTimerManager>();
        if (stm != null) stm.PauseTimer();   // ← timer を止める
        DG.Tweening.DOTween.PauseAll();      // ← Tween も止める（使っているため）

        // （もし CanvasGroup を使っているなら、念のためこちらも有効化）
        var cg = confirmRestartPanel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    // Yes 押下 → シーン再読み込み
    private void OnConfirmYes()
    {
        // ★ここで必ず解除してからリロード
        InputGate.ForceClear();

        // パネルを隠してからレイキャスト遮断
        confirmRestartPanel.SetActive(false);
        _confirmCg.blocksRaycasts = false;

        // スコア/タイマーリセット
        //    var stm = FindObjectOfType<ScoreTimerManager>();
        //    if (stm != null) stm.ResetAll();
        // ロード前に timeScale を必ず戻す（次シーンに凍結を持ち込まない）
        Time.timeScale = 1f;
        DG.Tweening.DOTween.PlayAll();
        // スコア/タイマーはリロードで初期化されるが、明示的に消したいなら ResetAll
        var stm = FindObjectOfType<ScoreTimerManager>();
        if (stm != null) stm.ResetAll();


        // シーンリロード
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // No 押下 → パネルを閉じるだけ
    private void OnConfirmNo()
    {
        // パネルを隠してレイキャスト遮断
        confirmRestartPanel.SetActive(false);
        _confirmCg.blocksRaycasts = false;

        // 一時停止解除
        Time.timeScale = 1f;
        var stm = FindObjectOfType<ScoreTimerManager>();
        if (stm != null) stm.ResumeTimer();
        DG.Tweening.DOTween.PlayAll();
    }

    // ← ここに End ボタン用メソッドを追加
    private void OnConfirmEnd()
    {
        // エディタ再生中なら停止
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

