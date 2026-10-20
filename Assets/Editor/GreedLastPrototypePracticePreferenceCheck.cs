using System;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypePracticePreferenceCheck
    {
        [MenuItem("Greed Last/Run Onboarding Fixture Safety Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Fixture safety checks require Edit mode.");
            int cases = 0;
            foreach (int original in new[] { -1, 0, 1 })
            {
                bool existed = original >= 0;
                Store Fresh() => new Store { Exists = existed, RawValue = original };
                void Prepare(Store store) => GreedLastPrototypePracticePreference.Prepare(store, existed, original);
                void Restore(Store store)
                {
                    Require(GreedLastPrototypePracticePreference.Restore(store, existed, original), "Owned fixtures must restore.");
                    Require(!store.Owned && store.Exists == existed && (!existed || store.Value == original),
                        "Restoration must preserve absence, zero and one distinctly.");
                    int saves = store.Saves;
                    Require(!GreedLastPrototypePracticePreference.Restore(store, existed, original) && store.Saves == saves,
                        "Restoring twice must not touch the store again.");
                }

                var clean = Fresh();
                Prepare(clean);
                Require(clean.Exists && clean.Value == 1 && clean.Owned && clean.Backups == 1 && clean.Saves == 1,
                    "Preparation must acquire one durable baseline before applying the temporary completion flag.");
                ExpectFailure(() => Prepare(clean));
                Require(clean.Backups == 1, "A repeated preparation must not overwrite an owned baseline.");
                Restore(clean);
                cases++;

                foreach (string fault in new[] { "backup", "write", "save", "apply-readback" })
                {
                    var store = Fresh();
                    store.Fault = fault;
                    ExpectFailure(() => Prepare(store));
                    if (fault == "backup")
                        Require(!store.Owned && store.Writes == 0 && store.Saves == 0
                            && store.Exists == existed && (!existed || store.Value == original),
                            "Backup failure must occur before any preference mutation or ownership acquisition.");
                    else
                    {
                        Require(store.Owned && store.Backups == 1, "Failed mutation must retain the baseline and restoration ownership.");
                        store.Fault = null;
                        Restore(store);
                    }
                    cases++;
                }
                foreach (string fault in new[] { "write", "save", "restore-readback" })
                {
                    var store = Fresh();
                    Prepare(store);
                    store.Fault = fault;
                    ExpectFailure(() => GreedLastPrototypePracticePreference.Restore(store, existed, original));
                    Require(store.Owned && store.Backups == 1, "Failed restoration must retain its original owned baseline.");
                    ExpectFailure(() => Prepare(store));
                    store.Fault = null;
                    Restore(store);
                    cases++;
                }
            }
            var unknown = new Store { Exists = true, RawValue = 42 };
            ExpectFailure(() => GreedLastPrototypePracticePreference.Prepare(unknown, true, 42));
            Require(unknown.Backups == 0 && unknown.Writes == 0 && !unknown.Owned,
                "Unknown baseline values must be rejected without mutation.");
            var changed = new Store { Exists = true, RawValue = 0 };
            ExpectFailure(() => GreedLastPrototypePracticePreference.Prepare(changed, true, 1));
            Require(changed.Backups == 0 && changed.Writes == 0 && !changed.Owned,
                "Stale captured baselines must be rejected before backup or mutation.");
            cases += 2;
            Debug.Log($"Greed onboarding fixture safety passed: {cases} in-memory cases; absent/zero/one baselines, "
                + "backup/apply/save/readback failures, owned-baseline reacquisition refusal and idempotent restoration. No PlayerPrefs or files written.");
        }

        private sealed class Store : GreedLastPrototypePracticePreference.IStore
        {
            public bool Exists { get; set; }
            public int RawValue;
            public bool Owned { get; set; }
            public string Fault;
            public int Backups, Writes, Saves;
            private bool restoring;
            public int Value
            {
                get => Fault == "apply-readback" && Saves > 0 ? 42 : RawValue;
                set
                {
                    Fail("write");
                    Writes++;
                    Exists = true;
                    RawValue = value;
                }
            }
            public void Delete()
            {
                Fail("write");
                Writes++;
                Exists = false;
            }
            public void Save()
            {
                Saves++;
                Fail("save");
                if (restoring && Fault == "restore-readback") { Exists = true; RawValue = 42; }
                restoring = true;
            }
            public void Backup(bool exists, int value)
            {
                Fail("backup");
                Require(Exists == exists && (!exists || RawValue == value), "The backup must retain the exact captured baseline.");
                Backups++;
            }
            private void Fail(string operation)
            { if (Fault == operation) throw new InvalidOperationException("Isolated " + operation + " failure"); }
        }

        private static void ExpectFailure(Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { return; }
            throw new InvalidOperationException("The isolated fixture fault was not detected.");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
