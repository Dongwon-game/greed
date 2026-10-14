using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    internal static class GreedLastPrototypePreparationCheck
    {
        [MenuItem("Greed Last/Run Infinite Preparation Preview Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Preparation checks require Edit mode.");
            int mismatches = 0;
            string[] gifts = { "생존 축", "집중 축", "연쇄 축" };
            string[] relics = { "사막의 잔상", "황금 박동", "파라오의 각인" };
            for (int gift = 0; gift < gifts.Length; gift++)
                for (int relic = 0; relic < relics.Length; relic++)
                    CheckCase(gift, relic, gifts[gift], relics[relic], ref mismatches);
            Require(mismatches == 0, "Prepared entry previews must match actual start bonuses. Mismatches=" + mismatches);
            Debug.Log("Greed infinite preparation previews passed: nine isolated keys/gift-relic combinations, three selected slots, real UI callbacks/waits, partial and complete preview, cancel/reselect, two actual starts and auto-empty, named neighbor/original record preservation. No personal keys, private clock edits, full gameplay or physical input testing.");
        }

        [MenuItem("Greed Last/Run Infinite Preparation Preview Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void CheckCase(int giftIndex, int relicIndex, string gift, string relic, ref int mismatches)
        {
            string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "prepared-preview-" + Guid.NewGuid().ToString("N");
            GameObject host = new GameObject("GreedLastPreparationPreviewCheck");
            GameObject canvas = null;
            GameObject existingCamera = GameObject.Find("GreedLastCamera");
            GameObject existingEvents = GameObject.Find("GreedLastEventSystem");
            try
            {
                int slot = (giftIndex + relicIndex) % 3;
                int neighbor = (slot + 1) % 3;
                var original = new GreedLastRunRecord(true, 800, 3, 0, 1, 2, 2, 2, 1, 80f,
                    "기프트 방향", "박자 감각", "생존 축", string.Empty, "안정 루트 / 생존 축", string.Empty);
                var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                foreach (int target in new[] { slot, neighbor })
                {
                    backend.RegisterClear(original);
                    backend.SelectSaveLoadoutSlot(target);
                    backend.ConfirmSaveLoadout();
                }
                backend.RenameSelectedSaveLoadoutSlot();
                backend.SelectSaveLoadoutSlot(slot);
                var saved = Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot];
                var namedNeighbor = Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[neighbor];
                int neighborNameStep = Read<int[]>(backend, "saveLoadoutRenameSteps")[neighbor];
                backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                backend.SelectSaveLoadoutSlot(slot);

                var app = host.AddComponent<GreedLastApp>();
                Require(Read<GreedLastMockBackend>(app, "backend") == null, "The isolated app must not boot personal persistence.");
                var ui = host.AddComponent<GreedLastRuntimeUi>();
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui, playerControls: true);
                canvas = Read<GameObject>(ui, "rootPanel").GetComponentInParent<Canvas>().gameObject;
                var gate = new GreedLastRequestGate();
                var state = new GreedLastStateMachine(gate);
                var core = new GreedLastRunCore();
                Write(app, "backend", backend);
                Write(app, "requestGate", gate);
                Write(app, "stateMachine", state);
                Write(app, "runCore", core);
                Write(app, "runtimeUi", ui);
                Write(ui, "nextPatternRequested", Method(app, "RequestNextRunPattern").CreateDelegate(typeof(Action), app));
                Write(ui, "returnLobbyRequested", Method(app, "RequestReturnToLobby").CreateDelegate(typeof(Action), app));
                Write(ui, "saveLoadoutRequested", Method(app, "RequestSaveLoadout").CreateDelegate(typeof(Action), app));
                Write(ui, "runChannelRequested", Method(app, "HandleRunChannelInput").CreateDelegate(typeof(Action<GreedLastRunChannel>), app));
                state.StateChanged += (Action<GreedLastStateSnapshot>)Method(app, "HandleScreenStateChanged")
                    .CreateDelegate(typeof(Action<GreedLastStateSnapshot>), app);
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                state.SetLobbyReady(backend.LoadLobby());
                Method(app, "RequestInfiniteRun").Invoke(app, null);
                CheckPreview(state.CurrentDetail, saved, backend, "stored", gift, relic, ref mismatches);
                WaitForPreparation(app);
                Click(Read<Button[]>(ui, "laneButtons")[giftIndex]);
                var partial = GreedLastRunCore.CreatePreparedInfiniteLoadout(saved, gift, string.Empty);
                CheckPreview(state.CurrentDetail, partial, backend, "gift", gift, relic, ref mismatches);
                WaitForPreparation(app);
                Click(Read<Button[]>(ui, "laneButtons")[relicIndex]);
                var prepared = GreedLastRunCore.CreatePreparedInfiniteLoadout(saved, gift, relic);
                CheckPreview(state.CurrentDetail, prepared, backend, "complete", gift, relic, ref mismatches);
                Require(Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == 2
                    && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].Equals(saved),
                    "Partial and complete preparation must not change the stored gift direction or uses.");
                Click(Read<Button>(ui, "returnLobbyButton"));
                Require(state.CurrentState == GreedLastScreenState.LobbyReady
                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == 2,
                    "Canceling complete preparation must preserve both uses.");

                for (int use = 0; use < 2; use++)
                {
                    Method(app, "RequestInfiniteRun").Invoke(app, null);
                    Require(Read<string>(app, "infinitePreparationGift") == string.Empty
                        && Read<string>(app, "infinitePreparationRelic") == string.Empty,
                        "A new preparation must not retain the canceled or previous run's choices.");
                    WaitForPreparation(app);
                    Click(Read<Button[]>(ui, "laneButtons")[giftIndex]);
                    WaitForPreparation(app);
                    Click(Read<Button[]>(ui, "laneButtons")[relicIndex]);
                    WaitForPreparation(app);
                    Click(Read<Button>(ui, "nextPatternButton"));
                    Require(state.CurrentState == GreedLastScreenState.InfiniteRun && run.InfiniteMode
                        && !run.Stopped && run.StartCountdownActive,
                        "The separate fresh start command must enter a running infinite countdown.");
                    CheckPreview(state.CurrentDetail, prepared, backend, "entry-" + use, gift, relic, ref mismatches);
                    var bonus = (GreedLastInfiniteLoadoutBonus)typeof(GreedLastRunCore)
                        .GetMethod("CalculateInfiniteLoadoutBonus", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { prepared });
                    Require(run.Health == Mathf.Clamp(3 + bonus.HealthBonus, 1, 5)
                        && run.Combo == Mathf.Clamp(bonus.ComboBonus, 0, 9) && run.Score == bonus.ScoreBonus
                        && run.FocusStateActive == (bonus.FocusBonus >= 3)
                        && run.Focus == (bonus.FocusBonus >= 3 ? 0 : bonus.FocusBonus)
                        && run.ActiveInfiniteLoadoutName == saved.LoadoutName,
                        "The actual fresh run must use the prepared bonus without changing its stored loadout identity.");
                    Require(Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == 1 - use
                        && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].IsValid == (use == 0)
                        && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[neighbor].Equals(namedNeighbor)
                        && Read<int[]>(backend, "saveLoadoutUsesRemaining")[neighbor] == 2
                        && Read<int[]>(backend, "saveLoadoutRenameSteps")[neighbor] == neighborNameStep
                        && Read<GreedLastRunRecord>(backend, "lastRunRecord").Equals(original),
                        "Actual start must consume only its chosen slot, preserving its named neighbor and original normal record.");
                    core.Exit();
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Write(app, "backend", backend);
                    if (use == 0) backend.SelectSaveLoadoutSlot(slot);
                    state.SetLobbyReady(backend.LoadLobby());
                }
                Require(!backend.HasSaveLoadoutCandidate && backend.LoadLobby().SelectedSaveSlotIndex == neighbor,
                    "Second-use auto-empty must keep the next usable neighbor without reviving the consumed candidate.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                DestroyCreated("GreedLastCamera", existingCamera);
                DestroyCreated("GreedLastEventSystem", existingEvents);
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        private static void CheckPreview(string text, GreedLastRunRecord record, GreedLastMockBackend backend,
            string stage, string gift, string relic, ref int mismatches)
        {
            string bonus = GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(record);
            string start = GreedLastRunCore.BuildInfiniteLoadoutStartStatePreview(record,
                backend.GrowthSurvivalLevel, backend.GrowthFocusLevel, backend.GrowthTimingLevel);
            if (text.Contains(bonus, StringComparison.Ordinal) && text.Contains(start, StringComparison.Ordinal)) return;
            mismatches++;
            Debug.Log("Greed preparation preview mismatch: stage=" + stage + ", gift=" + gift + ", relic=" + relic
                + "; expected " + bonus + "; " + start.Replace("\n", " / "));
        }

        private static void WaitForPreparation(GreedLastApp app)
        {
            float deadline = Read<float>(app, "infinitePreparationInputBlockUntil");
            int milliseconds = Mathf.CeilToInt(Mathf.Max(0f, deadline - Time.realtimeSinceStartup) * 1000f) + 30;
            if (milliseconds > 0) System.Threading.Thread.Sleep(milliseconds);
        }

        private static void Click(Button button)
        {
            Require(button.gameObject.activeInHierarchy && button.IsInteractable(), "The preparation control must be visible and enabled.");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static MethodInfo Method(object target, string name)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static T Read<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Write(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void DestroyCreated(string name, GameObject existing)
        {
            GameObject created = GameObject.Find(name);
            if (existing == null && created != null) UnityEngine.Object.DestroyImmediate(created);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
