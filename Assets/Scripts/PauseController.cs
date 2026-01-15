using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PauseController : MonoBehaviour
{
    [SerializeField] private Button pauseButton;
    [SerializeField] private Text pauseButtonText;        // TMPならTMP_Textに置き換え可
    [SerializeField] private GameObject pauseOverlay;     // 「PAUSED」表示パネル
    [SerializeField] private ScoreTimerManager stm;       // ゲーム内タイマー
    [SerializeField] private CardFactory factory;         // 入力ブロック用

    // どこからでもPause状態を参照できるようにする（ComboTimerUI 等で使用）
    public static bool IsPaused { get; private set; }

    private bool isPaused;

    private void Awake()
    {
        if (pauseButton) pauseButton.onClick.AddListener(TogglePause);

        // 参照が未割り当てなら自動で拾う（任意）
        if (!stm) stm = FindObjectOfType<ScoreTimerManager>(true);
        if (!factory) factory = FindObjectOfType<CardFactory>(true);

        SetUI(false);
    }

    private void OnDestroy()
    {
        if (pauseButton) pauseButton.onClick.RemoveListener(TogglePause);
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        IsPaused = isPaused; // 静的フラグにも反映

        // 1) 時間停止（ほぼ全てのUpdateが止まる）
        Time.timeScale = isPaused ? 0f : 1f;

        // 2) スコアタイマー停止/再開
        if (stm != null)
        {
            if (isPaused) stm.PauseTimer();
            else stm.ResumeTimer();
        }

        // 3) 入力を止める（CardFactoryの入力許可を切り替え）
        if (factory != null)
            factory.SetInputEnabled(!isPaused);

        // 4) Tweenを停止/再開（独立更新のDOTween対策）
        if (isPaused) DOTween.PauseAll();
        else DOTween.PlayAll();

        AudioListener.pause = isPaused;

        // 5) BGMをミュート/解除（BgmManager を使用）
        var bgm = BgmManagerV2.I ?? FindObjectOfType<BgmManagerV2>(true);
        if (bgm != null) bgm.SetMute(isPaused);

        // 6) オーバーレイ＆ボタン表示
        SetUI(isPaused);
    }

    private void SetUI(bool paused)
    {
        if (pauseOverlay) pauseOverlay.SetActive(paused);

        if (pauseButtonText)
            pauseButtonText.text = paused ? "RESUME" : "PAUSE"; // 「再開 / 一時停止」
        // （TMP_Text を使う場合は型を差し替え）
    }
}
