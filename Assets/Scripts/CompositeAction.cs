using UnityEngine;

public class CompositeAction : IUndoable
{
    readonly IUndoable[] actions;

    public CompositeAction(IUndoable[] actions)
    {
        this.actions = actions;
    }

    public void Undo()
    {
        Log.D($"[CompositeAction] Undo(): {actions.Length} sub-actions");

        // 中身は逆順でUndo
        for (int i = actions.Length - 1; i >= 0; i--)
        {
            actions[i].Undo();
        }

        // ★ 重要：Undoしたらコンボは必ず途切れる
        ScoreManager.Instance?.BreakCombo();

        ScoreManager.Instance?.ClearLastManualMove(); // ★追加：Undoしたら“直前の手”を消す
    }
}
