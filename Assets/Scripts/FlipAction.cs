using UnityEngine;

// カードを表向き→裏向きに戻すアクション
public class FlipAction : IUndoable
{
    readonly CardBehaviour card;
    readonly bool prevFaceUp;
    readonly int prevSortingOrder;

    public FlipAction(CardBehaviour card)
    {
        this.card = card;
        // 巻き戻す前の状態をキャプチャ
        this.prevFaceUp = card.Data.isFaceUp;
        var sr = card.GetComponent<SpriteRenderer>();
        this.prevSortingOrder = sr != null ? sr.sortingOrder : 0;
    }

    public void Undo()
    {
        //card.SetFaceUp(false);

        // 向きと描画順を元に戻す
        card.Data.isFaceUp = prevFaceUp;
        card.UpdateVisual();
        var sr = card.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = prevSortingOrder;
        
        // レイアウトも正しく再配置
        var parent = card.transform.parent;
        if (parent.CompareTag("TableauColumn"))
            CardBehaviour.RefreshTableauColumn(parent);
        else if (parent.CompareTag("FoundationSlot"))
            CardBehaviour.RefreshFoundationSlot(parent);
    }
}
