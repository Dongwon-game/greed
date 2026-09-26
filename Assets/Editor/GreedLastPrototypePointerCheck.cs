using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    internal static class GreedLastPrototypePointerCheck
    {
        public static void Run(GreedLastRuntimeUi ui)
        {
            GreedLastPrototypeKeyboardCheck.Run(ui);
            foreach (string field in new[] { "normalRunButton", "saveLoadoutButton", "nextPatternButton", "returnLobbyButton", "saveSlotDeleteButton", "pauseButton" })
                Require(Read<Button>(ui, field) is GreedLastCommandButton,
                    "Runtime action buttons must use the interruption-safe command component: " + field);
            VerifyCommandPressLifetime();
            FieldInfo callback = Field("runChannelRequested");
            object previousCallback = callback.GetValue(ui);
            CanvasGroup group = null;
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            core.Enter();
            float clock = Time.realtimeSinceStartup;
            core.RestartPractice(clock);
            core.Tick(clock += 2f);
            core.Tick(clock += 1f);
            try
            {
                int calls = 0;
                GreedLastRunChannel lastChannel = GreedLastRunChannel.None;
                callback.SetValue(ui, (Action<GreedLastRunChannel>)(channel => { calls++; lastChannel = channel; }));
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.RunCoreTest, "Pointer check", string.Empty, 0));
                ui.RenderRun(run);
                Button lane = Read<Button[]>(ui, "laneButtons")[0];
                Button hitZone = Read<Button[]>(ui, "laneHitButtons")[0];
                bool[] held = Read<bool[]>(ui, "laneHoldActive");
                Require(!Read<Text>(ui, "timingGuideText").raycastTarget,
                    "The timing guide must not intercept the center lane's expanded touch margin.");
                PointerEventData first = Pointer(10);
                PointerEventData second = Pointer(11);
                PointerEventData right = Pointer(-2, PointerEventData.InputButton.Right);
                PointerEventData middle = Pointer(-3, PointerEventData.InputButton.Middle);

                Down(lane, right);
                Down(lane, middle);
                Require(calls == 0 && !held[0], "Non-primary mouse buttons must not submit a lane input.");
                Down(lane, first);
                Require(calls == 1 && lastChannel == GreedLastRunChannel.Left && held[0],
                    "A fresh tap must submit on pointer-down without waiting for release.");
                Down(lane, first);
                Down(hitZone, first);
                Require(calls == 1, "One pointer must not submit twice through overlapping lane surfaces.");
                ExecuteEvents.Execute(lane.gameObject, first, ExecuteEvents.pointerClickHandler);
                Require(calls == 1, "The release click must not create a second rhythm input.");
                Down(hitZone, second);
                Require(calls == 2 && held[0], "A second finger must remain a distinct tap.");
                Up(lane, first);
                Up(lane, right);
                Up(lane, Pointer(99));
                Require(held[0], "Releasing another pointer must not clear a finger still held on this lane.");
                ExecuteEvents.Execute(hitZone.gameObject, second, ExecuteEvents.pointerExitHandler);
                Require(!held[0], "Exiting the last held pointer must release the lane visual.");
                Down(lane, first);
                ExecuteEvents.Execute(lane.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
                Require(!held[0], "Cancellation must clear lane pointer ownership.");

                foreach (string lifecycle in new[] { "OnApplicationFocus", "OnApplicationPause" })
                {
                    Down(lane, first);
                    int beforeFocusReturn = calls;
                    typeof(GreedLastRuntimeUi).GetMethod(lifecycle, BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(ui, new object[] { lifecycle == "OnApplicationPause" });
                    Require(!held[0], "Backgrounding must clear a pointer even when a choice does not pause the core.");
                    typeof(GreedLastRuntimeUi).GetMethod(lifecycle, BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(ui, new object[] { lifecycle == "OnApplicationFocus" });
                    Down(lane, first);
                    Require(calls == beforeFocusReturn + 1, "A released-outside pointer ID must be usable after returning.");
                    Up(lane, first);
                }

                int blockedCalls = calls;
                group = lane.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                Require(!lane.IsInteractable(), "The blocked-group fixture must disable its selectable.");
                Down(lane, first);
                Require(calls == blockedCalls && !held[0], "A CanvasGroup-disabled lane must reject pointer input.");
                UnityEngine.Object.DestroyImmediate(group);
                group = null;
                lane.enabled = false;
                Down(lane, first);
                Require(calls == blockedCalls, "A disabled Button component must reject its EventTrigger input.");
                lane.enabled = true;

                Down(lane, first);
                Require(held[0], "A re-enabled lane must accept a fresh pointer.");
                core.TogglePause(clock + 0.1f);
                ui.RenderRun(run);
                Require(!held[0] && !lane.IsInteractable(), "Pausing must clear held fingers and disable lane input.");
                blockedCalls = calls;
                Down(lane, second);
                Require(calls == blockedCalls, "Paused lanes must not send a tap through to gameplay.");
                core.TogglePause(clock + 0.2f);
                ui.RenderRun(run);
                Down(hitZone, second);
                Require(calls == blockedCalls && !held[0], "Resume countdown must keep the enlarged hit zone locked.");
                core.Tick(clock += 3f);
                ui.RenderRun(run);
                Down(lane, first);
                Require(calls == blockedCalls + 1 && held[0], "GO must re-enable a pointer ID cleared by pause.");
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.LobbyReady, "Lobby", string.Empty, 0));
                Require(!held[0], "Returning to the lobby must not retain a held lane.");
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.RunCoreTest, "Restart", string.Empty, 0));
                core.RestartPractice(clock);
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                ui.RenderRun(run);
                Down(lane, first);
                Require(calls == blockedCalls + 2, "A new run must accept a reused finger ID after leaving a held lane.");
                Up(lane, first);
                core.Enter();
                ui.RenderRun(run);
                Down(lane, first);
                Require(run.ChoiceActive && held[0], "Choice inputs must also own their held pointer.");
                // Edit-mode fixtures invoke Unity lifecycle hooks explicitly.
                typeof(GreedLastRuntimeUi).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                Require(!held[0], "Disabling the UI must release a held choice pointer without needing a core pause.");
                typeof(GreedLastRuntimeUi).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                int beforeChoiceReturn = calls;
                Down(lane, first);
                Require(calls == beforeChoiceReturn + 1, "The same finger ID must work when the choice UI returns.");
                Up(lane, first);
                Debug.Log("Greed Last pointer checks passed: down-only input, mouse filtering, distinct fingers, duplicate suppression, disabled groups, focus/background release, pause/countdown and lobby reset.");
            }
            finally
            {
                if (group != null) UnityEngine.Object.DestroyImmediate(group);
                callback.SetValue(ui, previousCallback);
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.LobbyReady, "Lobby", string.Empty, 0));
            }
        }

        private static void VerifyCommandPressLifetime()
        {
            GameObject host = new GameObject("GreedLastCommandPressCheck", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var command = host.AddComponent<GreedLastCommandButton>();
                command.navigation = new Navigation { mode = Navigation.Mode.None };
                CanvasGroup group = host.GetComponent<CanvasGroup>();
                int clicks = 0;
                command.onClick.AddListener(() => clicks++);
                PointerEventData first = Pointer(21);
                PointerEventData second = Pointer(22);
                void Press(PointerEventData pointer)
                {
                    pointer.eligibleForClick = true;
                    Down(command, pointer);
                }
                void Release(PointerEventData pointer)
                {
                    Up(command, pointer);
                    if (pointer.eligibleForClick)
                        ExecuteEvents.Execute(command.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                }
                foreach (string reason in new[] { "cancel", "component", "object", "interaction", "group" })
                {
                    int before = clicks;
                    Press(first);
                    Press(second);
                    switch (reason)
                    {
                        case "cancel": command.CancelPendingPresses(); break;
                        case "component": command.enabled = false; command.enabled = true; break;
                        case "object": host.SetActive(false); host.SetActive(true); break;
                        case "interaction": command.interactable = false; command.interactable = true; break;
                        case "group": group.interactable = false; group.interactable = true; break;
                    }
                    Require(!first.eligibleForClick && !second.eligibleForClick,
                        "Every held command pointer must be canceled by " + reason);
                    Release(first);
                    Release(second);
                    Require(clicks == before, "Canceled commands must never call onClick: " + reason);
                    Press(first);
                    command.interactable = true;
                    command.colors = command.colors;
                    Release(first);
                    Require(clicks == before + 1, "Fresh commands must survive unchanged presentation properties: " + reason);
                }
                int beforeLocked = clicks;
                command.interactable = false;
                Press(first);
                command.interactable = true;
                Release(first);
                Require(clicks == beforeLocked, "A disabled command must not queue a future click.");
                PointerEventData right = Pointer(-2, PointerEventData.InputButton.Right);
                Press(right);
                Release(right);
                Require(clicks == beforeLocked, "Secondary mouse input must remain ignored.");
                Debug.Log("Greed Last command lifetime checks passed: two held pointers, cancellation, hidden/disabled UI, transient locks, blocked-start input and fresh-click recovery. Direct event tests; rendered module checks are separate.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static PointerEventData Pointer(int id, PointerEventData.InputButton button = PointerEventData.InputButton.Left)
            => new PointerEventData(EventSystem.current) { pointerId = id, button = button };
        private static void Down(Button button, PointerEventData pointer)
            => ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        private static void Up(Button button, PointerEventData pointer)
            => ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        private static FieldInfo Field(string name)
            => typeof(GreedLastRuntimeUi).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static T Read<T>(GreedLastRuntimeUi ui, string name) => (T)Field(name).GetValue(ui);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
