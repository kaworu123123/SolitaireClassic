# コードレビュー - Solitaire Classic

## 概要
Unity製のソリテア（Klondike）ゲームの包括的なコードレビューです。

## ✅ 良い点

### 1. アーキテクチャ
- **明確な責任分離**: `CardBehaviour`, `CardFactory`, `UndoManager`, `ScoreManager`など、役割が明確
- **アクションパターン**: `IUndoable`インターフェースによる統一されたUndoシステム
- **Composite Pattern**: `CompositeAction`で複数の操作をまとめて管理
- **Singleton パターン**: 主要マネージャーに適切に適用

### 2. コード品質
- 詳細なコメント（日本語）で意図が明確
- エラーハンドリングが適切（try-catch、nullチェック）
- デバッグ用の`Log`クラスが整備されている
- 条件付きコンパイルでデバッグログを制御

### 3. 機能実装
- **InputGate**: 重複操作を防ぐ仕組みが実装されている
- **タイムアウト機能**: ハング防止のための自己回復機能
- **コンボシステム**: 連続アクションに対する報酬システム
- **オートコンプリート**: 自動完了機能が実装されている

## ⚠️ 改善が必要な点

### 1. パフォーマンス関連

#### 1.1 FindObjectOfTypeの多用
**問題**: 多数の`FindObjectOfType`呼び出しが存在
```csharp
// CardBehaviour.cs などで多数出現
var factory = FindObjectOfType<CardFactory>();
var stm = FindObjectOfType<ScoreTimerManager>();
```
**影響**: 実行時のパフォーマンス低下（特にUpdate内）

**推奨**:
- Singletonパターンを活用（既存の`Instance`プロパティを使用）
- キャッシュするか、依存性注入を検討

**例**:
```csharp
// Before
var stm = FindObjectOfType<ScoreTimerManager>();

// After
var stm = ScoreTimerManager.Instance;
```

#### 1.2 GetComponentの頻繁な呼び出し
**問題**: ループ内での`GetComponent`呼び出し
```csharp
// CardBehaviour.cs:892-921
for (int i = 0; i < column.childCount; i++)
{
    var cb = tf.GetComponent<CardBehaviour>();
    // ...
}
```

**推奨**:
- 必要なコンポーネントは事前にキャッシュ
- `TryGetComponent`の使用を検討

#### 1.3 文字列操作の最適化
```csharp
// Deck.cs:424 - 文字列連結が頻繁
var tops = new System.Text.StringBuilder(7 * 3);
```
**状態**: 既に`StringBuilder`を使用しているのは良い

### 2. コード品質・保守性

#### 2.1 マジックナンバー
**問題**: ハードコードされた値が散在
```csharp
// CardBehaviour.cs
private const float TableauSpacing = 0.3f;
public static float FACE_DOWN_OFFSET = 0.18f;
public static float FACE_UP_OFFSET = 0.36f;
private const float FOUNDATION_Y_STEP = 0.00f;
```

**推奨**: 設定クラスやScriptableObjectに集約

#### 2.2 長いメソッド
**問題**: `CardBehaviour.OnEndDrag()` (712行) が非常に長い

**推奨**: 
- メソッドの分割
- 処理を小さい単位のメソッドに分解

#### 2.3 重複コード
**問題**: 類似した処理が複数箇所に存在
- `RefreshTableauColumn`と`RefreshFoundationSlot`の類似ロジック
- `TryAutoMove`と`OnEndDrag`内の移動判定ロジック

**推奨**: 共通メソッドへの抽出

#### 2.4 Nullチェックの一貫性
**問題**: nullチェックのパターンが統一されていない
```csharp
// パターン1
if (factory != null) factory.Do();

// パターン2
factory?.Do();

// パターン3
var f = factory;
if (f == null) return;
```

**推奨**: プロジェクト全体で統一されたパターンを採用

### 3. 設計・アーキテクチャ

#### 3.1 静的フィールドの使用
**問題**: `InputGate`クラスが全て静的メンバー

**検討事項**:
- マルチシーン対応が必要な場合、Singleton化を検討
- 現状は問題ないが、テストしにくい可能性

#### 3.2 グローバル状態
**問題**: `GameState`が静的クラス
```csharp
public static class GameState
{
    public static bool VictoryOpen = false;
    public static bool AutoCollecting = false;
}
```

**推奨**: 
- イベントシステムの導入を検討
- 状態変更の追跡が容易になる

#### 3.3 カプセル化
**問題**: 一部のpublicフィールドが直接アクセスされている
```csharp
// CardBehaviour.cs
public bool allowDrag = false; // public field
```

**推奨**: プロパティに変更
```csharp
public bool AllowDrag { get; set; }
```

### 4. 潜在的なバグ

