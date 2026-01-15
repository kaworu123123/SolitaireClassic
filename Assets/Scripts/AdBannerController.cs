using UnityEngine;
using System;
using UnityEngine.SceneManagement;   // �� �ǉ�
#if UNITY_ANDROID || UNITY_IOS
using GoogleMobileAds.Api;
using System.Collections.Generic;
#endif

public class AdBannerController : MonoBehaviour
{
    public static int LastTopInsetPx { get; private set; } = 0;

    public static event Action<int> OnTopInsetChangedPx;

    [Header("Editor Simulation")]
    [SerializeField] bool simulateInEditor = true;
    [SerializeField] int simulatedTopPx = 120;
    [SerializeField] bool liveSimInEditor = true;

    [Header("Show Only In These Scenes")]
    [SerializeField] string[] showInSceneNames = new[] { "MainScene" }; // �� �����ɕ\���������V�[�������

#if UNITY_ANDROID || UNITY_IOS
    private BannerView bannerView;
#endif
    int _lastSentSimPx = -1;

    void Awake()
    {
        // �V���O���g���ȈՉ��i�C�Ӂj
        var existing = FindObjectsOfType<AdBannerController>();
        if (existing.Length > 1) { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject); // �� �풓
        Log.D("[AdBannerController] Awake (DontDestroyOnLoad)");
        SceneManager.activeSceneChanged += OnActiveSceneChanged; // �� �V�[���ύX�Ď�
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    void Start()
    {
        // �N������̃V�[���ɑ΂��Ă���Ԃ𔽉f
        HandleSceneBanners(SceneManager.GetActiveScene().name);
    }

    void OnActiveSceneChanged(Scene prev, Scene next)
    {
        HandleSceneBanners(next.name);
    }

    bool ShouldShowInScene(string sceneName)
    {
        if (showInSceneNames == null || showInSceneNames.Length == 0) return false;
        foreach (var s in showInSceneNames)
            if (!string.IsNullOrEmpty(s) && s == sceneName) return true;
        return false;
    }

    void HandleSceneBanners(string sceneName)
    {
        Log.D($"[AdBannerController] HandleSceneBanners scene={sceneName}");
        if (ShouldShowInScene(sceneName))
        {
            ShowOrCreateBannerIfNeeded();
        }
        else
        {
            HideAndDisposeBanner();
            // ���炵�ʂ�0�ɖ߂�
            LastTopInsetPx = 0;
            OnTopInsetChangedPx?.Invoke(0);

#if UNITY_EDITOR
            _lastSentSimPx = -1;
#endif
        }
    }

    // �R���|�[�l���g�̃��j���[����Ăׂ�g�đ��h��
    [ContextMenu("Re-Emit Last Inset")]
    public void ReemitLastInset()
    {
        Log.D($"[AdBannerController] Re-emit inset={LastTopInsetPx}");
        OnTopInsetChangedPx?.Invoke(LastTopInsetPx);
    }

    void ShowOrCreateBannerIfNeeded()
    {
#if UNITY_ANDROID || UNITY_IOS

    if (LastTopInsetPx > 0)
        OnTopInsetChangedPx?.Invoke(LastTopInsetPx);

        if (bannerView == null)
        {
            CreateAndLoadBanner();
        }
        // AdMob��BannerView�̓��[�h�������� OnBannerAdLoaded �ō���px��ʒm
        // �����ł͓��ɒǉ������s�v
#else
        // Editor�V�~�����[�V�����iMainScene�̂Ƃ������j
        if (simulateInEditor)
        {
            if (liveSimInEditor)
            {
                if (_lastSentSimPx != simulatedTopPx)
                {
                    _lastSentSimPx = simulatedTopPx;
                    Log.D($"[AdBannerController] send inset(live)={simulatedTopPx} (EDITOR)");
                    LastTopInsetPx = simulatedTopPx;
                    OnTopInsetChangedPx?.Invoke(simulatedTopPx);
                }
            }
            else
            {
                // ��x��������ꍇ
                if (_lastSentSimPx != simulatedTopPx)
                {
                    _lastSentSimPx = simulatedTopPx;
                    Log.D($"[AdBannerController] send inset={simulatedTopPx} (EDITOR)");
                    OnTopInsetChangedPx?.Invoke(simulatedTopPx);
                }
            }
        }
#endif
    }

    void HideAndDisposeBanner()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (bannerView != null)
        {
            try
            {
                bannerView.Destroy();
            }
            catch (Exception e)
            {
                Log.W($"[AdBannerController] Destroy error: {e}");
            }
            bannerView = null;
        }
#endif
    }

#if UNITY_ANDROID || UNITY_IOS
    void CreateAndLoadBanner()
    {
string adUnitId =
#if UNITY_ANDROID
    "ca-app-pub-9741453050715050/2621754243"; // �e�X�g�p�o�i�[(Android)
#elif UNITY_IOS
    "ca-app-pub-3940256099942544/2934735716"; // �e�X�g�p�o�i�[(iOS)
#else
    "unexpected_platform";
#endif

        Log.D($"[AdBannerController] CreateAndLoadBanner adUnitId={adUnitId}");

        // �[�̐؂�ɂ����A�_�v�e�B�u����
        AdSize adSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
        bannerView = new BannerView(adUnitId, adSize, AdPosition.Top);

        bannerView.OnBannerAdLoaded += () =>
        {
            int heightPx = Mathf.RoundToInt((float)bannerView.GetHeightInPixels());
            Log.D($"[AdBannerController] OnBannerAdLoaded heightPx={heightPx} (MOBILE)");
            LastTopInsetPx = heightPx;   
            OnTopInsetChangedPx?.Invoke(heightPx);
        };
        bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            Log.W($"[AdBannerController] OnBannerAdLoadFailed: {error}");
            LastTopInsetPx = 0;                                  
            OnTopInsetChangedPx?.Invoke(0);
        };

        var request = new AdRequest();
        Log.D("[AdBannerController] bannerView.LoadAd(request)...");
        bannerView.LoadAd(request);
    }
#endif
}
