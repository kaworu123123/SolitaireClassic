using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UndoManager : MonoBehaviour
{
    public static UndoManager Instance { get; private set; }
    private Stack<IUndoable> stack;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            stack = new Stack<IUndoable>();
            Log.D($"[UndoManager:{name}] Awake(): Instance 初期化");
        }
        else
        {
            Log.D($"[UndoManager:{name}] Awake(): Duplicate! 破棄します");
            Destroy(gameObject);
        }
    }

    public void Record(IUndoable action)
    {
        Log.D($"[UndoManager:{name}] Record() on instance {GetInstanceID()}, pushing action, new stack size={stack.Count + 1}");
        stack.Push(action);
    }

    /// <summary>最後の操作を取り消す（スタックが空なら何もしない）</summary>
    public void UndoLast()
    {
        if (stack.Count == 0)
        {
            Log.D($"[UndoManager:{name}] UndoLast(): stack empty, nothing to pop");
            return;
        }

        var action = stack.Pop();
        Log.D($"[UndoManager:{name}] UndoLast(): popped {action.GetType().Name}, new stack size = {stack.Count}");
        action.Undo();
    }

    // ★ OnClick 用ラッパー：Undo → 次フレームで正規整列のみ
    public void Undo()
    {
        if (GameState.IsUiBlocked) return;  // ★ 勝利中/吸い込み中は無効
        Log.D($"[UndoManager:{name}] Undo() on instance {GetInstanceID()}, stack size = {stack.Count}");
        UndoLast();

        // フレーム内は各アクションの復元やTweenと競合することがあるので、
        // 正規整列は“次フレーム”に1度だけまとめて行う
        StartCoroutine(NormalizeNextFrame());
    }

    private IEnumerator NormalizeNextFrame()
    {
        // 1フレ待って、最終状態に対してだけ整列を適用
        yield return null;

        var fac = CardFactory.Instance;
        if (fac != null)
        {
            // Tableau を正規整列（表/裏オフセットと sortingOrder を再構築）
            foreach (var col in fac.columnParents)
                if (col) CardBehaviour.RefreshTableauColumn(col);

            // Foundation も正規整列（ランク順で兄弟順＆sortingOrderを決定）
            foreach (var f in fac.foundationParents)
                if (f) CardBehaviour.RefreshFoundationSlot(f);
        }
    }
}
