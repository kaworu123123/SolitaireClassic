using UnityEngine;
using UnityEngine.UI;

public class VictoryPanelActivator : MonoBehaviour
{
    [Header("Victory Panel")]
    public CanvasGroup victoryPanelCanvasGroup;
    public GameObject confettiObject;

    [Header("Blocker（全画面のクリック遮断）")]
    public GameObject popupBlockerPanel; // ← PopupBlockerPanel を割り当て

    [Header("HUD全体（Pause/Undoを含む親）")]
    public CanvasGroup hudCanvasGroup;   // ← UIRoot などHUDの親に CanvasGroup を付けて割当て

    [Header("個別ボタン（任意：誤配線直す）")]
    public Button pauseButton;           // 本物の Pause を割当て（Restartではない）
    public Button undoButton;            // 本物の Undo を割当て

    public void OnVictoryPanelShown()
    {
        // 1) Victory 自身を前面でクリック受付に
        if (victoryPanelCanvasGroup)
        {
            victoryPanelCanvasGroup.alpha = 1f;
            victoryPanelCanvasGroup.interactable = true;
            victoryPanelCanvasGroup.blocksRaycasts = true;
        }

        // 2) 全画面ブロッカーON（これで確実に下に通らない）
        if (popupBlockerPanel) popupBlockerPanel.SetActive(true);

        // 3) HUD自体も念のため無効化
        if (hudCanvasGroup) hudCanvasGroup.interactable = false;

        if (pauseButton) pauseButton.interactable = false;
        if (undoButton) undoButton.interactable = false;

        GameState.VictoryOpen = true; // ロジック側ブロック用
    }

    public void OnVictoryPanelHidden()
    {
        if (popupBlockerPanel) popupBlockerPanel.SetActive(false);

        if (hudCanvasGroup)
        {
            hudCanvasGroup.interactable = true;
            hudCanvasGroup.blocksRaycasts = true;
        }

        if (pauseButton) pauseButton.interactable = true;
        if (undoButton) undoButton.interactable = true;

        GameState.VictoryOpen = false;
    }

    public void PlayConfetti()
    {
        if (!confettiObject) return;
        confettiObject.SetActive(true);
        var ps = confettiObject.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }
    }
}
