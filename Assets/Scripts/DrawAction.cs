// Assets/Scripts/DrawAction.cs
using UnityEngine;

public class DrawAction : IUndoable
{
    private readonly CardFactory factory;
    private readonly CardData data;
    private readonly GameObject go;
    private readonly int scoreDelta;
    private readonly bool countedAsMove;

    // ← ここだけ残す。既定値付き引数で一つにまとめます
    public DrawAction(CardFactory factory, CardData data, GameObject go, int scoreDelta = 0, bool countedAsMove = true)
    {
        this.factory = factory;
        this.data = data;
        this.go = go;
        this.scoreDelta = scoreDelta;
        this.countedAsMove = countedAsMove;
    }

    public void Undo()
    {
        // Waste から山札へ戻す
        factory.UndoDraw(go, data);

        // スコアを巻き戻す
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(-scoreDelta);

        // ムーブ数を巻き戻す
        if (countedAsMove)
        {
            var stm = Object.FindObjectOfType<ScoreTimerManager>();
            if (stm != null)
                stm.ReduceMove();
        }
    }
}
