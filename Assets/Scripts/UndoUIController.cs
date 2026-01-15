// UndoUIController.cs
using UnityEngine;
using UnityEngine.UI;

public class UndoUIController : MonoBehaviour
{
    public Button undoButton;

    void Awake()
    {
        if (undoButton == null) Debug.LogError("UndoButton ‚ªƒAƒTƒCƒ“‚³‚ê‚Ä‚¢‚Ü‚¹‚ñ");
        undoButton.onClick.AddListener(() => UndoManager.Instance.Undo());
    }
}
