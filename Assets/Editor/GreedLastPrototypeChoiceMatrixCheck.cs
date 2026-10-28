using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeChoiceMatrixCheck
    {
        [MenuItem("Greed Last/Run Mixed Choice Pair Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Mixed choice checks require Edit mode.");
            int[,] matrix = BuildMatrix();
            Require(CoversEveryPair(matrix), "The authored matrix must cover all nine values of every choice pair.");
            var duplicate = (int[,])matrix.Clone();
            for (int row = 0; row < duplicate.GetLength(0); row++) duplicate[row, 9] = duplicate[row, 0];
            Require(!CoversEveryPair(duplicate), "Coverage validation must reject a duplicated choice column.");
            duplicate = (int[,])matrix.Clone();
            duplicate[0, 0] = 3;
            Require(!CoversEveryPair(duplicate), "Coverage validation must reject out-of-range choices.");
            int clears = 0, starts = 0;
            foreach (int accuracy in new[] { 0, 1, 2 })
            for (int row = 0; row < matrix.GetLength(0); row++)
            {
                VerifyPath(matrix, row, accuracy, ref starts);
                clears++;
            }
            Require(clears == 81 && starts == 162, "Every mixed path must finish and start both allocated uses.");
            Debug.Log("Greed mixed choice pairs passed: 27 rows cover all 45 choice pairs / 405 pair values, "
                + "81 actual normal clears (exact/Good/mixed), 1215 judgements, 648 choices, 162 prepared infinite starts and first traps, "
                + "one save/two uses/automatic clear, third-use rejection and live last-use preservation. "
                + "Memory-only backends and synthetic core clocks; not 162 full infinite runs or native input/device proof.");
        }

        private static int[,] BuildMatrix()
        {
            // Distinct projective directions over F3 give pairwise coverage without 3^10 full paths.
            int[,] directions = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 }, { 1, 1, 0 }, { 1, 2, 0 },
                { 1, 0, 1 }, { 1, 0, 2 }, { 0, 1, 1 }, { 0, 1, 2 }, { 1, 1, 1 } };
            var matrix = new int[27, 10];
            for (int row = 0; row < 27; row++)
            for (int column = 0; column < 10; column++)
                matrix[row, column] = (row / 9 * directions[column, 0]
                    + row / 3 % 3 * directions[column, 1] + row % 3 * directions[column, 2]) % 3;
            return matrix;
        }

        private static bool CoversEveryPair(int[,] matrix)
        {
            for (int row = 0; row < matrix.GetLength(0); row++)
            for (int column = 0; column < matrix.GetLength(1); column++)
                if (matrix[row, column] < 0 || matrix[row, column] >= 3) return false;
            for (int first = 0; first < matrix.GetLength(1); first++)
            for (int second = first + 1; second < matrix.GetLength(1); second++)
            {
                var pairs = new HashSet<int>();
                for (int row = 0; row < matrix.GetLength(0); row++)
                    pairs.Add(matrix[row, first] * 3 + matrix[row, second]);
                if (pairs.Count != 9) return false;
            }
            return true;
        }

        private static void VerifyPath(int[,] matrix, int row, int accuracy, ref int starts)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            core.Enter();
            int choices = 0, judgements = 0;
            GreedLastChoiceKind[] order = { GreedLastChoiceKind.StartGift, GreedLastChoiceKind.Node,
                GreedLastChoiceKind.Gift, GreedLastChoiceKind.Node, GreedLastChoiceKind.Gift,
                GreedLastChoiceKind.Relic, GreedLastChoiceKind.Node, GreedLastChoiceKind.Gift };
            string context = "row=" + row + " accuracy=" + accuracy;
            try
            {
                for (int step = 0; !run.Stopped && step < 100; step++)
                {
                    if (run.ChoiceActive)
                    {
                        Require(choices < order.Length && run.ChoiceKind == order[choices], "Unexpected choice order: " + context);
                        core.HandleInput((GreedLastRunChannel)(matrix[row, choices] + 1), clock += 0.1f);
                        choices++;
                        continue;
                    }
                    if (run.StartCountdownActive) { core.Tick(clock += 2f); core.Tick(clock += 1f); }
                    if (!run.ActivePattern) core.RequestNextPattern(clock += 2f);
                    Require(run.ActivePattern && !run.CountdownActive, "Mixed paths must reach authored traps: " + context);
                    bool exact = accuracy == 0 || accuracy == 2 && judgements % 2 == 0;
                    float delta = exact ? 0f : (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f;
                    clock += run.Pattern.CoreSeconds + delta - run.ElapsedSeconds - core.InputTimingOffsetSeconds;
                    core.HandleInput(run.Pattern.Channel, clock);
                    judgements++;
                }
                int expectedExact = accuracy == 0 ? 15 : accuracy == 1 ? 0 : 8;
                Require(core.CanExitAfterClear && run.EscapeSucceeded && run.SaveEligible && run.Health > 0
                    && choices == 8 && judgements == 15 && run.SuccessCount == expectedExact
                    && run.GoodCount == 15 - expectedExact && run.MissCount == 0,
                    "Every mixed path must finish the real core, including all-Good survival: " + context);
                var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                backend.RegisterClear(core.CreateRunRecord());
                backend.SelectSaveLoadoutSlot(row % 3);
                backend.ConfirmSaveLoadout();
                Require(!backend.HasSaveLoadoutCandidate, "A mixed clear can be saved only once: " + context);
                backend.ConfirmSaveLoadout();
                string[] gifts = { "집중 축", "생존 축", "연쇄 축" };
                string[] relics = { "황금 박동", "사막의 잔상", "파라오의 각인" };
                string gift = gifts[matrix[row, 8]], relic = relics[matrix[row, 9]];
                for (int use = 0; use < 2; use++)
                {
                    Require(backend.TryConsumeSelectedSaveLoadout(out GreedLastRunRecord stored, out string detail,
                        out string error, gift, relic) && string.IsNullOrEmpty(error) && stored.IsValid
                        && string.IsNullOrEmpty(stored.Relic), "Mixed saved gifts must permit exactly two prepared starts: " + context);
                    GreedLastRunRecord prepared = GreedLastRunCore.CreatePreparedInfiniteLoadout(stored, gift, relic);
                    Require(prepared.Relic == relic && detail.Contains(GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(prepared), StringComparison.Ordinal)
                        && detail.Contains(GreedLastRunCore.BuildInfiniteLoadoutStartStatePreview(prepared, 0, 0, 0), StringComparison.Ordinal),
                        "The actual prepared preview must describe this start, not the normal relic: " + context);
                    VerifyInfiniteStart(backend, prepared, use == 1, context);
                    starts++;
                }
            }
            finally { core.Exit(); }
        }

        private static void VerifyInfiniteStart(GreedLastMockBackend backend, GreedLastRunRecord prepared, bool lastUse, string context)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            try
            {
                core.EnterInfiniteRun(prepared);
                Require(run.InfiniteMode && !run.Stopped && run.StartCountdownActive && run.Health > 0,
                    "Prepared mixed paths must begin their own countdown: " + context);
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                if (!run.ActivePattern) core.RequestNextPattern(clock += 2f);
                Require(run.ActivePattern, "Prepared mixed paths must publish a first infinite trap: " + context);
                clock += run.Pattern.CoreSeconds - run.ElapsedSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(run.Pattern.Channel, clock);
                Require(run.SuccessCount == 1 && run.GoodCount == 0 && run.MissCount == 0 && !run.Stopped,
                    "Prepared mixed paths must resolve a real first trap: " + context);
                if (lastUse)
                {
                    Require(!backend.TryConsumeSelectedSaveLoadout(out _, out _, out _)
                        && run.InfiniteMode && !run.Stopped && run.SuccessCount == 1,
                        "A cleared slot must reject a third start without ending its active last-use run: " + context);
                }
            }
            finally { core.Exit(); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
