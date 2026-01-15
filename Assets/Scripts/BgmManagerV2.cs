using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class BgmManagerV2 : MonoBehaviour
{
    public static BgmManagerV2 I { get; private set; }

    [Header("Tracks (assign in Inspector)")]
    public List<AudioClip> tracks = new();

    [Header("Audio Sources (2ch crossfade)")]
    public AudioSource sourceA;
    public AudioSource sourceB;

    [Header("Options")]
    [Tooltip("ランダム再生を行うか")]
    public bool isRandom = false;
    [Range(0f, 2f)] public float crossfadeSec = 1.0f;    // 曲間クロスフェード時間
    [Range(0f, 0.5f)] public float scheduleLead = 0.15f; // 予約再生の先行秒

    const string PP_KEY_IDX = "BGM_CurrentIndex";
    const string PP_KEY_RANDOM = "BGM_IsRandom";
    const string PP_KEY_MUTED = "BGM_IsMuted";

    [Header("Persistence")]
    public bool usePersistence = true;     // ← 保存/復元を使うか
    public bool autoStart = true;          // ← 起動時に自動再生
    public bool autoStartFromSaved = true; // ← 保存値があればそれを再生

    // --- 内部状態 ---
    int currentIndex = -1;
    AudioSource _active, _inactive;
    Coroutine _xfadeCo;

    // Mute / Pause
    public bool IsMuted { get; private set; } = false;

    // Ducking
    Coroutine _duckCo;
    float _baseVolA = 1f, _baseVolB = 1f; // Duck前の基準

    const string PP_KEY_VOL = "BGM_Volume";

    // 好みの初期値（0〜1）
    [Header("Volume")]
    [Range(0f, 1f)] public float musicVolume = 0.8f;

    Coroutine _saveCo;
    readonly WaitForSecondsRealtime _saveDelay = new WaitForSecondsRealtime(0.75f);

    Coroutine _autoNextCo;

    public event Action<int, string> OnTrackChanged; // (index, title)

    void NotifyTrackChanged()
    {
        if (tracks == null || currentIndex < 0 || currentIndex >= tracks.Count) return;
        var title = tracks[currentIndex] ? tracks[currentIndex].name : "(None)";
        OnTrackChanged?.Invoke(currentIndex, title);
    }

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this; DontDestroyOnLoad(gameObject);

        // safety
        sourceA.playOnAwake = false; sourceB.playOnAwake = false;
        sourceA.spatialBlend = 0f; sourceB.spatialBlend = 0f;
        sourceA.loop = false; sourceB.loop = false;
        sourceA.volume = 1f; sourceB.volume = 0f;

        _active = sourceA; _inactive = sourceB;

        if (usePersistence) LoadState();   // 以前の状態を読み込む

        if (autoStart && tracks != null && tracks.Count > 0)
        {
            if (autoStartFromSaved && currentIndex >= 0 && currentIndex < tracks.Count)
            {
                // 前回の設定でスタート（ランダムONでも“前回の曲”から始める）
                PlayIndex(currentIndex, loopSingle: !isRandom);
            }
            else
            {
                // 保存なし：通常の初期動作（ランダムならランダム、そうでなければ0番など）
                if (isRandom) PlayRandom();
                else PlayIndex(Mathf.Clamp(currentIndex, 0, tracks.Count - 1), loopSingle: true);
            }
        }

        SceneManager.sceneLoaded += (scene, mode) =>
        {
            RestoreDuckImmediate();  // ← 勝利後の再ロード/タイトル戻り直後でも戻す
        };
    }

    public void PlayIndex(int index, bool loopSingle = false, bool save = false)
    {
        if (tracks == null || tracks.Count == 0) return;
        index = Mathf.Clamp(index, 0, tracks.Count - 1);
        var next = tracks[index];
        if (next == null) return;

        // 初回
        if (!_active.isPlaying && !_inactive.isPlaying)
        {
            _active.clip = next;
            _active.loop = loopSingle && !isRandom;
            _active.volume = IsMuted ? 0f : musicVolume;
            _active.Play();
            currentIndex = index;
            NotifyTrackChanged();

            // ランダム継続なら、ここで次曲を先行予約しておく
            if (isRandom && !_active.loop && _active.clip != null)
            {
                if (_autoNextCo != null) { StopCoroutine(_autoNextCo); }
                _autoNextCo = StartCoroutine(AutoScheduleNextRandom(_active.clip.length));
            }
            if (save) SaveState();

            return;
        }

        // 以降はクロスフェード
        ScheduleCrossfade(next, loopSingle && !isRandom);
        currentIndex = index;
        if (save) SaveState();        // ← ここも条件付き
    }

    public void PlayRandom(bool save = false)
    {
        if (tracks == null || tracks.Count <= 0) return;
        int next = UnityEngine.Random.Range(0, tracks.Count);
        if (tracks.Count > 1 && next == currentIndex) next = (next + 1) % tracks.Count;
        PlayIndex(next, loopSingle: false, save: save);   // ← save引き継ぎ
    }

    void ScheduleCrossfade(AudioClip nextClip, bool loopSingle)
    {
        _inactive.clip = nextClip;
        _inactive.loop = loopSingle;
        _inactive.volume = IsMuted ? 0f : 0f; // ミュート時は0、通常時はフェードで上がる

        double start = AudioSettings.dspTime + scheduleLead;
        _inactive.PlayScheduled(start);

        if (_xfadeCo != null) StopCoroutine(_xfadeCo);
        _xfadeCo = StartCoroutine(CrossfadeRoutine(start, crossfadeSec));
    }

    IEnumerator CrossfadeRoutine(double startDspTime, float duration)
    {
        // 再生が始まるまで待つ
        double end = startDspTime + duration;
        while (AudioSettings.dspTime < startDspTime) yield return null;

        var a = _active; var b = _inactive;

        // Duck中に切替が来ても戻せるよう、基準は随時更新
        if (_duckCo == null) { _baseVolA = a.volume; _baseVolB = b.volume; }

        float t = 0f;
        while (AudioSettings.dspTime < end)
        {
            t = Mathf.Clamp01((float)((AudioSettings.dspTime - startDspTime) / duration));
            a.volume = IsMuted ? 0f : Mathf.Lerp(musicVolume, 0f, t);
            b.volume = IsMuted ? 0f : Mathf.Lerp(0f, musicVolume, t);
            yield return null;
        }
        a.volume = 0f; b.volume = IsMuted ? 0f : musicVolume;

        a.Stop();

        // スワップ
        _active = b; _inactive = a;
        _xfadeCo = null;
        NotifyTrackChanged();

        // ランダム継続：曲が終わる手前で次を予約
        if (isRandom && !_active.loop && _active.clip != null)
        {
            if (_autoNextCo != null) { StopCoroutine(_autoNextCo); }
            _autoNextCo = StartCoroutine(AutoScheduleNextRandom(_active.clip.length));
        }
    }

    IEnumerator AutoScheduleNextRandom(float length)
    {
        float wait = Mathf.Max(0f, length - (crossfadeSec + scheduleLead + 0.1f));
        yield return new WaitForSeconds(wait);
        PlayRandom(save: false);   // ← ここは必ず false
    }

    // ====== 互換API（旧 BgmManager と同名） ======

    // 情報系
    public int TrackCount => tracks?.Count ?? 0;
    public string GetTrackTitle(int index)
    {
        if (tracks == null || index < 0 || index >= tracks.Count) return "(None)";
        var c = tracks[index]; return c ? c.name : "(Unnamed Clip)";
    }
    public int GetCurrentIndex() => currentIndex;
    public bool GetIsRandom() => isRandom;
    public bool GetIsMuted() => IsMuted;

    // 制御系
    public void Play(int index) => PlayIndex(index, loopSingle: !isRandom);

    public void SetRandom(bool on)
    {
        isRandom = on;
        SaveState();
        
           // すでに再生中にONになったら、今の曲の終わりに向けて予約
           if (on && _active && _active.clip != null && !_active.loop)
               {
                   if (_autoNextCo != null) { StopCoroutine(_autoNextCo); }
            _autoNextCo = StartCoroutine(AutoScheduleNextRandom(_active.clip.length));
               }
    }

    public void SetMute(bool mute)
    {
        IsMuted = mute;
        if (_active) _active.mute = mute;
        if (_inactive) _inactive.mute = mute;

        // volumeもゼロにして“完全ミュート”
        if (mute)
        {
            if (_duckCo != null) { StopCoroutine(_duckCo); _duckCo = null; }
            if (_active) _active.volume = 0f;
            if (_inactive) _inactive.volume = 0f;
        }
        else
        {
            // Duck中でなければ基準音量へ戻す（待機側は0のまま）
            if (_duckCo == null)
            {
                if (_active) _active.volume = musicVolume;
                if (_inactive) _inactive.volume = 0f;
            }
        }

        SaveState();
    }

    // 一時停止（旧API名そのまま）
    public void PauseBGM()
    {
        if (_active) _active.Pause();
        if (_inactive) _inactive.Pause();
    }
    public void ResumeBGM()
    {
        if (_active && _active.clip) _active.UnPause();
        if (_inactive && _inactive.clip) _inactive.UnPause();
    }

    // Duck（勝利演出などでBGMを一時的に下げる）
    public void DuckToFraction(float fraction, float fadeSec)
    {
        fraction = Mathf.Clamp01(fraction);
        if (_duckCo != null) StopCoroutine(_duckCo);
        _duckCo = StartCoroutine(CoDuckToFraction(fraction, Mathf.Max(0f, fadeSec)));
    }
    public void RestoreFromDuck(float fadeSec)
    {
        if (_duckCo != null) StopCoroutine(_duckCo);
        _duckCo = StartCoroutine(CoRestoreFromDuck(Mathf.Max(0f, fadeSec)));
    }

    IEnumerator CoDuckToFraction(float fraction, float fadeSec)
    {
        if (!_active && !_inactive) yield break;

        // 基準化
        _baseVolA = _active ? _active.volume : 0f;
        _baseVolB = _inactive ? _inactive.volume : 0f;

        float t = 0f;
        while (t < fadeSec)
        {
            t += Time.unscaledDeltaTime;
            float k = (fadeSec <= 0f) ? 1f : (t / fadeSec);
            if (_active) _active.volume = _baseVolA * Mathf.Lerp(1f, fraction, k);
            if (_inactive) _inactive.volume = _baseVolB * Mathf.Lerp(1f, fraction, k);
            yield return null;
        }
        if (_active) _active.volume = _baseVolA * fraction;
        if (_inactive) _inactive.volume = _baseVolB * fraction;
        _duckCo = null;
    }

    IEnumerator CoRestoreFromDuck(float fadeSec)
    {
        if (!_active && !_inactive) yield break;

        float startA = _active ? _active.volume : 0f;
        float startB = _inactive ? _inactive.volume : 0f;

        float t = 0f;
        while (t < fadeSec)
        {
            t += Time.unscaledDeltaTime;
            float k = (fadeSec <= 0f) ? 1f : (t / fadeSec);
            if (_active) _active.volume = Mathf.Lerp(startA, _baseVolA, k);
            if (_inactive) _inactive.volume = Mathf.Lerp(startB, _baseVolB, k);
            yield return null;
        }
        if (_active) _active.volume = _baseVolA;
        if (_inactive) _inactive.volume = _baseVolB;
        _duckCo = null;
    }

    // 既存の補助APIも残す
    public void ToggleRandom(bool on) => isRandom = on;
    public void NextRandom() => PlayRandom();
    public void ReplayLoop() { if (currentIndex >= 0) PlayIndex(currentIndex, loopSingle: true); }

    public void SaveState(bool immediate = false)
    {
        if (!usePersistence) return;

        PlayerPrefs.SetInt(PP_KEY_IDX, currentIndex);
        PlayerPrefs.SetInt(PP_KEY_RANDOM, isRandom ? 1 : 0);
        PlayerPrefs.SetInt(PP_KEY_MUTED, IsMuted ? 1 : 0);
        PlayerPrefs.SetFloat(PP_KEY_VOL, musicVolume);

        // ① 即時保存指定 or ② コンポーネント非アクティブ の場合は、その場で保存
        if (immediate || !isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            PlayerPrefs.Save();
            return;
        }

        // それ以外はデバウンス（0.75s後に1回だけ）
        if (_saveCo != null) StopCoroutine(_saveCo);
        _saveCo = StartCoroutine(CoFlushPrefs());
    }

    IEnumerator CoFlushPrefs()
    {
        yield return _saveDelay;
        PlayerPrefs.Save();
        _saveCo = null;
    }

    public void LoadState()
    {
        if (!usePersistence) return;

        // 先に index / random を読む
        if (PlayerPrefs.HasKey(PP_KEY_IDX))
            currentIndex = Mathf.Clamp(PlayerPrefs.GetInt(PP_KEY_IDX, -1), -1, (tracks?.Count ?? 1) - 1);

        if (PlayerPrefs.HasKey(PP_KEY_RANDOM))
            isRandom = PlayerPrefs.GetInt(PP_KEY_RANDOM, 0) != 0;

        // ミュートは“保存せずに”適用
        bool muted = PlayerPrefs.GetInt(PP_KEY_MUTED, 0) != 0;
        IsMuted = muted;
        if (_active) _active.mute = muted;
        if (_inactive) _inactive.mute = muted;

        if (PlayerPrefs.HasKey(PP_KEY_VOL))
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PP_KEY_VOL, musicVolume));
    }

    // アプリ終了/破棄時にも保険で保存
    void OnApplicationQuit()
    {
        SaveState(immediate: true);
    }

    void OnApplicationPause(bool pause)
    {
        if (pause) { SaveState(immediate: true); }
    }

    void OnDestroy()
    {
        // 破棄時は必ず即時保存（ここで StartCoroutine はしない）
        SaveState(immediate: true);
    }

    void OnDisable()
    {
        // 進行中の遅延保存があれば止める（任意だが安心）
        if (_saveCo != null) { StopCoroutine(_saveCo); _saveCo = null; }
    }

    public void SetMusicVolume(float v, bool save = true)
    {
        musicVolume = Mathf.Clamp01(v);
        if (!IsMuted && _duckCo == null)
        {
            if (_active) _active.volume = musicVolume;
            if (_inactive) _inactive.volume = 0f;
        }
        if (save) SaveState();
    }
    public float GetMusicVolume() => musicVolume;

    public void RestoreDuckImmediate()
    {
        if (_duckCo != null) { StopCoroutine(_duckCo); _duckCo = null; }
        if (_active) _active.volume = IsMuted ? 0f : musicVolume;
        if (_inactive) _inactive.volume = 0f; // 待機側は0
    }

    // トラック総数を返す
    public int GetTrackCount() => tracks != null ? tracks.Count : 0;

    // 次のトラックへ（ループで0に戻る）
    public void PlayNext(bool save = true)
    {
        int count = GetTrackCount();
        if (count == 0) return;
        int cur = Mathf.Clamp(GetCurrentIndex(), 0, count - 1);
        int next = (cur + 1) % count;
        PlayIndex(next, loopSingle: !isRandom, save: save);
    }

    // 前のトラックへ（ループで末尾へ）
    public void PlayPrev(bool save = true)
    {
        int count = GetTrackCount();
        if (count == 0) return;
        int cur = Mathf.Clamp(GetCurrentIndex(), 0, count - 1);
        int prev = (cur - 1 + count) % count;
        PlayIndex(prev, loopSingle: !isRandom, save: save);
    }

    // ミュートをトグル
    public void ToggleMute()
    {
        SetMute(!GetIsMuted());
    }
}
