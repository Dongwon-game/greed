using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeSaveLifetimeModelCheck
    {
        [MenuItem("Greed Last/Run Save Lifetime Sequence Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Save sequence checks require Edit mode.");
            int steps = 0;
            foreach (int seed in new[] { 42, 271828, 773 }) steps += VerifySeed(seed);
            Debug.Log("Greed save lifetime sequences passed: three deterministic seeded backend command traces, "
                + steps + " checked transitions, independent three-slot/candidate/use-count model, duplicate confirmations, "
                + "same-stat fresh candidates, reloads, rename/delete/discard and isolated neighbor preservation. "
                + "Dedicated test keys and seeded records only; not actual played clears, UI commands or new-process proof.");
        }

        private static int VerifySeed(int seed)
        {
            string prefix = GreedLastMockBackend.PersistenceTestKeyPrefix + "lifetime-model-" + Guid.NewGuid().ToString("N");
            string key = prefix + ".active", neighborKey = prefix + ".neighbor";
            Require(!PlayerPrefs.HasKey(key) && !PlayerPrefs.HasKey(neighborKey), "The model must own fresh test keys.");
            var source = new GreedLastRunRecord[3];
            var expectedSummary = new string[3];
            string[] gifts = { "집중 축", "생존 축", "연쇄 축" };
            for (int i = 0; i < source.Length; i++)
            {
                expectedSummary[i] = "박자 감각 / 안정 루트 / " + gifts[i];
                source[i] = new GreedLastRunRecord(true, 300 + i * 50, 3, 2, 1, 4, 3, 1, 0,
                    20f, "lifetime-source-" + i, "박자 감각", gifts[i], "황금 박동",
                    expectedSummary[i] + " / 황금 박동", "정박 최고 3");
            }
            var uses = new int[3];
            var template = new[] { -1, -1, -1 };
            int candidate = -1, step = 0;
            var trace = new Queue<string>();
            GreedLastMockBackend backend = null;
            try
            {
                var neighbor = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(neighborKey);
                neighbor.RegisterClear(source[1]);
                neighbor.SelectSaveLoadoutSlot(1);
                neighbor.ConfirmSaveLoadout();
                string neighborJson = PlayerPrefs.GetString(neighborKey);
                backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);

                void Check(string action)
                {
                    step++;
                    trace.Enqueue(action);
                    if (trace.Count > 16) trace.Dequeue();
                    string context = "seed=" + seed + " step=" + step + " trace=" + string.Join(",", trace);
                    GreedLastLobbySnapshot lobby = backend.LoadLobby();
                    Require(backend.HasSaveLoadoutCandidate == (candidate >= 0), "Candidate lifetime differs: " + context);
                    for (int slot = 0; slot < 3; slot++)
                    {
                        Require(lobby.SaveSlotOccupied[slot] == (uses[slot] > 0)
                            && lobby.SaveSlotUsable[slot] == (uses[slot] > 0), "Slot lifetime differs: " + context);
                        if (uses[slot] > 0)
                            Require(lobby.SaveSlotLabels[slot].Contains(uses[slot] + "/2", StringComparison.Ordinal),
                                "The displayed use count differs from the independent model: " + context);
                    }
                    Require(PlayerPrefs.GetString(neighborKey) == neighborJson, "Another profile changed: " + context);
                }

                void Clear(int index)
                {
                    backend.RegisterClear(source[index]);
                    candidate = index;
                    Check("clear" + index);
                }

                void Save(int slot, int repeats)
                {
                    backend.SelectSaveLoadoutSlot(slot);
                    for (int repeat = 0; repeat < repeats; repeat++) backend.ConfirmSaveLoadout();
                    if (candidate >= 0) { uses[slot] = 2; template[slot] = candidate; candidate = -1; }
                    Check("save" + slot + "x" + repeats);
                }

                void Consume(int slot)
                {
                    backend.SelectSaveLoadoutSlot(slot);
                    bool consumed = backend.TryConsumeSelectedSaveLoadout(out GreedLastRunRecord record, out _, out _);
                    Require(consumed == (uses[slot] > 0), "Unexpected use acceptance: seed=" + seed + " step=" + step);
                    if (consumed)
                    {
                        int index = template[slot];
                        Require(index >= 0 && record.Score == source[index].Score && record.PrimaryGift == gifts[index]
                            && record.StartGift == source[index].StartGift && record.Relic == ""
                            && record.ChoiceSummary == expectedSummary[index] && record.TimingProfile == source[index].TimingProfile,
                            "Consumption returned another slot or changed its gift-only record: seed=" + seed + " step=" + step);
                        if (--uses[slot] == 0) template[slot] = -1;
                    }
                    Check("consume" + slot);
                }

                void Reload()
                {
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Check("reload");
                }

                // Fixed boundaries precede random traces so rare operations cannot be missed by a seed.
                Clear(0); Save(0, 3); Consume(0); Reload();
                backend.SelectSaveLoadoutSlot(0); backend.RenameSelectedSaveLoadoutSlot(); Check("rename0");
                Consume(0); Reload(); Save(0, 3); Consume(0);
                Clear(1); Save(1, 1); Clear(1); Save(2, 3); Reload();
                backend.SelectSaveLoadoutSlot(1); backend.DeleteSelectedSaveLoadoutSlot(); uses[1] = 0; template[1] = -1; Check("delete1");
                Consume(2); Reload(); Consume(2); Reload();
                var random = new System.Random(seed);
                for (int turn = 0; turn < 250; turn++)
                {
                    int action = random.Next(9), slot = random.Next(3);
                    var occupied = new List<int>();
                    for (int i = 0; i < 3; i++) if (uses[i] > 0) occupied.Add(i);
                    if (occupied.Count > 0 && (action == 2 || action == 3 || action == 4))
                        slot = occupied[random.Next(occupied.Count)];
                    switch (action)
                    {
                        case 0: Clear(random.Next(3)); break;
                        case 1: Save(slot, 1); break;
                        case 2: Consume(slot); break;
                        case 3:
                            backend.SelectSaveLoadoutSlot(slot); backend.DeleteSelectedSaveLoadoutSlot();
                            uses[slot] = 0; template[slot] = -1; Check("delete" + slot); break;
                        case 4:
                            backend.SelectSaveLoadoutSlot(slot); backend.RenameSelectedSaveLoadoutSlot(); Check("rename" + slot); break;
                        case 5:
                            backend.DiscardSaveLoadoutCandidate(); candidate = -1; Check("discard"); break;
                        case 6: Reload(); break;
                        case 7:
                            backend.BuildSaveLoadoutDraftText(); backend.BuildSelectedSaveLoadoutComparisonText();
                            backend.BuildSelectedInfiniteLoadoutDetailText(); Check("read"); break;
                        case 8: Save(slot, 3); break;
                    }
                }
                Reload();
                return step;
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.DeleteKey(neighborKey);
                PlayerPrefs.Save();
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
