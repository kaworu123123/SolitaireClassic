using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;
using System.Collections;
using Solitaire.Stats;   // ← これが超重要（GameBest を解決）

public class VictoryUIController : MonoBehaviour
{
    [Header("UI ボタン")]
    public Button retryButton;
    public Button menuButton;

    [Header("今回の結果")]
    public TextMeshProUGUI txtScore;
    public TextMeshProUGUI txtMoves;
    public TextMeshProUGUI txtTime;

    [Header("累計ベスト")]
    public TextMeshProUGUI txtBestScore;
    public TextMeshProUGUI txtBestMoves;
    public TextMeshProUGUI txtBestTime;

    [Header("今日のベスト")]
    public TextMeshProUGUI txtTodayScore;
    public TextMeshProUGUI txtTodayMoves;
    public TextMeshProUGUI txtTodayTime;

    [Header("勝利パネル")]
    public GameObject victoryPanel;      // ルート
    public Animator popupAnimator;       // 任意
    public ParticleSystem confettiEffect;

    [Header("SE")]
    public AudioSource sfxSource;        // 任意（未設定なら自動フォールバック）
    public AudioClip winClip;            // 通常勝利SE
    public AudioClip newRecordClip;      // 記録更新SE（共通）

    [Header("更新時アニメ(Animatorがあるなら pulse トリガー)")]
    public Animator animBestScore;
    public Animator animBestMoves;
    public Animator animBestTime;
    public Animator animTodayScore;
    public Animator animTodayMoves;
    public Animator animTodayTime;

    [Header("オプション")]
    public bool onlyPlayOnRecord = true;     // true: 更新時のみSE再生
    public float recordSfxInterval = 0.2f;   // 複数同時更新時の間隔

    private CanvasGroup victoryCg;
    private bool _didPlayRecordFx = false;

    void Awake()
    {
        if (!victoryPanel)
        {
            Debug.LogError("[VictoryUI] victoryPanel 未設定");
            return;
        }

        victoryPanel.SetActive(true); // CanvasGroupで制御
        victoryCg = victoryPanel.GetComponent<CanvasGroup>();
        if (!victoryCg) victoryCg = victoryPanel.AddComponent<CanvasGroup>();
        victoryCg.alpha = 0f;
        victoryCg.blocksRaycasts = false;
        victoryCg.interactable = false;

        if (retryButton) retryButton.onClick.AddListener(OnRetry);
        if (menuButton) menuButton.onClick.AddListener(OnMenu);
    }

    void OnEnable()
    {
        // OnEnable経由では演出は抑止（多重再生防止）
        _didPlayRecordFx = true;
        UpdateResultsPanel(commitRecords: false);
        StartCoroutine(CoFillResults(false));
    }

