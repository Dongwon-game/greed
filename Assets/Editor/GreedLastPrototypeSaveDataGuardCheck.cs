using System;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeSaveDataGuardCheck
    {
        [MenuItem("Greed Last/Run Unreadable Save Protection Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run save protection checks in idle Edit mode.");
            string prefix = GreedLastMockBackend.PersistenceTestKeyPrefix + "read-guard-" + Guid.NewGuid().ToString("N");
            string key = prefix + ".blocked", neighborKey = prefix + ".neighbor";
            var record = new GreedLastRunRecord(true, 150, 3, 1, 2, 4, 3, 1, 0, 21.25f,
                "guard-record", "박자 감각", "집중 축", "황금 박동", "안정 루트 / 집중 축", string.Empty);
            int rejectedProfiles = 0;
            try
            {
                var neighbor = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(neighborKey);
                neighbor.RegisterClear(record);
                neighbor.SelectSaveLoadoutSlot(1);
                neighbor.ConfirmSaveLoadout();
                string neighborJson = PlayerPrefs.GetString(neighborKey);
                foreach (string original in new[] { string.Empty, " ", "not-json", "null", "{}", "{\"version\":999}" })
                {
                    PlayerPrefs.SetString(key, original);
                    var blocked = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    blocked.LoadLobby();
                    blocked.SelectSaveLoadoutSlot(2);
                    Require(PlayerPrefs.GetString(key) == original,
                        "Selecting an empty slot must not replace unreadable or unsupported saved data.");
                    GreedLastConnectResult connection = blocked.CheckConnect();
                    Require(!connection.Success && connection.Reason == GreedLastConnectBlockReason.SaveDataUnavailable
                        && connection.Message.Contains("기존 저장"),
                        "Unreadable saved data must block ordinary game entry before new progress can be mistaken for saved progress.");
                    blocked.RegisterClear(record);
                    blocked.SelectSaveLoadoutSlot(2);
                    blocked.ConfirmSaveLoadout();
                    blocked.TryConsumeSelectedSaveLoadout(out _, out _, out _);
                    Require(PlayerPrefs.GetString(key) == original,
                        "Even direct backend write commands must preserve the original unreadable value.");
                    Require(PlayerPrefs.GetString(neighborKey) == neighborJson,
                        "A blocked profile must never alter another profile's records or uses.");
                    rejectedProfiles++;
                }
                PlayerPrefs.DeleteKey(key);
                var fresh = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                Require(fresh.CheckConnect().Success, "A missing save key must remain a writable new profile.");
                fresh.RegisterClear(record);
                fresh.SelectSaveLoadoutSlot(2);
                fresh.ConfirmSaveLoadout();
                var valid = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                Require(valid.CheckConnect().Success && valid.LoadLobby().SaveSlotOccupied[2]
                    && valid.TryConsumeSelectedSaveLoadout(out _, out _, out _),
                    "A recognized valid profile must still reload, save and consume normally.");
                var after = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                Require(after.CheckConnect().Success && after.LoadLobby().SaveSlotOccupied[2]
                    && after.TryConsumeSelectedSaveLoadout(out _, out _, out _)
                    && !GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key).LoadLobby().SaveSlotOccupied[2],
                    "Recognized saves must retain two real uses and automatic emptying.");
                Require(rejectedProfiles == 6 && PlayerPrefs.GetString(neighborKey) == neighborJson,
                    "All unreadable forms and independent-profile preservation must complete.");
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.DeleteKey(neighborKey);
                PlayerPrefs.Save();
            }
            Debug.Log("Greed unreadable save protection passed: six unreadable/unsupported profiles retain original bytes through entry/write attempts; missing/valid data stays writable with two-use auto-empty, independent profile unchanged. Test keys only; no personal recovery or process-kill guarantee.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
