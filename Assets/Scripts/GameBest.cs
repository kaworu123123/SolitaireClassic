using UnityEngine;
using System;

namespace Solitaire.Stats
{
    /// <summary>
    /// 累計ベスト／今日ベストの保存・更新ユーティリティ
    /// ・Score は「大きいほど良い」
    /// ・Moves と Time は「小さいほど良い」
    /// ・Today は日付が変わったら自動リセット
    /// </summary>
    public static class GameBest
    {
        // ===== PlayerPrefs Keys =====
        private const string K_AllScore = "AllBestScore";
        private const string K_AllMoves = "AllBestMoves";
        private const string K_AllTime = "AllBestTime";

        private const string TK_Score = "TodayBestScore";
        private const string TK_Moves = "TodayBestMoves";
        private const string TK_Time = "TodayBestTime";
        private const string TK_Date = "TodayDate";     // "yyyyMMdd"

        // ===== Data Structs =====
        public struct Snapshot
        {
            public int Score;
            public int Moves;
            public float TimeSec;
        }

        public struct UpdateFlags
        {
            public bool NewAllTimeScore;
            public bool NewAllTimeMoves;
            public bool NewAllTimeTime;

            public bool NewTodayScore;
            public bool NewTodayMoves;
            public bool NewTodayTime;

            public bool Any =>
                NewAllTimeScore || NewAllTimeMoves || NewAllTimeTime ||
                NewTodayScore || NewTodayMoves || NewTodayTime;
        }

        // ===== Public API =====

        /// <summary>
        /// 0 や不正値が保存されていたら削除してクリーンにする（古いバージョン互換）
        /// </summary>
        public static void SanitizeIfNeeded()
        {
            // 累計
            if (PlayerPrefs.GetInt(K_AllMoves, int.MaxValue) <= 0) PlayerPrefs.DeleteKey(K_AllMoves);
            if (PlayerPrefs.GetFloat(K_AllTime, float.MaxValue) <= 0f) PlayerPrefs.DeleteKey(K_AllTime);

            // 今日
            EnsureTodayFreshness(); // ← ここで日付も確認しつつ、Todayの0も消す
            if (PlayerPrefs.GetInt(TK_Moves, int.MaxValue) <= 0) PlayerPrefs.DeleteKey(TK_Moves);
            if (PlayerPrefs.GetFloat(TK_Time, float.MaxValue) <= 0f) PlayerPrefs.DeleteKey(TK_Time);
        }

        /// <summary>
        /// 累計ベスト（存在しない場合は null）
        /// </summary>
        public static Snapshot? GetAllTime()
        {
            bool hasScore = PlayerPrefs.HasKey(K_AllScore);
            bool hasMoves = PlayerPrefs.HasKey(K_AllMoves);
            bool hasTime = PlayerPrefs.HasKey(K_AllTime);

            if (!hasScore && !hasMoves && !hasTime) return null;

            Snapshot s = new Snapshot
            {
                Score = hasScore ? PlayerPrefs.GetInt(K_AllScore) : 0,
                Moves = hasMoves ? PlayerPrefs.GetInt(K_AllMoves) : int.MaxValue,
                TimeSec = hasTime ? PlayerPrefs.GetFloat(K_AllTime) : float.MaxValue
            };
            return s;
        }

        /// <summary>
        /// 今日ベスト（無ければ null）
        /// ※ 呼び出し時に日付ズレがあれば自動リセット
        /// </summary>
        public static Snapshot? GetToday()
        {
            EnsureTodayFreshness();

            bool hasScore = PlayerPrefs.HasKey(TK_Score);
            bool hasMoves = PlayerPrefs.HasKey(TK_Moves);
            bool hasTime = PlayerPrefs.HasKey(TK_Time);

            if (!hasScore && !hasMoves && !hasTime) return null;

            Snapshot s = new Snapshot
            {
                Score = hasScore ? PlayerPrefs.GetInt(TK_Score) : 0,
                Moves = hasMoves ? PlayerPrefs.GetInt(TK_Moves) : int.MaxValue,
                TimeSec = hasTime ? PlayerPrefs.GetFloat(TK_Time) : float.MaxValue
            };
            return s;
        }

