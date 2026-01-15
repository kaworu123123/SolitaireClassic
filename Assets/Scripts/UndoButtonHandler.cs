using UnityEngine;

public class UndoButtonHandler : MonoBehaviour
{
    // UI‚ÌUndoƒ{ƒ^ƒ“‚©‚çOnClick‚ÅŒÄ‚Ô
    public void OnClick_Undo()
    {
        if (GameState.IsUiBlocked) return; // Ÿ—˜’†E‹z‚¢‚İ’†‚Í–³Œø
        if (UndoManager.Instance != null)
            UndoManager.Instance.Undo();
    }
}