using UnityEngine;
using UnityEngine.UI;
using TMPro; // ← 追加（TextMeshPro）

public class ConfirmRestartPanel : MonoBehaviour
{
    [Header("BGM UI")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button muteButton;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text trackNameText; // ← TMP固定

    private void OnEnable()
    {
        HookEvents(true);
        RefreshUI();
    }

    private void OnDisable()
    {
        HookEvents(false);
    }

    private void HookEvents(bool on)
    {
        if (prevButton)
        {
            prevButton.onClick.RemoveListener(OnPrev);
            if (on) prevButton.onClick.AddListener(OnPrev);
        }
        if (nextButton)
        {
            nextButton.onClick.RemoveListener(OnNext);
            if (on) nextButton.onClick.AddListener(OnNext);
        }
        if (muteButton)
        {
            muteButton.onClick.RemoveListener(OnToggleMute);
            if (on) muteButton.onClick.AddListener(OnToggleMute);
        }
        if (volumeSlider)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            if (on) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
    }

    private void OnPrev()
    {
        var m = BgmManagerV2.I; if (m == null) return;
        m.PlayPrev();
        RefreshUI();
    }

    private void OnNext()
    {
        var m = BgmManagerV2.I; if (m == null) return;
        m.PlayNext();
        RefreshUI();
    }

    private void OnToggleMute()
    {
        var m = BgmManagerV2.I; if (m == null) return;
        m.ToggleMute();
        RefreshUI();
    }

    private void OnVolumeChanged(float v)
    {
        var m = BgmManagerV2.I; if (m == null) return;
        m.SetMusicVolume(v);
        RefreshUI(false); // ボリュームだけ更新
    }

    private void RefreshUI(bool refreshAll = true)
    {
        var m = BgmManagerV2.I; if (m == null) return;

        if (volumeSlider)
            volumeSlider.SetValueWithoutNotify(m.GetMusicVolume());

        if (!refreshAll) return;

        // 曲名表示（TMP）
        if (trackNameText)
        {
            int idx = m.GetCurrentIndex();
            string title = (idx >= 0) ? m.GetTrackTitle(idx) : "-";
            trackNameText.text = string.IsNullOrEmpty(title) ? "-" : title;
        }

        // ミュートボタンのラベル（TMP優先、無ければText）
        if (muteButton)
        {
            var tmp = muteButton.GetComponentInChildren<TMP_Text>();
            if (tmp) tmp.text = m.GetIsMuted() ? "Unmute" : "Mute";
            else
            {
                //var legacy = muteButton.GetComponentInChildren<Text>();
                //if (legacy) legacy.text = m.GetIsMuted() ? "Unmute" : "Mute";
            }
        }
    }
}
