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
            RunGrowthStartPreview();
            RunPreparedProfilePreviewCheck();
            Debug.Log("Greed infinite preparation previews passed: nine isolated keys/gift-relic combinations, three selected slots, real UI callbacks/waits, partial/complete/rewound previews, distinct stored loadout switch/return, cancel/new preparation, two actual starts and auto-empty, named neighbor/original record preservation. No personal keys, private clock edits, full gameplay or physical input testing.");
        }

        [MenuItem("Greed Last/Run Infinite Preparation Preview Check", true)]
        [MenuItem("Greed Last/Run Growth Infinite Start Preview Check", true)]
        [MenuItem("Greed Last/Run Prepared Profile Preview Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Greed Last/Run Prepared Profile Preview Check")]
        public static void RunPreparedProfilePreviewCheck()
        {
            Require(CanRun(), "Prepared profile checks require Edit mode.");
            string[] summaries = {
                "박자 감각 / 안정 루트 / 생존 축 / 변주 루트 / 생존 축 / 압박 루트 / 집중 축",
                "안정 호흡 / 변주 루트 / 집중 축 / 안정 루트 / 집중 축 / 압박 루트 / 생존 축",
                "고점 본능 / 안정 루트 / 연쇄 축 / 안정 루트 / 생존 축 / 변주 루트 / 연쇄 축"
            };
            string[] starts = { "박자 감각", "안정 호흡", "고점 본능" };
            string[] primaryGifts = { "집중 축", "생존 축", "연쇄 축" };
            string[] gifts = { "생존 축", "집중 축", "연쇄 축" };
            string[] relics = { "사막의 잔상", "황금 박동", "파라오의 각인" };
            int cases = 0, changedProfiles = 0, mismatches = 0;
            foreach (string gift in gifts)
                foreach (string relic in relics)
                    for (int profile = 0; profile < summaries.Length; profile++)
                    {
                        var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                        var original = new GreedLastRunRecord(true, 800, 3, 0, 0, 4, 3, 1, 0, 60f,
                            "정비 성향 " + profile, starts[profile], primaryGifts[profile], string.Empty,
                            summaries[profile], string.Empty);
                        int slot = cases % 3;
                        backend.RegisterClear(original);
                        backend.SelectSaveLoadoutSlot(slot);
                        backend.ConfirmSaveLoadout();
                        var stored = Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot];
                        var prepared = GreedLastRunCore.CreatePreparedInfiniteLoadout(stored, gift, relic);
                        var core = new GreedLastRunCore();
                        GreedLastRunSnapshot run = default;
                        core.SnapshotChanged += value => run = value;
                        try
                        {
                            core.EnterInfiniteRun(stored);
                            string storedProfile = run.ActiveInfiniteLoadoutProfile;
                            Require(backend.BuildActiveLoadoutText().Contains("\n성향: " + storedProfile + "\n", StringComparison.Ordinal),
                                "An unprepared preview must retain the stored profile and route.");
                            string active = backend.BuildActiveLoadoutText(gift, relic);
                            string ready = backend.BuildInfiniteStartReadyText(gift, relic);
                            Require(Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == 2
                                && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].Equals(stored),
                                "Reading prepared previews must not consume or modify the saved direction.");
                            for (int use = 0; use < 2; use++)
                            {
                                Require(backend.TryConsumeSelectedSaveLoadout(out GreedLastRunRecord consumed,
                                    out string detail, out string error, gift, relic) && string.IsNullOrEmpty(error)
                                    && consumed.Equals(stored), "A prepared start must consume the chosen stored direction only.");
                                core.EnterInfiniteRun(prepared);
                                string expectedLine = "\n성향: " + run.ActiveInfiniteLoadoutProfile + "\n";
                                if (use == 0 && run.ActiveInfiniteLoadoutProfile != storedProfile) changedProfiles++;
                                if (!active.Contains(expectedLine, StringComparison.Ordinal)
                                    || !ready.Contains(expectedLine, StringComparison.Ordinal)
                                    || !detail.Contains(expectedLine, StringComparison.Ordinal))
                                {
                                    mismatches++;
                                    Debug.Log("Greed prepared profile mismatch: stored=" + storedProfile
                                        + ", actual=" + run.ActiveInfiniteLoadoutProfile + ", gift=" + gift
                                        + ", relic=" + relic + ", use=" + use);
                                }
                                Require(run.StartCountdownActive && !run.Stopped && run.InfiniteMode
                                    && run.ActiveRelic == relic && run.ActiveInfiniteLoadoutName == stored.LoadoutName
                                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == 1 - use
                                    && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].IsValid == (use == 0)
                                    && Read<GreedLastRunRecord>(backend, "lastRunRecord").Equals(original),
                                    "Preview labels must preserve actual starts, two-use auto-empty, run-local relic and normal history.");
                            }
                            cases++;
                        }
                        finally { core.Exit(); }
                    }
            Require(changedProfiles > 0, "Profile cases must genuinely change a stored direction after preparation.");
            Require(mismatches == 0, "Prepared profile previews must match the actual run after the new gift. Mismatches=" + mismatches);
            Debug.Log("Greed prepared profile previews passed: " + cases + " in-memory directions/gift/relic cases, "
                + changedProfiles + " changed profiles, preparation and both start notices match actual core profiles; stored identity/history, two uses and auto-empty preserved. No personal saves, engine-frame gameplay or physical input testing.");
        }

        [MenuItem("Greed Last/Run Growth Infinite Start Preview Check")]
        public static void RunGrowthStartPreview()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Growth preview checks require Edit mode.");
            GreedLastRunRecord Record(int score, string gift = "", string summary = null) => new GreedLastRunRecord(
                true, score, 3, 0, 0, 0, 0, 0, 0, 20f, "growth-preview", string.Empty,
                gift, string.Empty, summary ?? gift, string.Empty);
            GreedLastRunRecord[] records = { GreedLastRunRecord.Empty, Record(0), Record(100),
                Record(0, "집중 축"), Record(0, "생존 축"), Record(0, "연쇄 축"),
                GreedLastRunCore.CreatePreparedInfiniteLoadout(Record(100, "생존 축"), "집중 축", "황금 박동"),
                Record(0, "생존 축", "생존 축 / 생존 축") };
            int mismatches = 0;
            for (int focusLevel = 0; focusLevel <= 3; focusLevel++)
                for (int index = 0; index < records.Length; index++)
                {
                    int survivalLevel = focusLevel, timingLevel = (focusLevel + 1) % 4;
                    var core = new GreedLastRunCore();
                    GreedLastRunSnapshot run = default;
                    core.SnapshotChanged += value => run = value;
                    core.ConfigureGrowth(survivalLevel, focusLevel, timingLevel);
                    core.EnterInfiniteRun(records[index]);
                    Require(run.InfiniteMode && run.StartCountdownActive,
                        "Growth previews must be compared with an actual fresh core start.");
                    string preview = GreedLastRunCore.BuildInfiniteLoadoutStartStatePreview(
                        records[index], survivalLevel, focusLevel, timingLevel);
                    string focus = run.FocusStateActive
                        ? " / 집중 상태 " + run.FocusStateRemainingSeconds.ToString("0.0") + "초"
                        : " / 집중 " + run.Focus + "/" + run.MaxFocus;
                    string health = "시작 상태: 체력 " + run.Health + "/" + run.MaxHealth;
                    if (!preview.Contains(focus, StringComparison.Ordinal)
                        || !preview.Contains(health, StringComparison.Ordinal)
                        || !preview.Contains(" / 콤보 " + run.Combo + " / 점수 " + run.Score, StringComparison.Ordinal))
                    {
                        mismatches++;
                        Debug.Log("Greed growth preview mismatch: level=" + focusLevel + ", record=" + index
                            + ", actual=" + health + focus + ", preview=" + preview.Replace("\n", " / "));
                    }
                    core.Exit();
                }
            Require(mismatches == 0, "Growth start preview must describe actual health/focus/combo/score and duration. Mismatches=" + mismatches);
            Debug.Log("Greed growth infinite start previews passed: 32 transient starts, growth 0-3, empty/zero/score/focus/survival/chain/prepared/full-bonus controls, actual starting health gauge, focus state/duration, combo and score. No persistence, clock edits, physical input or full gameplay testing.");
        }

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
                foreach (int target in new[] { neighbor, slot })
                {
                    backend.RegisterClear(target == slot ? original : new GreedLastRunRecord(
                        true, 600, 2, 0, 0, 4, 3, 0, 0, 60f, "다른 기프트 방향", "생존 본능", "연쇄 축",
                        string.Empty, "압박 루트 / 연쇄 축", string.Empty));
                    backend.SelectSaveLoadoutSlot(target);
                    backend.ConfirmSaveLoadout();
                }
                backend.SelectSaveLoadoutSlot(neighbor);
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
                RequirePreparedPreviewFirst(state.CurrentDetail);
                WaitForPreparation(app);
                Click(Read<Button[]>(ui, "laneButtons")[relicIndex]);
                var prepared = GreedLastRunCore.CreatePreparedInfiniteLoadout(saved, gift, relic);
                CheckPreview(state.CurrentDetail, prepared, backend, "complete", gift, relic, ref mismatches);
                RequirePreparedPreviewFirst(state.CurrentDetail);
                Require(Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == 2
                    && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].Equals(saved),
                    "Partial and complete preparation must not change the stored gift direction or uses.");
                WaitForPreparation(app);
                Click(Read<Button[]>(ui, "laneButtons")[1]);
                Require(Read<int>(app, "infinitePreparationStep") == 1
                    && Read<string>(app, "infinitePreparationGift") == gift
                    && Read<string>(app, "infinitePreparationRelic") == string.Empty,
                    "Relic reselect must keep the gift and remove only the run-local relic.");
                CheckPreview(state.CurrentDetail, partial, backend, "relic-rewind", gift, relic, ref mismatches);
                WaitForPreparation(app);
                Click(Read<Button[]>(ui, "laneButtons")[(relicIndex + 1) % 3]);
                WaitForPreparation(app);
                Click(Read<Button[]>(ui, "laneButtons")[0]);
                Require(Read<int>(app, "infinitePreparationStep") == 0
                    && Read<string>(app, "infinitePreparationGift") == string.Empty
                    && Read<string>(app, "infinitePreparationRelic") == string.Empty,
                    "Gift reselect must clear both prepared choices and restore the stored preview.");
                CheckPreview(state.CurrentDetail, saved, backend, "gift-rewind", gift, relic, ref mismatches);
                WaitForPreparation(app);
                Click(Read<Button[]>(ui, "laneButtons")[giftIndex]);
                WaitForPreparation(app);
                Click(Read<Button[]>(ui, "laneButtons")[relicIndex]);
                foreach (int changedSlot in new[] { neighbor, slot })
                {
                    Click(Read<Button>(ui, "saveLoadoutButton"));
                    Require(state.CurrentState == GreedLastScreenState.InfiniteLoadoutSelect,
                        "The real preparation change action must open saved-loadout selection.");
                    Click(Read<Button[]>(ui, "laneButtons")[changedSlot]);
                    Click(Read<Button>(ui, "nextPatternButton"));
                    var changed = GreedLastRunCore.CreatePreparedInfiniteLoadout(
                        changedSlot == slot ? saved : namedNeighbor, gift, relic);
                    Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady
                        && backend.LoadLobby().SelectedSaveSlotIndex == changedSlot
                        && state.CurrentDetail.Contains(changed.LoadoutName, StringComparison.Ordinal)
                        && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == 2
                        && Read<int[]>(backend, "saveLoadoutUsesRemaining")[neighbor] == 2,
                        "Switching a prepared source must show the chosen identity without spending either slot.");
                    CheckPreview(state.CurrentDetail, changed, backend, "switch-S" + changedSlot, gift, relic, ref mismatches);
                }
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

        private static void RequirePreparedPreviewFirst(string text)
        {
            Require(text.IndexOf("시작 상태:", StringComparison.Ordinal) < text.IndexOf("기록 기준", StringComparison.Ordinal)
                && text.Contains("기프트 누적: 생존 x1", StringComparison.Ordinal),
                "The prepared start must precede historical statistics, keeping the stored gift summary unchanged.");
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