    IEnumerator CoFillResults(bool commitRecords)
    {
        // 初期1〜2フレの差し込み対策（テキストだけ再反映）
        for (int i = 0; i < 2; i++)
        {
            UpdateResultsPanel(commitRecords);
            yield return null;
        }
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void LateUpdate()
    {
        if (victoryCg && !victoryCg.interactable)
        {
            victoryCg.interactable = true;
            victoryCg.blocksRaycasts = true;
        }
    }

    // ====== 公開API（勝利時に呼ぶ） ======
    public void ShowVictory()
    {
        var stm = ScoreTimerManager.Instance;
        // タイム確定 & タイムボーナス
        stm?.SnapshotFinalTime();
        if (ScoreManager.Instance != null && stm != null)
        {
            ScoreManager.Instance.timedGame = true;
            ScoreManager.Instance.AddScore(ScoreAction.ClearTimeBonus); // 必要ならボーナス方式を変更
        }

        EnsureEventSystem();

        if (!victoryPanel.activeSelf) victoryPanel.SetActive(true);

        // 一度きり演出を許可
        _didPlayRecordFx = false;

        UpdateResultsPanel(commitRecords: true);

        StopAllCoroutines();
        StartCoroutine(FadeInVictory());

        // 勝利パネルをCanvas内の最前面へ（兄弟順）
        if (victoryPanel) victoryPanel.transform.SetAsLastSibling();

        if (popupAnimator)
        {
            if (HasTrigger(popupAnimator, "Play")) popupAnimator.SetTrigger("Play");
            else popupAnimator.Play(0, 0, 0f);
        }

        if (confettiEffect) confettiEffect.Play();

        static bool HasTrigger(Animator a, string name)
        {
            if (!a) return false;
            foreach (var p in a.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) return true;
            return false;
        }
    }

    // ====== 中身更新（表示＆保存＆演出） ======
    private void UpdateResultsPanel(bool commitRecords)
    {
        var sm = ScoreManager.Instance;   // 現在のスコア等
        var stm = ScoreTimerManager.Instance;

        int curScore = sm ? sm.CurrentScore : 0;
        int curMoves = stm ? stm.CurrentMoves : 0;
        float curTime = stm ? (stm.HasFinalTime ? stm.FinalTimeSec : Mathf.Max(0, stm.CurrentTime)) : 0f;

        // 今回表示
        if (txtScore) txtScore.text = $"スコア：{curScore}";
        if (txtMoves) txtMoves.text = $"ムーブ：{curMoves}";
        if (txtTime) txtTime.text = $"タイム：{FormatTime(curTime)}";

        // 保存＆フラグ
        GameBest.SanitizeIfNeeded(); // 古い0値を掃除（Todayの日付もチェック）
        GameBest.UpdateFlags flags = commitRecords
            ? GameBest.UpdateAllAndToday(curScore, curMoves, curTime)
            : default;

        // 最新値の読み出し
        var all = GameBest.GetAllTime();  // Snapshot?
        var today = GameBest.GetToday();    // Snapshot?

        if (txtBestScore) txtBestScore.text = all.HasValue ? $"マイベストスコア：{all.Value.Score}" : "マイベストスコア：-";
        if (txtBestMoves) txtBestMoves.text = all.HasValue ? $"マイベストムーブ：{all.Value.Moves}" : "マイベストムーブ：-";
        if (txtBestTime) txtBestTime.text = all.HasValue ? $"マイベストタイム：{FormatTime(all.Value.TimeSec)}" : "マイベストタイム：-";

        if (txtTodayScore) txtTodayScore.text = today.HasValue ? $"今日のベストスコア：{today.Value.Score}" : "今日のベストスコア：-";
        if (txtTodayMoves) txtTodayMoves.text = today.HasValue ? $"今日のベストムーブ：{today.Value.Moves}" : "今日のベストムーブ：-";
        if (txtTodayTime) txtTodayTime.text = today.HasValue ? $"今日のベストタイム：{FormatTime(today.Value.TimeSec)}" : "今日のベストタイム：-";

        // ここから演出（commitRecords=true のときだけ）
        if (!commitRecords) return;

        bool anyRecord =
            flags.NewAllTimeScore || flags.NewAllTimeMoves || flags.NewAllTimeTime ||
            flags.NewTodayScore || flags.NewTodayMoves || flags.NewTodayTime;

        // 累計ベストの演出
        if (flags.NewAllTimeScore) PlayBestFX(animBestScore, txtBestScore);
        if (flags.NewAllTimeMoves) PlayBestFX(animBestMoves, txtBestMoves);
        if (flags.NewAllTimeTime) PlayBestFX(animBestTime, txtBestTime);

        // 今日ベストの演出
        if (flags.NewTodayScore) PlayBestFX(animTodayScore, txtTodayScore);
        if (flags.NewTodayMoves) PlayBestFX(animTodayMoves, txtTodayMoves);
        if (flags.NewTodayTime) PlayBestFX(animTodayTime, txtTodayTime);

        // ミラー発火（今日更新で見た目として累計と同値/優位なら累計側も光らせる）
        if (today.HasValue && all.HasValue)
        {
            if (flags.NewTodayScore && today.Value.Score >= all.Value.Score) PlayBestFX(animBestScore, txtBestScore);
            if (flags.NewTodayMoves && today.Value.Moves <= all.Value.Moves) PlayBestFX(animBestMoves, txtBestMoves);
            if (flags.NewTodayTime && today.Value.TimeSec <= all.Value.TimeSec && today.Value.TimeSec > 0f)
                PlayBestFX(animBestTime, txtBestTime);
        }

        // SE（更新時だけ or 常時）— 同一表示での多重再生防止
        if (!_didPlayRecordFx)
        {
            if (anyRecord)
            {
                if (newRecordClip) PlaySfx(newRecordClip);
            }
            else
            {
                if (!onlyPlayOnRecord && winClip) PlaySfx(winClip);
            }
            _didPlayRecordFx = true; // ← ここで一度きりにする
        }
    }
    // ====== フェード表示 ======
    private IEnumerator FadeInVictory()
    {
        victoryCg.DOKill();
        victoryCg.alpha = 0f;
        victoryCg.blocksRaycasts = true;
        victoryCg.interactable = true;

        float d = 0.30f, t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            victoryCg.alpha = Mathf.SmoothStep(0f, 1f, t / d);
            yield return null;
        }
        victoryCg.alpha = 1f;
    }

