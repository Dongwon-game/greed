using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeCandidateCheck
    {
        [MenuItem("Greed Last/Run Repeated Clear Save Check")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Candidate checks require Edit mode.");
            int failures = 0;
            for (int slot = 0; slot < 3; slot++)
            {
                string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "repeated-clear-" + Guid.NewGuid().ToString("N");
                try
                {
                    var record = new GreedLastRunRecord(true, 800, 3, 0, 1, 6, 15, 0, 0, 90f,
                        "repeat-clear", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                    var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    backend.RegisterClear(record);
                    string firstToken = ReadToken(key);
                    Require(Guid.TryParseExact(firstToken, "N", out _), "A fresh clear must persist its canonical identity.");
                    backend.SelectSaveLoadoutSlot(slot);
                    backend.ConfirmSaveLoadout();
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(!backend.HasSaveLoadoutCandidate, "The first clear's saved candidate must remain consumed.");
                    backend.RegisterClear(record);
                    string secondToken = ReadToken(key);
                    Require(secondToken != firstToken, "A new clear must have a new identity even with the same statistics.");
                    Require(backend.HasSaveLoadoutCandidate, "A distinct new clear must offer its own one-time save.");
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    if (!backend.HasSaveLoadoutCandidate)
                    {
                        Debug.Log("Greed repeated clear mismatch: new clear with identical statistics lost its pending candidate after reload; first slot=" + slot);
                        failures++;
                        continue;
                    }
                    int next = (slot + 1) % 3;
                    backend.SelectSaveLoadoutSlot(next);
                    backend.ConfirmSaveLoadout();
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(!backend.HasSaveLoadoutCandidate && backend.LoadLobby().SaveSlotOccupied[slot]
                        && backend.LoadLobby().SaveSlotOccupied[next], "Two distinct clears may each save once without deleting the prior slot.");
                    backend.SelectSaveLoadoutSlot((slot + 2) % 3);
                    backend.ConfirmSaveLoadout();
                    Require(!backend.LoadLobby().SaveSlotOccupied[(slot + 2) % 3], "A consumed second clear must not produce a third copy.");
                    Require(ReadToken(key) == ReadField<string>(ReadData(key), "resolvedSaveLoadoutCandidateToken"),
                        "Saving must persist the same clear's resolved identity.");
                    foreach (int saved in new[] { slot, next })
                    {
                        backend.SelectSaveLoadoutSlot(saved);
                        backend.DeleteSelectedSaveLoadoutSlot();
                    }
                    MutateData(key, data => WriteField(data, "saveLoadoutCandidateAvailable", true));
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(!backend.HasSaveLoadoutCandidate, "An old available flag must not revive a resolved clear after its slots disappear.");
                    backend.RegisterClear(record);
                    backend.DiscardSaveLoadoutCandidate();
                    MutateData(key, data => WriteField(data, "saveLoadoutCandidateAvailable", true));
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(!backend.HasSaveLoadoutCandidate, "Discard must resolve its clear independently of occupied slots.");
                    backend.RegisterClear(record);
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(backend.HasSaveLoadoutCandidate, "A later identical clear must remain eligible after the prior one was discarded.");
                }
                finally
                {
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                }
            }
            Require(failures == 0, "Repeated clear candidates must survive reload independently of equal statistics. Failures=" + failures);
            VerifyCompatibility();
            Debug.Log("Greed repeated clear saves passed: nine isolated keys, three slot rotations/new identities, equal-stat reload/one save, preserved prior slots, resolved flag replay after deletion/discard, six legacy/malformed/case controls. Personal save keys untouched; not full-play, security or hardware testing.");
        }

        private static void VerifyCompatibility()
        {
            foreach (string scenario in new[] { "legacy-pending", "legacy-saved", "malformed-pending", "malformed-saved", "case-pending", "case-saved" })
            {
                string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "candidate-compat-" + Guid.NewGuid().ToString("N");
                try
                {
                    bool saved = scenario.EndsWith("saved", StringComparison.Ordinal);
                    var record = new GreedLastRunRecord(true, 800, 3, 0, 1, 6, 15, 0, 0, 90f,
                        "compatible-clear", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                    var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    backend.RegisterClear(record);
                    string token = ReadToken(key);
                    if (saved) backend.ConfirmSaveLoadout();
                    MutateData(key, data =>
                    {
                        WriteField(data, "saveLoadoutCandidateAvailable", true);
                        if (scenario.StartsWith("case", StringComparison.Ordinal))
                        {
                            WriteField(data, "saveLoadoutCandidateToken", token.ToUpperInvariant());
                            WriteField(data, "resolvedSaveLoadoutCandidateToken", saved ? token.ToLowerInvariant() : string.Empty);
                        }
                        else
                        {
                            string value = scenario.StartsWith("legacy", StringComparison.Ordinal) ? string.Empty : "invalid-token";
                            WriteField(data, "saveLoadoutCandidateToken", value);
                            WriteField(data, "resolvedSaveLoadoutCandidateToken", value);
                        }
                    });
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(backend.HasSaveLoadoutCandidate == !saved, scenario + ": pending legacy clear must survive, already saved clear must stay resolved.");
                    Require(ReadField<int>(ReadData(key), "version") == 1, "Optional identities must retain the existing data version.");
                    if (saved)
                    {
                        GreedLastLobbySnapshot lobby = backend.LoadLobby();
                        Require(lobby.SaveSlotOccupied[0] && lobby.SaveSlotUsable[0]
                            && ReadField<int[]>(ReadData(key), "saveLoadoutUsesRemaining")[0] == 2,
                            scenario + ": legacy duplicate repair must not delete the stored record or consume its uses.");
                    }
                    else
                    {
                        backend.SelectSaveLoadoutSlot(2);
                        backend.ConfirmSaveLoadout();
                        backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        string current = ReadToken(key);
                        Require(!backend.HasSaveLoadoutCandidate && backend.LoadLobby().SaveSlotOccupied[2]
                            && Guid.TryParseExact(current, "N", out _) && current == current.ToLowerInvariant()
                            && current == ReadField<string>(ReadData(key), "resolvedSaveLoadoutCandidateToken"),
                            scenario + ": confirming an old pending candidate must persist one canonical resolved identity.");
                    }
                }
                finally
                {
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                }
            }
        }

        private static object ReadData(string key)
            => JsonUtility.FromJson(PlayerPrefs.GetString(key), typeof(GreedLastMockBackend).GetNestedType("BackendSaveData", BindingFlags.NonPublic));

        private static T ReadField<T>(object data, string name) => (T)data.GetType().GetField(name).GetValue(data);
        private static void WriteField(object data, string name, object value) => data.GetType().GetField(name).SetValue(data, value);
        private static string ReadToken(string key) => ReadField<string>(ReadData(key), "saveLoadoutCandidateToken");

        private static void MutateData(string key, Action<object> change)
        {
            object data = ReadData(key);
            change(data);
            PlayerPrefs.SetString(key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        [MenuItem("Greed Last/Run Repeated Clear Save Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