        /// <summary>
        /// 「今回の結果」を渡して、累計／今日ベストを更新。更新フラグを返す。
        /// ※ 呼び出し時に日付ズレがあれば今日ベストを自動リセット。
        /// </summary>
        public static UpdateFlags UpdateAllAndToday(int score, int moves, float timeSec)
        {
            EnsureTodayFreshness();

            var flags = new UpdateFlags();

            // ===== 累計 =====
            // Score: 大きいほど良い。未設定(=キー無し)なら無条件更新
            if (!PlayerPrefs.HasKey(K_AllScore) || score > PlayerPrefs.GetInt(K_AllScore))
            {
                PlayerPrefs.SetInt(K_AllScore, score);
                flags.NewAllTimeScore = true;
            }
            // Moves: 小さいほど良い。未設定なら無条件更新
            if (!PlayerPrefs.HasKey(K_AllMoves) || moves < PlayerPrefs.GetInt(K_AllMoves))
            {
                PlayerPrefs.SetInt(K_AllMoves, moves);
                flags.NewAllTimeMoves = true;
            }
            // Time: 小さいほど良い。0や負は不正として無視。未設定なら無条件更新
            if (timeSec > 0f && (!PlayerPrefs.HasKey(K_AllTime) || timeSec < PlayerPrefs.GetFloat(K_AllTime)))
            {
                PlayerPrefs.SetFloat(K_AllTime, timeSec);
                flags.NewAllTimeTime = true;
            }

            // ===== 今日 =====
            // Score: 大きいほど良い。未設定なら無条件更新
            if (!PlayerPrefs.HasKey(TK_Score) || score > PlayerPrefs.GetInt(TK_Score))
            {
                PlayerPrefs.SetInt(TK_Score, score);
                flags.NewTodayScore = true;
            }
            // Moves: 小さいほど良い。未設定なら無条件更新
            if (!PlayerPrefs.HasKey(TK_Moves) || moves < PlayerPrefs.GetInt(TK_Moves))
            {
                PlayerPrefs.SetInt(TK_Moves, moves);
                flags.NewTodayMoves = true;
            }
            // Time: 小さいほど良い。0や負は不正として無視。未設定なら無条件更新
            if (timeSec > 0f && (!PlayerPrefs.HasKey(TK_Time) || timeSec < PlayerPrefs.GetFloat(TK_Time)))
            {
                PlayerPrefs.SetFloat(TK_Time, timeSec);
                flags.NewTodayTime = true;
            }

            // 今日の日付を刻んでおく（初回/更新時とも）
            PlayerPrefs.SetString(TK_Date, NowYmd());

            PlayerPrefs.Save();
            return flags;
        }

        // ===== Helpers =====

        /// <summary>
        /// 時刻→ "yyyyMMdd"（端末ローカルタイム）
        /// </summary>
        private static string NowYmd()
        {
            // 端末ローカルの暦で十分（Androidは端末タイムゾーン）
            return DateTime.Now.ToString("yyyyMMdd");
        }

        /// <summary>
        /// 保存されている Today の日付が今日と違えば Today の記録をリセット
        /// </summary>
        public static void EnsureTodayFreshness()
        {
            var today = NowYmd();
            var stored = PlayerPrefs.GetString(TK_Date, string.Empty);

            if (stored == today) return; // 同日 → 何もしない

            // 日付が無い or 変わっている → Today をリセット
            PlayerPrefs.DeleteKey(TK_Score);
            PlayerPrefs.DeleteKey(TK_Moves);
            PlayerPrefs.DeleteKey(TK_Time);

            PlayerPrefs.SetString(TK_Date, today);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// タイム表記 "MM:SS"
        /// </summary>
        public static string FormatTime(float sec)
        {
            if (sec <= 0f) return "00:00";
            int s = Mathf.RoundToInt(sec);
            int m = s / 60;
            int ss = s % 60;
            return $"{m:00}:{ss:00}";
        }

        // （任意）デバッグ用：Today手動リセット
        public static void ResetTodayForDebug()
        {
            PlayerPrefs.DeleteKey(TK_Score);
            PlayerPrefs.DeleteKey(TK_Moves);
            PlayerPrefs.DeleteKey(TK_Time);
            PlayerPrefs.SetString(TK_Date, NowYmd());
            PlayerPrefs.Save();
        }
    }
}
