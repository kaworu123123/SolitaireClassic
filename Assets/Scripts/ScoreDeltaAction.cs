public class ScoreDeltaAction : IUndoable
{
    private readonly int delta; // 例: 15（Flipの分）や 20（任意）
    public ScoreDeltaAction(int delta) { this.delta = delta; }
    public void Undo()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(-delta);
    }
}
