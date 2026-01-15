using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BgmUIController : MonoBehaviour
{
    [Header("Top label (button)")]
    public Button currentTitleButton;
    public TMP_Text currentTitleTextTMP;
    public Text currentTitleTextLegacy;

    [Header("Dropdown panel")]
    public GameObject listPanelRoot;
    public Transform listContainer;
    public Button listItemButtonPrefab;

    const int INDEX_RANDOM = -1;
    const int INDEX_MUTE = -2;

    void Start()
    {
        if (currentTitleButton != null)
            currentTitleButton.onClick.AddListener(ToggleListPanel);

        BuildList();
        SyncFromManager();
        CloseList();
    }

    void BuildList()
    {
        foreach (Transform child in listContainer) Destroy(child.gameObject);

        var m = BgmManagerV2.I;
        if (m == null) return;


        // 特殊ボタン
        MakeItemButton("♫ ランダム ♫", INDEX_RANDOM);
        MakeItemButton("ミュート", INDEX_MUTE);
        // 曲ボタン
        for (int i = 0; i < m.TrackCount; i++)
        {
            MakeItemButton(m.GetTrackTitle(i), i);
        }


    }

    void MakeItemButton(string label, int index)
    {
        var b = Instantiate(listItemButtonPrefab, listContainer);

        var tmp = b.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = label;
        else
        {
            var legacy = b.GetComponentInChildren<Text>();
            if (legacy != null) legacy.text = label;
        }

        b.onClick.AddListener(() => OnItemPressed(index));
    }

    void OnItemPressed(int index)
    {
        var m = BgmManagerV2.I;
        if (m == null) return;

        if (index == INDEX_RANDOM)
        {
            // ★ミュート解除を先に入れる
            m.SetMute(false);
            m.SetRandom(true);
            m.PlayRandom();
        }
        else if (index == INDEX_MUTE)
        {
            // ミュート専用エントリ
            m.SetRandom(false);
            m.SetMute(true);
            // 必要なら m.Stop(); してもOK
        }
        else
        {
            // ★ミュート解除を先に入れる
            m.SetMute(false);
            m.SetRandom(false);
            m.Play(index);
        }

        SyncFromManager();
        CloseList();
    }

    void SyncFromManager()
    {
        var m = BgmManagerV2.I;
        if (m == null) return;

        string text;

        if (m.GetIsMuted())
        {
            text = "ミュート";
        }
        else if (m.GetIsRandom())
        {
            text = "♫ ランダム ♫";
        }
        else
        {
            string title = m.GetTrackTitle(m.GetCurrentIndex());
            text = "♫ " + title + " ♫";
        }

        if (currentTitleTextTMP != null) currentTitleTextTMP.text = text;
        if (currentTitleTextLegacy != null) currentTitleTextLegacy.text = text;
    }

    public void ToggleListPanel()
    {
        if (listPanelRoot == null) return;
        listPanelRoot.SetActive(!listPanelRoot.activeSelf);
    }

    public void CloseList()
    {
        if (listPanelRoot == null) return;
        listPanelRoot.SetActive(false);
    }

    void OnEnable()
    {
        if (BgmManagerV2.I != null)
            BgmManagerV2.I.OnTrackChanged += HandleTrackChanged;
    }

    void OnDisable()
    {
        if (BgmManagerV2.I != null)
            BgmManagerV2.I.OnTrackChanged -= HandleTrackChanged;
    }

    void HandleTrackChanged(int index, string title)
    {
        // 切替フレームから外す（1フレーム遅延）
        StopAllCoroutines();
        StartCoroutine(RefreshNextFrame(index, title));
    }

    System.Collections.IEnumerator RefreshNextFrame(int index, string title)
    {
        yield return null; // 1フレーム待つ：レイアウト再計算を次フレームへ
                           // ここでUI反映（例）
        if (currentTitleTextTMP != null) currentTitleTextTMP.text = "♫ " + title + " ♫";
        if (currentTitleTextLegacy != null) currentTitleTextLegacy.text = "♫ " + title + " ♫";
        // トグルやスライダー等の更新もこの中で
    }
}
