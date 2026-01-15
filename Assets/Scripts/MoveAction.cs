using UnityEngine;
using DG.Tweening;

public class MoveAction : IUndoable
{
    readonly CardBehaviour card;
    readonly Transform oldParent;
    readonly Transform newParent;
    readonly Vector3 oldLocalPos;
    readonly Vector3 oldLocalScale;
    readonly int oldSortingOrder;
    readonly int oldSiblingIndex;
    readonly int scoreDelta;
    readonly bool countedAsMove;

    // ★ 追加：コンボ状態を戻すための情報
    readonly bool restoreCombo;
    readonly ScoreManager.ComboSnapshot comboSnapshot;

    // 既存呼び出し互換のため、追加引数はデフォルト付き
    public MoveAction(
        CardBehaviour card,
        Transform oldParent,
        Transform newParent,
        Vector3 oldLocalPos,
        Vector3 oldLocalScale,
        int oldSortingOrder,
        int oldSiblingIndex,
        int scoreDelta,
        bool countedAsMove,
        bool restoreCombo = false,
        ScoreManager.ComboSnapshot comboSnapshot = default
    )
    {
        this.card = card;
        this.oldParent = oldParent;
        this.newParent = newParent;
        this.oldLocalPos = oldLocalPos;
        this.oldLocalScale = oldLocalScale;
        this.oldSortingOrder = oldSortingOrder;
        this.oldSiblingIndex = oldSiblingIndex;
        this.scoreDelta = scoreDelta;
        this.countedAsMove = countedAsMove;

        this.restoreCombo = restoreCombo;
        this.comboSnapshot = comboSnapshot;
    }

    public void Undo()
    {
        // Tween停止
        card.transform.DOKill();

        // 位置／親を復元
        card.transform.SetParent(oldParent, true);
        Vector3 worldPos = oldParent.TransformPoint(oldLocalPos);
        card.transform.position = worldPos;
        card.transform.localScale = oldLocalScale;
        card.transform.SetSiblingIndex(oldSiblingIndex);
        var sr = card.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = oldSortingOrder;

        // ★ 先にコンボ状態を復元
        if (restoreCombo && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RestoreCombo(comboSnapshot);
        }

        // スコア／ムーブの巻き戻し（既存）
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(-scoreDelta);
            ScoreManager.Instance.BreakCombo();          // ★ 追加：Undoしたらコンボは必ず途切れる
        if (countedAsMove)
        {
            var stm = Object.FindObjectOfType<ScoreTimerManager>();
            if (stm != null) stm.ReduceMove();
        }

        // 列の見た目更新（既存）
        if (oldParent.CompareTag("TableauColumn"))
            CardBehaviour.RefreshTableauColumn(oldParent);
        else if (oldParent.CompareTag("FoundationSlot"))
            CardBehaviour.RefreshFoundationSlot(oldParent);
        else if (oldParent.CompareTag("WasteSlot"))
            CardBehaviour.RefreshWasteSlot(oldParent);

        if (newParent.CompareTag("TableauColumn"))
            CardBehaviour.RefreshTableauColumn(newParent);
        else if (newParent.CompareTag("FoundationSlot"))
            CardBehaviour.RefreshFoundationSlot(newParent);
        else if (newParent.CompareTag("WasteSlot"))
            CardBehaviour.RefreshWasteSlot(newParent);
    }
}