#### 4.1 タイムアウト処理
```csharp
// InputGate.cs:20
if (Busy && (Time.unscaledTime - _busySince) > TimeoutSeconds)
```
**問題**: `_busySince`が0で初期化されているため、初期状態で誤判定の可能性

**推奨**: 
```csharp
if (Busy && _busySince > 0 && (Time.unscaledTime - _busySince) > TimeoutSeconds)
```

#### 4.2 メモリリークの可能性
**問題**: `CardBehaviour`内の`currentTween`リストがクリアされない可能性
```csharp
// CardBehaviour.cs:40
private readonly List<DG.Tweening.Tween> currentTweens = new();
```

**推奨**: 
- `OnDestroy`で確実にクリア
- 既に一部実装されているが、全パスで確実にクリア

#### 4.3 文字エンコーディング
**問題**: コメント内に文字化けが見られる
```csharp
// GameState.cs:6
public static bool AutoCollecting = false; //zݒɂg
```

**推奨**: ファイルのエンコーディングをUTF-8に統一

### 5. セキュリティ・堅牢性

#### 5.1 例外処理
**状態**: 基本的なtry-catchは実装されている

**改善点**: 
- より具体的な例外処理
- エラー時の復旧処理の強化

#### 5.2 入力検証
**状態**: 基本的な検証は実装されている

**改善点**:
- 範囲チェックの追加（例: rank の1-13範囲チェック）

### 6. ドキュメント

#### 6.1 XML Documentation
**推奨**: 主要なpublicメソッドにXMLドキュメントコメントを追加
```csharp
/// <summary>
/// カードをFoundationに移動できるか判定します
/// </summary>
/// <param name="targetSlot">移動先のFoundationスロット</param>
/// <returns>移動可能な場合true</returns>
public bool CanMoveToFoundation(Transform targetSlot)
```

## 📊 メトリクス

### コードサイズ
- **総スクリプト数**: 76ファイル
- **主要クラス**:
  - `CardBehaviour`: ~1800行（非常に大きい）
  - `CardFactory`: ~1400行（大きい）
  - `Deck`: ~500行
  - `ScoreManager`: ~500行

### 複雑度
- **高い複雑度のメソッド**:
  - `CardBehaviour.OnEndDrag()`: 非常に複雑
  - `CardBehaviour.TryAutoMove()`: 非常に複雑
  - `CardFactory.AutoCompleteCoroutine()`: 複雑

## 🎯 優先度別改善提案

### 高優先度
1. **FindObjectOfTypeの削減**: パフォーマンスへの影響が大きい
2. **CardBehaviour.OnEndDrag()の分割**: 保守性向上
3. **文字エンコーディングの修正**: 可読性向上

### 中優先度
4. **マジックナンバーの集約**: 設定の一元管理
5. **重複コードの抽出**: 保守性向上
6. **Nullチェックパターンの統一**: コード品質向上

### 低優先度
7. **XML Documentationの追加**: 開発効率向上
8. **設計パターンの検討**: 長期的な保守性

## 📝 その他の観察

### 良い実装例
1. **InputGate**: 操作の重複を防ぐ仕組みがよく設計されている
2. **Undoシステム**: Composite Patternを使用した統一的な実装
3. **Deck生成**: `CreateEasedNew`による高度なデッキ生成ロジック

### 技術的な特徴
- DOTweenの活用
- イベント駆動の部分的な実装
- 条件付きコンパイルによるデバッグ制御

## 🔧 具体的な改善例

### 例1: Singletonの活用
```csharp
// Before
var factory = FindObjectOfType<CardFactory>();
var stm = FindObjectOfType<ScoreTimerManager>();

// After
var factory = CardFactory.Instance;
var stm = ScoreTimerManager.Instance;
```

### 例2: メソッド分割
```csharp
// CardBehaviour.OnEndDrag()を分割
private void HandleSuccessfulDrag(Transform target, bool toFound) { ... }
private void HandleFailedDrag() { ... }
private void RecordDragAction(Transform target, int score) { ... }
```

### 例3: 設定の集約
```csharp
[CreateAssetMenu]
public class GameSettings : ScriptableObject
{
    public float tableauSpacing = 0.3f;
    public float faceDownOffset = 0.18f;
    public float faceUpOffset = 0.36f;
    // ...
}
```

## 総評

全体的に**よく構造化されたコードベース**です。主要な機能は適切に実装され、エラーハンドリングも基本的なレベルで実装されています。

主な改善点は：
1. **パフォーマンス最適化**（FindObjectOfTypeの削減）
2. **コードの分割**（長いメソッドのリファクタリング）
3. **設定の集約**（マジックナンバーの削減）

これらを改善することで、保守性とパフォーマンスが大幅に向上するでしょう。


