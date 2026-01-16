using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Deck
{
    // ===== TUNING =====
    // A/2/3 を「非常に低い」扱いにする（要望に合わせて <=3）
    static bool IsVeryLow(CardData c) => c.rank <= 2;

    // A/2/3 を重要扱い（裏に埋めたくない/序盤に触れたい）
    private static bool IsCritical(CardData c) => c.rank >= 1 && c.rank <= 3;

    // 先頭28中、各列の「最下層（底）」に VeryLow が何枚あるか
    // ※列0(0枚伏せ)をどう扱うかは excludeCol0 で切替
    static int CountVeryLowAtColumnBottoms(List<CardData> deck, bool excludeCol0 = false)
    {
        int cnt = 0;

        // 深い伏せの底（列1..6の一番深い伏せ）だけチェックする
        // BuriedIndices = {1,3,6,10,15,21}
        foreach (var idx in BuriedIndices)
        {
            if (idx >= 0 && idx < deck.Count && IsVeryLow(deck[idx]))
                cnt++;
        }

        return cnt;
    }


    public static List<CardData> CreateNew()
    {
        var cards = new List<CardData>();
        foreach (Suit s in System.Enum.GetValues(typeof(Suit)))
        {
            for (int r = 1; r <= 13; r++)
            {
                cards.Add(new CardData(s, r));
            }
        }
        return cards;
    }

    public static void Shuffle<T>(this IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int rnd = UnityEngine.Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[rnd];
            list[rnd] = tmp;
        }
    }

    public static void Shuffle<T>(this IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int rnd = rng.Next(i + 1);
            (list[i], list[rnd]) = (list[rnd], list[i]);
        }
    }

    public static void ShuffleWithEasyMode(this IList<CardData> list)
    {
        // 通常シャッフル
        list.Shuffle();

        // 重要カードを奥に埋めない処理（※この関数は A/2 を対象のまま）
        AvoidBuryingKeyCards(list);

        // 同スート連続率を低くする
        ReduceSameSuitRuns(list);
    }

    private static void AvoidBuryingKeyCards(IList<CardData> list)
    {
        for (int i = 0; i < 7; i++)
        {
            // 表7枚に A/2 が来たら奥に逃がす（ここは好みで <=3 にしてもOK）
            if (list[i].rank == 1 || list[i].rank == 2)
            {
                int swapIndex = UnityEngine.Random.Range(10, list.Count);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
        }
    }

    private static void ReduceSameSuitRuns(IList<CardData> list)
    {
        for (int i = 1; i < list.Count; i++)
        {
            if (list[i].suit == list[i - 1].suit && UnityEngine.Random.value < 0.5f)
            {
                int swapIndex = UnityEngine.Random.Range(0, list.Count);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
        }
    }

    // タブロー配りで「深い伏せ」になるデッキ内インデックス（0始まり）
    // 列0..6で、深い伏せがあるのは列1..6 → {1,3,6,10,15,21}
    private static readonly int[] BuriedIndices = { 1, 3, 6, 10, 15, 21 };

    private static bool IsImportant(CardData c) => (c.rank == 1 || c.rank == 2); // 旧互換（A/2）

    // 先頭limit枚での「同スート連続ペア数」を数える
    private static int CountSameSuitAdjPairs(List<CardData> deck, int limit)
    {
        int n = Math.Min(limit, deck.Count);
        int cnt = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (deck[i].suit == deck[i - 1].suit) cnt++;
        }
        return cnt;
    }

    // 深い伏せ位置にA/2が来ていたら、後方（できればストック側）とスワップして回避
    private static void FixBuriedImportantCards(List<CardData> deck)
    {
        foreach (var idx in BuriedIndices)
        {
            if (idx >= 0 && idx < deck.Count && IsImportant(deck[idx]))
            {
                int swapIndex = -1;

                // 1) 28枚目以降（ストック候補）から探す
                for (int j = Math.Max(28, idx + 1); j < deck.Count; j++)
                {
                    if (!IsImportant(deck[j]))
                    {
                        swapIndex = j;
                        break;
                    }
                }
                // 2) 見つからなければ全体から探す
                if (swapIndex == -1)
                {
                    for (int j = deck.Count - 1; j > idx; j--)
                    {
                        if (!IsImportant(deck[j]))
                        {
                            swapIndex = j;
                            break;
                        }
                    }
                }
                if (swapIndex != -1)
                {
                    (deck[idx], deck[swapIndex]) = (deck[swapIndex], deck[idx]);
                }
            }
        }
    }

    static bool HasKingInFaceUpStarters(List<CardData> deck)
    {
        // スタート表7枚のインデックス: idx = T(c) + c
        for (int c = 0; c <= 6; c++)
        {
            int idx = (c * (c + 1) / 2) + c;
            if (idx < deck.Count && deck[idx].rank == 13) return true;
        }
        return false;
    }

    // ★追加：先頭28（タブロー）で「同スート縦3連以上」を検出する
    // 同じ列の中で upper.suit == lower.suit が2回連続したら「縦3連」(例: ♠7-♠6-♠5)
    static bool HasTripleSameSuitRunInFirst28(List<CardData> deck)
    {
        int index = 0;

        for (int col = 0; col <= 6; col++)
        {
            int height = col + 1;
            int runPairs = 0;

            for (int row = 0; row < height - 1; row++)
            {
                var upper = deck[index + row];
                var lower = deck[index + row + 1];

                if (upper.suit == lower.suit)
                {
                    runPairs++;
                    if (runPairs >= 2) return true; // 2ペア連続＝3枚同スート
                }
                else
                {
                    runPairs = 0;
                }
            }

            index += height;
        }

        return false;
    }

    public static List<CardData> CreateEasedNew(
        int maxSameSuitPairsInFirst28 = 6,
        int maxSameSuitPairsInWholeDeck = 8,
        int maxBuriedCriticalInFaceDown = 2,   // A/2/3 の深埋めは原則ゼロに
        bool requireAtLeastOneCriticalFaceUp = false,
        int maxTries = 20000
    )
    {
        var baseDeck = CreateNew();

        for (int t = 0; t < maxTries; t++)
        {
            var deck = new List<CardData>(baseDeck);
            deck.Shuffle();

            // ① 先頭28の同スート連続（さらに甘く）
            int p28 = CountSameSuitPairsInPrefix(deck, 28);
            if (p28 > maxSameSuitPairsInFirst28) continue;

            if (HasTripleSameSuitRunInFirst28(deck) && UnityEngine.Random.value < 0.8f)
                continue; // 80%は弾く、20%は許す

            // ② 全体52の同スート連続
            int p52 = CountSameSuitPairsInPrefix(deck, 52);
            if (p52 > maxSameSuitPairsInWholeDeck) continue;

            // ③ 先頭28の“裏”に埋まる重要カード（A/2/3）数
            int buried = CountBuriedCriticalInFirst28(deck);
            if (buried > maxBuriedCriticalInFaceDown) continue;

            // ④ 表7枚に最低1枚の重要カード
            if (requireAtLeastOneCriticalFaceUp && !HasCriticalInFaceUpStarters(deck)) continue;

            // ⑤ 列の底（最下層の伏せ位置）に A/2/3 を置かない（完全禁止）
            // ここが今回の要望の「本体」：A/2/3 対象＆列0も含めるなら excludeCol0=false
            int bottoms = CountVeryLowAtColumnBottoms(deck, excludeCol0: false);

            // 深い伏せ底にA/2が来るのはキツいので「だいたい弾く」(70%)、たまに許す(30%)
            if (bottoms > 0 && UnityEngine.Random.value < 0.7f)
                continue;


            // ⑥ 低ランク（A/2/3）の“初期で触れる枚数”を底上げ
            if (!EnoughCriticalExposed(deck)) continue;

            // ⑦ 表7枚のKが多すぎると渋滞する → 2枚まで
            if (CountKingOnTopInStarters(deck) > 2) continue;

            // ⑧ “開始直後に動ける手”の2本保証（異なる列から）
            if (!HasAtLeastTwoStarterMoves(deck)) continue;

            // 似た配列対応
            var sig = DealDiversity.BuildSignature(deck);
            if (DealDiversity.ViolatesHistory(sig)) continue;
            DealDiversity.PushHistory(sig);

            return deck;
        }

        Log.W("[EASE DEAL] 条件を満たす配りが見つからず、通常シャッフルを返します。条件が厳しすぎる可能性あり。");
        var fallback = CreateNew();
        fallback.Shuffle();
        return fallback;
    }

    private static int CountSameSuitPairsInPrefix(List<CardData> deck, int prefixCount)
    {
        int n = Mathf.Min(prefixCount, deck.Count);
        int cnt = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (deck[i].suit == deck[i + 1].suit) cnt++;
        }
        return cnt;
    }

    public static void DebugDeckStats(List<CardData> deck)
    {
        int totalPairs = CountSameSuitPairsInPrefix(deck, 52);
        int first28Pairs = CountSameSuitPairsInPrefix(deck, 28);

        Log.D($"[DEBUG] 同スート連続回数 (先頭28): {first28Pairs}, (全体52): {totalPairs}");

        System.Text.StringBuilder sb = new System.Text.StringBuilder("先頭28スート: ");
        for (int i = 0; i < Mathf.Min(28, deck.Count); i++)
        {
            sb.Append(deck[i].suit.ToString()[0]);
            if (i < 27) sb.Append("-");
        }
        Log.D(sb.ToString());
    }

    // スタート時に「すぐ動かせる組み合わせ」があるか判定
    static bool HasAnyImmediateStarterMove(List<CardData> deck)
    {
        var starters = new List<CardData>(7);
        for (int c = 0; c <= 6; c++)
        {
            int idx = (c * (c + 1) / 2) + c;
            if (idx < deck.Count) starters.Add(deck[idx]);
        }

        for (int i = 0; i < starters.Count; i++)
            for (int j = 0; j < starters.Count; j++)
            {
                if (i == j) continue;
                var a = starters[i];
                var b = starters[j];
                bool isOppositeColor =
                    (a.suit == Suit.Hearts || a.suit == Suit.Diamonds)
                    != (b.suit == Suit.Hearts || b.suit == Suit.Diamonds);
                if (isOppositeColor && a.rank + 1 == b.rank)
                    return true;
            }
        return false;
    }

    // 先頭28枚のうち「裏向き(true) / 表(false)」になる位置を、
    // Klondike の配り順に沿って構築する
    private static bool[] BuildKlondikeFaceDownMask()
    {
        bool[] mask = new bool[28];
        int idx = 0;

        for (int r = 0; r <= 6; r++)
        {
            for (int col = r; col <= 6; col++)
            {
                bool isFaceUp = (col == r);
                mask[idx++] = !isFaceUp;
            }
        }

        if (idx != 28)
        {
            Log.W($"[DealMask] unexpected built length={idx}");
        }
        return mask;
    }

    // 先頭28で「裏向きに配られる位置」にある重要カードの個数を数える
    private static int CountBuriedCriticalInFirst28(List<CardData> deck)
    {
        bool[] down = BuildKlondikeFaceDownMask();
        int n = Mathf.Min(deck.Count, 28);
        int buried = 0;
        for (int i = 0; i < n; i++)
        {
            if (down[i] && IsCritical(deck[i])) buried++;
        }
        return buried;
    }

    // 先頭28の「表で配られる7枚」に重要カードが含まれるか
    private static bool HasCriticalInFaceUpStarters(List<CardData> deck)
    {
        bool[] down = BuildKlondikeFaceDownMask();
        int n = Mathf.Min(deck.Count, 28);
        for (int i = 0; i < n; i++)
        {
            if (!down[i] && IsCritical(deck[i]))
                return true;
        }
        return false;
    }

    // 即時ムーブを2本以上（異なる列）保証
    static bool HasAtLeastTwoStarterMoves(List<CardData> deck)
    {
        var movesFromCol = new HashSet<int>();
        for (int from = 0; from <= 6; from++)
        {
            var topFrom = GetStarterTop(deck, from);
            if (topFrom == null) continue;
            for (int to = 0; to <= 6; to++) if (to != from)
                {
                    var topTo = GetStarterTop(deck, to);
                    if (CanStack(topFrom.Value, topTo)) { movesFromCol.Add(from); break; }
                }
        }
        return movesFromCol.Count >= 2;
    }

    static (int rank, Suit suit)? GetStarterTop(List<CardData> deck, int col)
    {
        int idx = (col * (col + 1) / 2) + col;
        if (idx >= deck.Count) return null;
        var c = deck[idx];
        return (c.rank, c.suit);
    }

    // 交互色＆ランク−1
    static bool CanStack((int rank, Suit suit) a, (int rank, Suit suit)? bMaybe)
    {
        if (a.rank == 13) return true;
        if (bMaybe == null) return false;
        var b = bMaybe.Value;
        bool opposite = ((a.suit == Suit.Clubs || a.suit == Suit.Spades) ^ (b.suit == Suit.Clubs || b.suit == Suit.Spades));
        return opposite && (a.rank == b.rank - 1);
    }

    // A/2/3 が “表7枚＋山札トップ12枚” に合計2枚以上あるか
    static bool EnoughCriticalExposed(List<CardData> deck)
    {
        int cnt = 0;

        // 表7枚
        for (int c = 0; c <= 6; c++)
        {
            int idx = (c * (c + 1) / 2) + c;
            if (idx < deck.Count && deck[idx].rank <= 3) cnt++;
        }

        // 山札トップ12（28枚以降の先頭12）
        for (int i = 28; i < Math.Min(deck.Count, 28 + 12); i++)
            if (deck[i].rank <= 3) cnt++;

        return cnt >= 2;
    }

    // 表7枚（各列のトップ）にある K の枚数
    static int CountKingOnTopInStarters(List<CardData> deck)
    {
        int cnt = 0;
        for (int c = 0; c <= 6; c++)
        {
            int idx = (c * (c + 1) / 2) + c;
            if (idx < deck.Count && deck[idx].rank == 13) cnt++;
        }
        return cnt;
    }

    // ====== Diversity Guard (deal similarity) ======
    static class DealDiversity
    {
        const int MaxHistory = 10;

        public static string BuildSignature(List<CardData> deck)
        {
            var tops = new System.Text.StringBuilder(7 * 3);
            for (int c = 0; c <= 6; c++)
            {
                int idx = (c * (c + 1) / 2) + c;
                if (idx >= deck.Count) break;
                var cd = deck[idx];
                char col = (cd.suit == Suit.Clubs || cd.suit == Suit.Spades) ? 'B' : 'R';
                tops.Append(RankToShort(cd.rank)).Append(col).Append(',');
            }

            var suits28 = new System.Text.StringBuilder(28 * 2);
            int n = Mathf.Min(28, deck.Count);
            for (int i = 0; i < n; i++)
            {
                suits28.Append(deck[i].suit.ToString()[0]);
                if (i < n - 1) suits28.Append('-');
            }

            return $"T7:{tops}|S28:{suits28}";
        }

        public static bool ViolatesHistory(string sig)
        {
            foreach (var old in history)
                if (IsTooSimilar(sig, old)) return true;
            return false;
        }

        public static void PushHistory(string sig)
        {
            history.Add(sig);
            while (history.Count > MaxHistory) history.RemoveAt(0);
        }

        static bool IsTooSimilar(string a, string b)
        {
            Parse(a, out var aTops, out var aSuits);
            Parse(b, out var bTops, out var bSuits);

            int sameTop = 0;
            int topCount = Math.Min(aTops.Count, bTops.Count);
            for (int i = 0; i < topCount; i++) if (aTops[i] == bTops[i]) sameTop++;

            int suitSame = 0;
            int suitCount = Math.Min(aSuits.Count, bSuits.Count);
            for (int i = 0; i < suitCount; i++) if (aSuits[i] == bSuits[i]) suitSame++;

            bool topsTooClose = (topCount >= 7) && (sameTop >= 5);
            bool suitsTooClose = (suitCount >= 20) && (suitSame >= 16);

            return topsTooClose && suitsTooClose;
        }

        static void Parse(string sig, out List<string> tops, out List<char> suits)
        {
            tops = new List<string>(7);
            suits = new List<char>(28);

            var parts = sig.Split('|');
            foreach (var p in parts)
            {
                if (p.StartsWith("T7:"))
                {
                    var t = p.Substring(3).TrimEnd(',');
                    foreach (var token in t.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        tops.Add(token);
                }
                else if (p.StartsWith("S28:"))
                {
                    var s = p.Substring(4);
                    foreach (var token in s.Split('-', StringSplitOptions.RemoveEmptyEntries))
                        if (!string.IsNullOrEmpty(token)) suits.Add(token[0]);
                }
            }
        }

        static string RankToShort(int r) => r switch { 1 => "A", 11 => "J", 12 => "Q", 13 => "K", _ => r.ToString() };

        static readonly List<string> history = new List<string>();
    }
}