    // ====== 更新時の演出（Animator優先／無ければDOTween＋虹） ======
    private void PlayBestFX(Animator anim, TMP_Text text)
    {
        if (!text) return;

        // Animatorがあれば pulse トリガー
        if (anim)
        {
            anim.ResetTrigger("pulse");
            anim.SetTrigger("pulse");
        }
        else
        {
            // フォールバック：ポップ
            var rt = text.transform as RectTransform;
            rt.DOKill(true);
            var baseScale = rt.localScale;
            DOTween.Sequence()
                .Append(rt.DOScale(baseScale * 1.2f, 0.18f).SetEase(Ease.OutBack))
                .Append(rt.DOScale(baseScale, 0.20f).SetEase(Ease.InOutSine));
        }

        // 虹エフェクト（パネルを閉じるまで）
        var r1 = text.GetComponent<RainbowTMP_v2>();
        if (r1)
        {
            r1.enabled = true;   // 念のため有効化
            r1.Play();           // ★これがないと動かない（playOnAwake=false対策）
        }

        var r2 = text.GetComponent<KiraKiraText>();
        if (r2)
        {
            r2.enabled = true;   // 念のため有効化
            r2.Play();           // ★これも明示的に再生
        }
    }

    // ====== ユーティリティ ======
    private void PlaySfx(AudioClip clip)
    {
        if (!clip) return;

        if (sfxSource && sfxSource.isActiveAndEnabled && sfxSource.gameObject.activeInHierarchy)
        {
            sfxSource.PlayOneShot(clip);
            return;
        }
        // フォールバック：一発音
        var listener = FindObjectOfType<AudioListener>();
        Vector3 pos = Camera.main ? Camera.main.transform.position :
                       (listener ? listener.transform.position : Vector3.zero);
        AudioSource.PlayClipAtPoint(clip, pos);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null && EventSystem.current.isActiveAndEnabled) return;

        var go = new GameObject("EventSystem (auto)");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        if (!go.TryGetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(out _))
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        if (!go.TryGetComponent<StandaloneInputModule>(out _))
            go.AddComponent<StandaloneInputModule>();
#endif
    }

    private static string FormatTime(float secF)
    {
        int sec = Mathf.RoundToInt(Mathf.Max(0f, secF));
        int m = sec / 60;
        int s = sec % 60;
        return $"{m:00}:{s:00}";
    }

    private void OnRetry()
    {
        if (victoryPanel) victoryPanel.SetActive(false);
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    private void OnMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("[SceneLoaded] " + scene.name + " mode=" + mode);
    }
}
