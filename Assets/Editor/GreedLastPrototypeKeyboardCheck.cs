using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    internal static class GreedLastPrototypeKeyboardCheck
    {
        [MenuItem("Greed Last/Run Menu Scroll Interruption Check")]
        public static void VerifyMenuScrollInterruption()
        {
            // Reuse the rendered preview fixture; a headless overlay has no reliable raycast geometry.
            GreedLastPrototypeVisualCheck.RunMenuScrollInput();
        }

        public static void VerifyShortcutModifiers()
        {
            MethodInfo policy = typeof(GreedLastApp).GetMethod("ShouldAllowPrototypeKeyboardInput", BindingFlags.Static | BindingFlags.NonPublic);
            for (int mask = 0; mask < 8; mask++)
            {
                bool allowed = (bool)policy.Invoke(null, new object[] { (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0 });
                Require(allowed == (mask == 0), "Control/Alt/system shortcut modifiers must not route gameplay keys: mask=" + mask);
            }
            Debug.Log("Greed shortcut modifier policy passed: eight Control/Alt/system combinations; plain/Shift-compatible keys remain enabled. Boolean policy, not OS shortcut input testing.");
        }

        [MenuItem("Greed Last/Run Shortcut Hold Visual Check")]
        public static void VerifyShortcutHoldVisuals()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Shortcut hold checks require an idle Edit-mode editor.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var host = new GameObject("GreedLastShortcutHoldProbe");
            try
            {
                var ui = host.AddComponent<GreedLastRuntimeUi>();
                var keyboard = new bool[3];
                var touch = new[] { false, true, false };
                typeof(GreedLastRuntimeUi).GetField("laneKeyboardHoldActive", flags).SetValue(ui, keyboard);
                typeof(GreedLastRuntimeUi).GetField("laneHoldActive", flags).SetValue(ui, touch);
                MethodInfo apply = typeof(GreedLastRuntimeUi).GetMethod("ApplyKeyboardLaneHoldInput", flags);
                MethodInfo amount = typeof(GreedLastRuntimeUi).GetMethod("BuildLaneHoldAmount", flags);
                MethodInfo policy = typeof(GreedLastApp).GetMethod("ShouldAllowPrototypeKeyboardInput", BindingFlags.Static | BindingFlags.NonPublic);
                for (int mask = 0; mask < 8; mask++)
                {
                    bool allowed = (bool)policy.Invoke(null, new object[] { (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0 });
                    apply.Invoke(ui, new object[] { allowed, true, true, true });
                    Require(keyboard[0] == allowed && keyboard[1] == allowed && keyboard[2] == allowed,
                        "Shortcut modifiers must also suppress all keyboard hold visuals: mask=" + mask);
                    Require((float)amount.Invoke(ui, new object[] { 0 }) == (allowed ? 0.86f : 0f)
                        && (float)amount.Invoke(ui, new object[] { 1 }) == 1f
                        && touch[1], "Blocked keyboard hints must not clear a real touch hold.");
                    apply.Invoke(ui, new object[] { true, false, false, true });
                    Require(!keyboard[0] && !keyboard[1] && keyboard[2],
                        "A fresh unmodified lane hold must recover without stale shortcut highlights.");
                }
                Debug.Log("Greed shortcut hold visuals passed: eight modifier combinations, touch hold preservation and fresh unmodified recovery. Isolated production hold-state callback, not OS input or rendered-image testing.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [MenuItem("Greed Last/Run Input Borrowing Policy Check")]
        public static void VerifyInputBorrowingPolicy()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Input borrowing policy requires an idle Edit-mode editor.");
            MethodInfo policy = typeof(GreedLastRuntimeUi).GetMethod("CanBorrowEventSystem", BindingFlags.Static | BindingFlags.NonPublic);
            var host = new GameObject("GreedLastInputBorrowingProbe", typeof(EventSystem), typeof(BorrowingInputModule));
            var noModuleHost = new GameObject("GreedLastMissingModuleProbe", typeof(EventSystem));
            var legacyHost = new GameObject("GreedLastLegacyBorrowingProbe", typeof(EventSystem), typeof(StandaloneInputModule));
            try
            {
                EventSystem events = host.GetComponent<EventSystem>();
                BorrowingInputModule module = host.GetComponent<BorrowingInputModule>();
                bool CanBorrow(EventSystem value) => (bool)policy.Invoke(null, new object[] { value });
                Require(!CanBorrow(null) && !CanBorrow(noModuleHost.GetComponent<EventSystem>()),
                    "Missing systems and input modules must not be borrowed.");
                Require(CanBorrow(legacyHost.GetComponent<EventSystem>()), "The actual legacy module must remain usable.");
                foreach (bool navigation in new[] { false, true })
                {
                    events.sendNavigationEvents = navigation;
                    module.enabled = events.enabled = true;
                    module.Supported = false;
                    Require(!module.IsModuleSupported() && !CanBorrow(events),
                        "An enabled but unsupported input module must not be borrowed.");
                    module.Supported = true;
                    Require(module.IsModuleSupported() && CanBorrow(events),
                        "A usable pointer module must recover without creating a false borrowing lock.");
                    module.enabled = false;
                    Require(!CanBorrow(events), "A disabled input module must not be borrowed.");
                    module.enabled = true;
                    events.enabled = false;
                    Require(!CanBorrow(events) && events.sendNavigationEvents == navigation,
                        "The read-only policy must preserve disabled state and navigation settings.");
                    events.enabled = true;
                    Require(CanBorrow(events) && events.sendNavigationEvents == navigation,
                        "The read-only policy must preserve a usable system's settings.");
                }
                Debug.Log("Greed input borrowing policy passed: missing system/module, virtual unsupported/usable module, disabled-module/system and recovery at both navigation values; actual legacy module positive control. Edit-mode production policy with a BaseInputModule test double, not hardware or engine-frame dispatch.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(noModuleHost);
                UnityEngine.Object.DestroyImmediate(legacyHost);
            }
        }

        public sealed class BorrowingInputModule : BaseInputModule
        {
            public bool Supported { get; set; }
            public override bool IsModuleSupported() => Supported;
            public override void Process() { }
        }

        public static void Run(GreedLastRuntimeUi ui)
        {
            EventSystem system = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            Require(system != null && !system.sendNavigationEvents,
                "The prototype must leave keyboard Submit/Cancel/Move to its application router.");
            var root = (GameObject)typeof(GreedLastRuntimeUi).GetField("rootPanel",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                Require(button.navigation.mode == Navigation.Mode.None,
                    "Pointer buttons must not acquire automatic keyboard navigation: " + button.name);
            StandaloneInputModule module = system.GetComponent<StandaloneInputModule>();
            Require(module != null, "The keyboard fixture needs the prototype's actual standalone input module.");
            BaseInput originalInput = module.inputOverride;
            FieldInfo moduleSystem = typeof(BaseInputModule).GetField("m_EventSystem", BindingFlags.Instance | BindingFlags.NonPublic);
            object originalModuleSystem = moduleSystem.GetValue(module);
            GameObject originalSelection = system.currentSelectedGameObject;
            FieldInfo focus = typeof(EventSystem).GetField("m_HasFocus", BindingFlags.Instance | BindingFlags.NonPublic);
            bool originalFocus = system.isFocused;
            GameObject host = new GameObject("GreedLastKeyboardProbe");
            try
            {
                var probe = host.AddComponent<GreedLastPrototypeKeyboardInput>();
                // Edit-mode fixtures do not receive the module's normal OnEnable wiring.
                moduleSystem.SetValue(module, system);
                module.inputOverride = probe;
                focus.SetValue(system, true);
                system.SetSelectedGameObject(host);
                module.Process();
                Require(probe.Submits == 0 && probe.Cancels == 0 && probe.Moves == 0,
                    "Submit/Cancel must not reach a stale selection when the app handles those keys.");
                // Positive control proves the real module consumes our synthetic keys, not a no-op fixture.
                system.sendNavigationEvents = true;
                module.Process();
                Require(probe.Submits == 1 && probe.Cancels == 1,
                    "The positive control must reproduce Unity's default duplicate-submit path.");
                system.sendNavigationEvents = false;
                probe.Horizontal = 1f;
                module.Process();
                Require(probe.Submits == 1 && probe.Cancels == 1 && probe.Moves == 0,
                    "Gameplay direction input must not move UI focus or trigger a second selected-button action.");
                Debug.Log("Greed Last keyboard isolation passed: actual input module, positive submit/cancel control, disabled navigation and pointer-only button selection.");
            }
            finally
            {
                system.sendNavigationEvents = false;
                module.inputOverride = originalInput;
                moduleSystem.SetValue(module, originalModuleSystem);
                system.SetSelectedGameObject(originalSelection);
                focus.SetValue(system, originalFocus);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        internal static void ClickOnce(EventSystem system, Button button, bool touch, Action beforePress = null, Action whileHeld = null)
        {
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Canvas.ForceUpdateCanvases();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, button.transform.position);
            WithPointerInput(system, button.gameObject, point, (input, module) =>
            {
                beforePress?.Invoke();
                if (touch)
                {
                    TouchFrame(input, module, TouchAt(71, point, TouchPhase.Began));
                    whileHeld?.Invoke();
                    TouchFrame(input, module, TouchAt(71, point, TouchPhase.Ended));
                }
                else
                {
                    input.MouseEnabled = true;
                    input.MousePosition = point;
                    input.MouseDown = input.MouseHeld = true;
                    module.Process();
                    whileHeld?.Invoke();
                    input.MouseDown = input.MouseHeld = false;
                    input.MouseUp = true;
                    module.Process();
                }
            });
        }

        internal static void VerifyInterruptedAppCommand(GreedLastRuntimeUi ui, EventSystem system, Button button, bool touch)
        {
            int clicks = 0;
            UnityEngine.Events.UnityAction countClick = () => clicks++;
            button.onClick.AddListener(countClick);
            try
            {
                ClickOnce(system, button, touch, whileHeld: () =>
                {
                    MethodInfo focus = typeof(GreedLastRuntimeUi).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic);
                    focus.Invoke(ui, new object[] { false });
                    focus.Invoke(ui, new object[] { true });
                });
                Require(clicks == 0, "An interrupted app-bound command must not invoke any callback: " + button.name);
            }
            finally
            {
                button.onClick.RemoveListener(countClick);
            }
        }

        internal static void VerifyActionClick(GreedLastRuntimeUi ui, EventSystem system, Button button, Vector2 screenPoint,
            Action redrawWhileHeld = null)
        {
            int clicks = 0;
            bool wasInteractable = button.interactable;
            CanvasGroup testGroup = button.GetComponent<CanvasGroup>();
            bool addedGroup = testGroup == null;
            if (addedGroup) testGroup = button.gameObject.AddComponent<CanvasGroup>();
            bool groupWasInteractable = testGroup.interactable;
            UnityEngine.Events.UnityAction countClick = () => clicks++;
            button.onClick.AddListener(countClick);
            try
            {
                WithPointerInput(system, button.gameObject, screenPoint, (input, module) =>
                {
                    Require(ui.IsPointerOverInteractiveUi(screenPoint), "A command must block rotation-drag initiation.");
                    input.MouseEnabled = true;
                    input.MousePosition = screenPoint;
                    input.MouseDown = input.MouseHeld = true;
                    module.Process();
                    Require(clicks == 0, "An action button must wait for pointer release: " + button.name);
                    input.MouseDown = false;
                    for (int frame = 0; frame < 3; frame++) module.Process();
                    Require(clicks == 0, "Holding a command must not execute it: " + button.name);
                    input.MouseHeld = false;
                    input.MouseUp = true;
                    module.Process();
                    Require(clicks == 1, "A rendered command must receive exactly one real-module click: " + button.name);
                    input.MouseUp = false;
                    input.MousePosition = new Vector2(-1000f, -1000f);
                    module.Process();
                    Require(clicks == 1, "Submit/Cancel must not add an action after pointer release.");

                    input.MouseEnabled = false;
                    TouchFrame(input, module, TouchAt(80, screenPoint, TouchPhase.Began));
                    TouchFrame(input, module, TouchAt(80, screenPoint, TouchPhase.Stationary));
                    Require(clicks == 1, "A touch command must also wait for release.");
                    TouchFrame(input, module, TouchAt(80, screenPoint, TouchPhase.Canceled));
                    Require(clicks == 1, "System-canceled touch must not execute a command: " + button.name);
                    TouchFrame(input, module, TouchAt(80, screenPoint, TouchPhase.Began));
                    TouchFrame(input, module, TouchAt(80, screenPoint, TouchPhase.Ended));
                    Require(clicks == 2, "The next completed touch must execute once after cancellation.");
                    TouchFrame(input, module, TouchAt(80, screenPoint, TouchPhase.Began));
                    TouchFrame(input, module, TouchAt(80, new Vector2(-1000f, -1000f), TouchPhase.Moved));
                    TouchFrame(input, module, TouchAt(80, new Vector2(-1000f, -1000f), TouchPhase.Ended));
                    Require(clicks == 2, "Releasing outside a command must cancel the click.");
                    Rect originalSafeArea = (Rect)typeof(GreedLastRuntimeUi).GetField("lastSafeArea", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
                    int viewportWidth = (int)typeof(GreedLastRuntimeUi).GetField("lastSafeAreaScreenWidth", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
                    int viewportHeight = (int)typeof(GreedLastRuntimeUi).GetField("lastSafeAreaScreenHeight", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
                    foreach (string interruption in new[] { "OnApplicationFocus", "OnApplicationPause", "OnDisable", "HiddenButton", "DisabledButton", "InteractionLock", "CanvasGroupLock", "ViewportChange" })
                    {
                        void Interrupt(bool returning)
                        {
                            if (interruption == "ViewportChange")
                            {
                                Rect area = returning ? originalSafeArea
                                    : new Rect(originalSafeArea.x + 1f, originalSafeArea.y, originalSafeArea.width - 1f, originalSafeArea.height);
                                typeof(GreedLastRuntimeUi).GetMethod("ApplySafeAreaSample", BindingFlags.Instance | BindingFlags.NonPublic)
                                    .Invoke(ui, new object[] { area, viewportWidth, viewportHeight, false });
                                return;
                            }
                            if (interruption == "HiddenButton")
                            {
                                button.gameObject.SetActive(returning);
                                return;
                            }
                            if (interruption == "DisabledButton")
                            {
                                button.enabled = returning;
                                return;
                            }
                            if (interruption == "InteractionLock")
                            {
                                button.interactable = returning;
                                return;
                            }
                            if (interruption == "CanvasGroupLock")
                            {
                                testGroup.interactable = returning;
                                return;
                            }
                            string method = interruption == "OnDisable" && returning ? "OnEnable" : interruption;
                            object[] args = interruption == "OnDisable" ? null
                                : new object[] { interruption == "OnApplicationFocus" ? returning : !returning };
                            typeof(GreedLastRuntimeUi).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, args);
                        }
                        int before = clicks;
                        TouchFrame(input, module, TouchAt(84, screenPoint, TouchPhase.Began));
                        Interrupt(false);
                        Interrupt(true);
                        TouchFrame(input, module, TouchAt(84, screenPoint, TouchPhase.Stationary));
                        TouchFrame(input, module, TouchAt(84, screenPoint, TouchPhase.Ended));
                        Require(clicks == before, "An interrupted touch must not execute on return: " + interruption + " / " + button.name);
                        TouchFrame(input, module, TouchAt(84, screenPoint, TouchPhase.Began));
                        TouchFrame(input, module, TouchAt(84, screenPoint, TouchPhase.Ended));
                        Require(clicks == before + 1, "A fresh touch must work after an interrupted command.");

                        input.Touches = Array.Empty<Touch>();
                        input.MouseEnabled = input.MouseDown = input.MouseHeld = true;
                        input.MousePosition = screenPoint;
                        module.Process();
                        input.MouseDown = false;
                        Interrupt(false);
                        Interrupt(true);
                        input.MouseHeld = false;
                        input.MouseUp = true;
                        module.Process();
                        Require(clicks == before + 1, "An interrupted mouse press must not execute on return: " + interruption + " / " + button.name);
                        input.MouseUp = false;
                        input.MouseDown = input.MouseHeld = true;
                        module.Process();
                        input.MouseDown = input.MouseHeld = false;
                        input.MouseUp = true;
                        module.Process();
                        Require(clicks == before + 2, "A fresh mouse click must work after an interrupted command.");
                        input.MouseEnabled = input.MouseUp = false;
                    }

                    int unlockedClicks = clicks;
                    button.interactable = false;
                    TouchFrame(input, module, TouchAt(85, screenPoint, TouchPhase.Began));
                    button.interactable = true;
                    TouchFrame(input, module, TouchAt(85, screenPoint, TouchPhase.Ended));
                    Require(clicks == unlockedClicks, "A touch begun while locked must not become a click when unlocked.");
                    TouchFrame(input, module, TouchAt(85, screenPoint, TouchPhase.Began));
                    TouchFrame(input, module, TouchAt(85, screenPoint, TouchPhase.Ended));
                    Require(clicks == unlockedClicks + 1, "Unlocking must still allow a fresh command touch.");

                    input.Touches = Array.Empty<Touch>();
                    input.MouseEnabled = input.MouseDown = input.MouseHeld = true;
                    input.MousePosition = screenPoint;
                    button.interactable = false;
                    module.Process();
                    button.interactable = true;
                    input.MouseDown = input.MouseHeld = false;
                    input.MouseUp = true;
                    module.Process();
                    Require(clicks == unlockedClicks + 1, "A mouse press begun while locked must not execute after unlocking.");
                    input.MouseEnabled = input.MouseUp = false;

                    int beforeMultiTouch = clicks;
                    TouchFrame(input, module, TouchAt(86, screenPoint, TouchPhase.Began), TouchAt(87, screenPoint, TouchPhase.Began));
                    button.interactable = false;
                    button.interactable = true;
                    TouchFrame(input, module, TouchAt(86, screenPoint, TouchPhase.Ended), TouchAt(87, screenPoint, TouchPhase.Stationary));
                    TouchFrame(input, module, TouchAt(87, screenPoint, TouchPhase.Ended));
                    Require(clicks == beforeMultiTouch, "Locking a command must cancel every held finger, not just the most recent one.");
                    TouchFrame(input, module, TouchAt(86, screenPoint, TouchPhase.Began));
                    TouchFrame(input, module, TouchAt(86, screenPoint, TouchPhase.Ended));
                    Require(clicks == beforeMultiTouch + 1, "A reused finger ID must work after a multi-touch cancellation.");
                    if (redrawWhileHeld != null)
                    {
                        int beforeRedraw = clicks;
                        TouchFrame(input, module, TouchAt(88, screenPoint, TouchPhase.Began));
                        for (int frame = 0; frame < 3; frame++) redrawWhileHeld();
                        TouchFrame(input, module, TouchAt(88, screenPoint, TouchPhase.Ended));
                        Require(clicks == beforeRedraw + 1, "A held touch must survive an unchanged run redraw: " + button.name);
                        input.Touches = Array.Empty<Touch>();
                        input.MouseEnabled = input.MouseDown = input.MouseHeld = true;
                        input.MousePosition = screenPoint;
                        module.Process();
                        for (int frame = 0; frame < 3; frame++) redrawWhileHeld();
                        input.MouseDown = input.MouseHeld = false;
                        input.MouseUp = true;
                        module.Process();
                        Require(clicks == beforeRedraw + 2, "A held mouse click must survive an unchanged run redraw: " + button.name);
                        input.MouseEnabled = input.MouseUp = false;
                    }
                });
            }
            finally
            {
                button.onClick.RemoveListener(countClick);
                button.interactable = wasInteractable;
                if (addedGroup) UnityEngine.Object.DestroyImmediate(testGroup);
                else testGroup.interactable = groupWasInteractable;
            }
        }

        internal static bool VerifyLaneInput(GreedLastRuntimeUi ui, EventSystem system, Button button,
            Vector2 screenPoint, int laneIndex, Button alternateSurface = null)
        {
            bool usedAlternate = false;
            if (alternateSurface != null)
            {
                var visibleLanes = (Button[])typeof(GreedLastRuntimeUi).GetField("laneButtons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
                var expandedLanes = (Button[])typeof(GreedLastRuntimeUi).GetField("laneHitButtons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
                Require(laneIndex >= 0 && laneIndex < visibleLanes.Length && button == expandedLanes[laneIndex]
                    && alternateSurface == visibleLanes[laneIndex],
                    "An expanded lane probe may accept only its own visible channel as an alternate surface.");
            }
            FieldInfo callback = typeof(GreedLastRuntimeUi).GetField("runChannelRequested", BindingFlags.Instance | BindingFlags.NonPublic);
            object originalCallback = callback.GetValue(ui);
            var held = (bool[])typeof(GreedLastRuntimeUi).GetField("laneHoldActive", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
            int calls = 0;
            callback.SetValue(ui, (Action<GreedLastRunChannel>)(channel =>
            {
                Require((int)channel == laneIndex + 1, "A rendered lane must route to its own channel: " + button.name);
                calls++;
            }));
            try
            {
                WithPointerInput(system, button.gameObject, screenPoint, (input, module) =>
                {
                    Require(ui.IsPointerOverInteractiveUi(screenPoint), "Lane taps must not also initiate a rotation drag.");
                    var panel = ((GameObject)typeof(GreedLastRuntimeUi).GetField("rootPanel", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(ui)).GetComponent<RectTransform>();
                    Camera camera = button.GetComponentInParent<Canvas>().worldCamera;
                    Vector2 emptyCorridor = camera.WorldToScreenPoint(panel.TransformPoint(panel.rect.min
                        + Vector2.Scale(panel.rect.size, new Vector2(0.1f, 0.4f))));
                    Require(!ui.IsPointerOverInteractiveUi(emptyCorridor), "Empty corridor space must remain available for a test drag.");
                    input.MouseEnabled = true;
                    input.MousePosition = screenPoint;
                    input.MouseDown = input.MouseHeld = true;
                    module.Process();
                    Require(calls == 1 && held[laneIndex], "The real module must submit a lane on down: " + button.name);
                    input.MouseDown = false;
                    for (int frame = 0; frame < 3; frame++) module.Process();
                    Require(calls == 1 && held[laneIndex], "Holding a lane must not repeat its input.");
                    input.MouseHeld = false;
                    input.MouseUp = true;
                    module.Process();
                    Require(calls == 1 && !held[laneIndex], "Releasing a lane must clear its hold without a second input.");
                    input.MouseUp = false;
                    input.MousePosition = new Vector2(-1000f, -1000f);
                    module.Process();
                    input.MouseEnabled = false;

                    TouchFrame(input, module, TouchAt(80, screenPoint, TouchPhase.Began), TouchAt(81, screenPoint, TouchPhase.Began));
                    Require(calls == 3 && held[laneIndex], "Two fingers must remain distinct lane taps.");
                    TouchFrame(input, module, TouchAt(80, screenPoint, TouchPhase.Ended), TouchAt(81, screenPoint, TouchPhase.Stationary));
                    Require(calls == 3 && held[laneIndex], "One released finger must leave the other finger held.");
                    TouchFrame(input, module, TouchAt(81, screenPoint, TouchPhase.Canceled));
                    Require(calls == 3 && !held[laneIndex], "Canceling the last finger must clear the hold without another input.");
                    TouchFrame(input, module, TouchAt(80, screenPoint, TouchPhase.Began));
                    TouchFrame(input, module, TouchAt(80, screenPoint, TouchPhase.Ended));
                    Require(calls == 4 && !held[laneIndex], "Reusing a released finger ID must produce exactly one new tap.");
                    TouchFrame(input, module, TouchAt(82, screenPoint, TouchPhase.Canceled));
                    Require(calls == 4 && !held[laneIndex], "An orphan cancellation must not create a new lane press.");

                    // Mobile platforms can expose both a touch and an emulated mouse in the same frame.
                    input.MouseEnabled = input.MouseDown = input.MouseHeld = true;
                    input.MousePosition = screenPoint;
                    TouchFrame(input, module, TouchAt(80, screenPoint, TouchPhase.Began));
                    input.MouseDown = input.MouseHeld = false;
                    input.MouseUp = true;
                    TouchFrame(input, module, TouchAt(80, screenPoint, TouchPhase.Ended));
                    Require(calls == 5 && !held[laneIndex], "Touch and its emulated mouse must not produce duplicate rhythm input.");

                    input.MouseEnabled = input.MouseUp = false;
                    TouchFrame(input, module, TouchAt(83, screenPoint, TouchPhase.Began));
                    Require(calls == 6 && held[laneIndex], "Focus-loss checks need a held touch.");
                    typeof(GreedLastRuntimeUi).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(ui, new object[] { false });
                    Require(!held[laneIndex], "Focus loss must release the real module's lane highlight.");
                    typeof(GreedLastRuntimeUi).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(ui, new object[] { true });
                    TouchFrame(input, module, TouchAt(83, screenPoint, TouchPhase.Stationary));
                    TouchFrame(input, module, TouchAt(83, screenPoint, TouchPhase.Ended));
                    Require(calls == 6 && !held[laneIndex], "Returning with a stale held touch must not replay its press.");

                    var lanes = (Button[])typeof(GreedLastRuntimeUi).GetField("laneButtons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
                    int neighbor = (laneIndex + 1) % lanes.Length;
                    Vector2 neighborPoint = camera.WorldToScreenPoint(lanes[neighbor].transform.position);
                    TouchFrame(input, module, TouchAt(83, screenPoint, TouchPhase.Began));
                    TouchFrame(input, module, TouchAt(83, neighborPoint, TouchPhase.Moved));
                    Require(calls == 7 && !held[laneIndex] && !held[neighbor],
                        "Dragging across lanes must release the old hold without pressing the neighboring lane.");
                    TouchFrame(input, module, TouchAt(83, neighborPoint, TouchPhase.Ended));
                    Require(calls == 7, "Releasing over another lane must not submit a new rhythm input.");
                    TouchFrame(input, module, TouchAt(83, screenPoint, TouchPhase.Began));
                    TouchFrame(input, module, TouchAt(83, screenPoint, TouchPhase.Ended));
                    Require(calls == 8 && !held[laneIndex], "A fresh press after a cross-lane drag must work normally.");
                }, alternateSurface == null ? null : alternateSurface.gameObject,
                    hit => usedAlternate = alternateSurface != null && (hit == alternateSurface.gameObject
                        || hit.transform.IsChildOf(alternateSurface.transform)));
            }
            finally
            {
                callback.SetValue(ui, originalCallback);
            }
            return usedAlternate;
        }

        internal static void VerifyScrollInput(GreedLastRuntimeUi ui, EventSystem system, ScrollRect scroll,
            Vector2 screenPoint, float dragPixels)
        {
            Vector2 originalPosition = scroll.content.anchoredPosition;
            FieldInfo callback = typeof(GreedLastRuntimeUi).GetField("runChannelRequested", BindingFlags.Instance | BindingFlags.NonPublic);
            object originalCallback = callback.GetValue(ui);
            int laneCalls = 0;
            callback.SetValue(ui, (Action<GreedLastRunChannel>)(_ => laneCalls++));
            try
            {
                scroll.verticalNormalizedPosition = 0.5f;
                Canvas.ForceUpdateCanvases();
                scroll.Rebuild(CanvasUpdate.PostLayout);
                scroll.verticalScrollbar.value = 0.25f;
                Require(Mathf.Abs(scroll.verticalNormalizedPosition - 0.25f) < 0.01f,
                    "A newly overflowing menu must retain its native scrollbar-to-content connection before focus changes.");
                WithPointerInput(system, scroll.gameObject, screenPoint, (input, module) =>
                {
                    Require(ui.IsPointerOverInteractiveUi(screenPoint), "Scrolling text must not initiate a rotation drag.");
                    scroll.verticalNormalizedPosition = 1f;
                    input.MouseEnabled = true;
                    input.MousePosition = screenPoint;
                    input.ScrollDelta = Vector2.down;
                    module.Process();
                    input.ScrollDelta = Vector2.zero;
                    Require(scroll.content.anchoredPosition.y > 1f, "The actual input module must scroll long text with the wheel.");
                    scroll.StopMovement();
                    scroll.verticalNormalizedPosition = 1f;
                    input.MousePosition = new Vector2(-1000f, -1000f);
                    module.Process();
                    input.MouseEnabled = false;
                    TouchFrame(input, module, TouchAt(90, screenPoint, TouchPhase.Began));
                    TouchFrame(input, module, TouchAt(90, screenPoint + Vector2.up * dragPixels * 0.5f, TouchPhase.Moved));
                    TouchFrame(input, module, TouchAt(90, screenPoint + Vector2.up * dragPixels, TouchPhase.Moved));
                    Require(scroll.content.anchoredPosition.y > 1f, "A real-module touch drag must move the comparison text.");
                    TouchFrame(input, module, TouchAt(90, screenPoint + Vector2.up * dragPixels, TouchPhase.Canceled));
                    scroll.StopMovement();
                    float afterCancel = scroll.content.anchoredPosition.y;
                    TouchFrame(input, module, TouchAt(90, screenPoint, TouchPhase.Began));
                    TouchFrame(input, module, TouchAt(90, screenPoint, TouchPhase.Ended));
                    Require(Mathf.Abs(scroll.content.anchoredPosition.y - afterCancel) < 1f,
                        "A new tap after canceled scrolling must not reuse the previous drag offset.");
                    Require(laneCalls == 0, "Scrolling comparison or records must never select an underlying slot.");
                    foreach (bool appSignal in new[] { true, false })
                    {
                        MethodInfo lifecycle = typeof(GreedLastRuntimeUi).GetMethod(
                            appSignal ? "SetAppInputAvailable" : "OnApplicationFocus",
                            BindingFlags.Instance | BindingFlags.NonPublic);
                        scroll.StopMovement();
                        scroll.verticalNormalizedPosition = 1f;
                        Vector2 frozen = scroll.content.anchoredPosition;
                        try
                        {
                            lifecycle.Invoke(ui, new object[] { false });
                            Require(!ui.AcceptsPlayerInput, "Suspended scroll checks must first lock player input.");
                            input.Touches = Array.Empty<Touch>();
                            input.MouseEnabled = true;
                            input.MousePosition = screenPoint;
                            input.ScrollDelta = Vector2.down;
                            module.Process();
                            input.ScrollDelta = Vector2.zero;
                            Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) < 0.1f,
                                "The input module must not wheel-scroll a suspended menu: appSignal=" + appSignal);
                            input.MouseEnabled = false;
                            TouchFrame(input, module, TouchAt(91, screenPoint, TouchPhase.Began));
                            TouchFrame(input, module, TouchAt(91, screenPoint + Vector2.up * dragPixels, TouchPhase.Moved));
                            TouchFrame(input, module, TouchAt(91, screenPoint + Vector2.up * dragPixels, TouchPhase.Canceled));
                            Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) < 0.1f,
                                "A fresh background drag must not move suspended comparison/record text.");
                            TouchFrame(input, module, TouchAt(92, screenPoint, TouchPhase.Began));
                            lifecycle.Invoke(ui, new object[] { true });
                            TouchFrame(input, module, TouchAt(92, screenPoint, TouchPhase.Stationary));
                            TouchFrame(input, module, TouchAt(92, screenPoint + Vector2.up * dragPixels, TouchPhase.Moved));
                            TouchFrame(input, module, TouchAt(92, screenPoint + Vector2.up * dragPixels, TouchPhase.Ended));
                            Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) < 0.1f,
                                "A drag begun while suspended must not become a foreground scroll.");
                            input.Touches = Array.Empty<Touch>();
                            input.MouseEnabled = true;
                            input.MousePosition = screenPoint;
                            input.ScrollDelta = Vector2.down;
                            module.Process();
                            input.ScrollDelta = Vector2.zero;
                            Require(scroll.content.anchoredPosition.y > frozen.y + 1f,
                                "A genuinely new foreground wheel input must still scroll the same menu.");
                            scroll.StopMovement();
                            scroll.verticalNormalizedPosition = 1f;
                            input.MouseEnabled = false;
                            TouchFrame(input, module, TouchAt(93, screenPoint, TouchPhase.Began));
                            TouchFrame(input, module, TouchAt(93, screenPoint + Vector2.up * dragPixels * 0.25f, TouchPhase.Moved));
                            TouchFrame(input, module, TouchAt(93, screenPoint + Vector2.up * dragPixels * 0.5f, TouchPhase.Moved));
                            frozen = scroll.content.anchoredPosition;
                            Require(frozen.y > 1f, "The interrupted drag fixture must first perform a real foreground scroll.");
                            lifecycle.Invoke(ui, new object[] { false });
                            TouchFrame(input, module, TouchAt(93, screenPoint + Vector2.up * dragPixels, TouchPhase.Moved));
                            lifecycle.Invoke(ui, new object[] { true });
                            TouchFrame(input, module, TouchAt(93, screenPoint + Vector2.up * dragPixels * 1.25f, TouchPhase.Moved));
                            TouchFrame(input, module, TouchAt(93, screenPoint + Vector2.up * dragPixels * 1.25f, TouchPhase.Ended));
                            Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) < 0.1f,
                                "Suspending a held drag must preserve its position across background motion and foreground release.");
                        }
                        finally
                        {
                            lifecycle.Invoke(ui, new object[] { true });
                            scroll.StopMovement();
                        }
                    }
                    VerifyViewportDragCancellation(ui, scroll, input, module, screenPoint, dragPixels);
                    VerifyScrollbarInterruption(ui, scroll, input, module);
                    VerifyMixedScrollContacts(ui, scroll, input, module);
                    VerifyCrossControlScrollContacts(scroll, input, module);
                    scroll.enabled = false;
                    try
                    {
                        typeof(GreedLastRuntimeUi).GetMethod("SetAppInputAvailable", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(ui, new object[] { false });
                        typeof(GreedLastRuntimeUi).GetMethod("SetAppInputAvailable", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(ui, new object[] { true });
                        Require(!scroll.enabled, "Foreground recovery must not enable a scroll that was already disabled.");
                    }
                    finally { scroll.enabled = true; }
                });
            }
            finally
            {
                callback.SetValue(ui, originalCallback);
                scroll.StopMovement();
                scroll.content.anchoredPosition = originalPosition;
            }
        }

        private static void VerifyViewportDragCancellation(GreedLastRuntimeUi ui, ScrollRect scroll,
            GreedLastPrototypeKeyboardInput input, StandaloneInputModule module, Vector2 point, float dragPixels)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Rect area = (Rect)typeof(GreedLastRuntimeUi).GetField("lastSafeArea", flags).GetValue(ui);
            int width = (int)typeof(GreedLastRuntimeUi).GetField("lastSafeAreaScreenWidth", flags).GetValue(ui);
            int height = (int)typeof(GreedLastRuntimeUi).GetField("lastSafeAreaScreenHeight", flags).GetValue(ui);
            MethodInfo apply = typeof(GreedLastRuntimeUi).GetMethod("ApplySafeAreaSample", flags);
            void RestoreArea() => apply.Invoke(ui, new object[] { area, width, height, true });
            try
            {
                foreach (bool alreadyDragging in new[] { true, false })
                {
                    scroll.StopMovement();
                    scroll.verticalNormalizedPosition = 1f;
                    input.MouseEnabled = false;
                    TouchFrame(input, module, TouchAt(94, point, TouchPhase.Began));
                    if (alreadyDragging)
                    {
                        TouchFrame(input, module, TouchAt(94, point + Vector2.up * dragPixels * 0.25f, TouchPhase.Moved));
                        TouchFrame(input, module, TouchAt(94, point + Vector2.up * dragPixels * 0.5f, TouchPhase.Moved));
                        Require(scroll.content.anchoredPosition.y > 1f, "Viewport interruption must include an established drag.");
                    }
                    apply.Invoke(ui, new object[] { new Rect(area.x + 2f, area.y, area.width - 2f, area.height), width, height, false });
                    RestoreArea();
                    Vector2 frozen = scroll.content.anchoredPosition;
                    TouchFrame(input, module, TouchAt(94, point + Vector2.up * dragPixels * 0.5f, TouchPhase.Stationary));
                    TouchFrame(input, module, TouchAt(94, point + Vector2.up * dragPixels * 0.75f, TouchPhase.Moved));
                    TouchFrame(input, module, TouchAt(94, point + Vector2.up * dragPixels, TouchPhase.Moved));
                    TouchFrame(input, module, TouchAt(94, point + Vector2.up * dragPixels, TouchPhase.Ended));
                    Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) < 0.1f,
                        "A viewport change must cancel the old scroll gesture, including a press before the drag threshold: dragging=" + alreadyDragging);
                    scroll.StopMovement();
                    scroll.verticalNormalizedPosition = 0.5f;
                    Vector2 freshBefore = scroll.content.anchoredPosition;
                    TouchFrame(input, module, TouchAt(94, point, TouchPhase.Began));
                    TouchFrame(input, module, TouchAt(94, point + Vector2.up * dragPixels * 0.5f, TouchPhase.Moved));
                    TouchFrame(input, module, TouchAt(94, point + Vector2.up * dragPixels, TouchPhase.Moved));
                    Require(scroll.content.anchoredPosition.y > freshBefore.y + 0.1f,
                        "A fresh gesture after viewport cancellation must still scroll the same text.");
                    TouchFrame(input, module, TouchAt(94, point + Vector2.up * dragPixels, TouchPhase.Canceled));
                }
            }
            finally
            {
                RestoreArea();
                scroll.StopMovement();
            }
        }

        private static void VerifyScrollbarInterruption(GreedLastRuntimeUi ui, ScrollRect scroll,
            GreedLastPrototypeKeyboardInput input, StandaloneInputModule module)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Scrollbar bar = scroll.verticalScrollbar;
            Canvas canvas = scroll.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 HandlePoint() => RectTransformUtility.WorldToScreenPoint(camera,
                bar.handleRect.TransformPoint(bar.handleRect.rect.center));
            Rect area = (Rect)typeof(GreedLastRuntimeUi).GetField("lastSafeArea", flags).GetValue(ui);
            int width = (int)typeof(GreedLastRuntimeUi).GetField("lastSafeAreaScreenWidth", flags).GetValue(ui);
            int height = (int)typeof(GreedLastRuntimeUi).GetField("lastSafeAreaScreenHeight", flags).GetValue(ui);
            MethodInfo apply = typeof(GreedLastRuntimeUi).GetMethod("ApplySafeAreaSample", flags);
            foreach (string interruption in new[] { "focus", "app", "viewport", "command" })
            foreach (bool alreadyDragging in new[] { true, false })
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = bar.value = 0.5f;
                Canvas.ForceUpdateCanvases();
                scroll.Rebuild(CanvasUpdate.PostLayout);
                Vector2 point = HandlePoint();
                input.MouseEnabled = false;
                TouchFrame(input, module, TouchAt(95, point, TouchPhase.Began));
                float before = scroll.verticalNormalizedPosition;
                if (alreadyDragging)
                {
                    TouchFrame(input, module, TouchAt(95, point + Vector2.down * 12f, TouchPhase.Moved));
                    TouchFrame(input, module, TouchAt(95, point + Vector2.down * 24f, TouchPhase.Moved));
                    Require(Mathf.Abs(scroll.verticalNormalizedPosition - before) > 0.01f,
                        "Scrollbar interruption needs a real module drag of the handle.");
                }
                if (interruption == "viewport")
                {
                    apply.Invoke(ui, new object[] { new Rect(area.x + 2f, area.y, area.width - 2f, area.height), width, height, false });
                    apply.Invoke(ui, new object[] { area, width, height, true });
                }
                else if (interruption == "command")
                    typeof(GreedLastRuntimeUi).GetMethod("CancelPendingUiInput", flags).Invoke(ui, null);
                else
                {
                    MethodInfo signal = typeof(GreedLastRuntimeUi).GetMethod(
                        interruption == "focus" ? "OnApplicationFocus" : "SetAppInputAvailable", flags);
                    signal.Invoke(ui, new object[] { false });
                    signal.Invoke(ui, new object[] { true });
                }
                Vector2 frozen = scroll.content.anchoredPosition;
                TouchFrame(input, module, TouchAt(95, point + Vector2.down * 36f, TouchPhase.Moved));
                TouchFrame(input, module, TouchAt(95, point + Vector2.down * 48f, TouchPhase.Moved));
                TouchFrame(input, module, TouchAt(95, point + Vector2.down * 48f, TouchPhase.Ended));
                Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) < 0.1f,
                    "An interrupted scrollbar must reject its old gesture: " + interruption + ", dragging=" + alreadyDragging);
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = bar.value = 0.5f;
                Canvas.ForceUpdateCanvases();
                scroll.Rebuild(CanvasUpdate.PostLayout);
                point = HandlePoint();
                before = scroll.verticalNormalizedPosition;
                TouchFrame(input, module, TouchAt(95, point, TouchPhase.Began));
                TouchFrame(input, module, TouchAt(95, point + Vector2.down * 12f, TouchPhase.Moved));
                TouchFrame(input, module, TouchAt(95, point + Vector2.down * 24f, TouchPhase.Moved));
                TouchFrame(input, module, TouchAt(95, point + Vector2.down * 24f, TouchPhase.Canceled));
                Require(Mathf.Abs(scroll.verticalNormalizedPosition - before) > 0.01f,
                    "A fresh scrollbar press after interruption must recover: " + interruption
                        + ", dragging=" + alreadyDragging + ", before=" + before + ", after=" + scroll.verticalNormalizedPosition
                        + ", bar=" + bar.value + ", size=" + bar.size + ", content=" + scroll.content.rect.height
                        + ", view=" + scroll.viewport.rect.height + ", point=" + point + ", interactable=" + bar.IsInteractable());
            }
        }

        private static void VerifyMixedScrollContacts(GreedLastRuntimeUi ui, ScrollRect scroll,
            GreedLastPrototypeKeyboardInput input, StandaloneInputModule module)
        {
            Canvas canvas = scroll.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (bool handle in new[] { false, true })
            {
                Vector2 Point()
                {
                    RectTransform rect = handle ? scroll.verticalScrollbar.handleRect : scroll.viewport;
                    return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
                }
                Vector2 delta = (handle ? Vector2.down : Vector2.up)
                    * Mathf.Max(48f, (EventSystem.current.pixelDragThreshold + 1f) * 4f);
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = scroll.verticalScrollbar.value = 0.5f;
                Canvas.ForceUpdateCanvases();
                scroll.Rebuild(CanvasUpdate.PostLayout);
                Vector2 point = Point();
                input.MouseEnabled = false;
                TouchFrame(input, module, TouchAt(96, point, TouchPhase.Began));
                TouchFrame(input, module, TouchAt(96, point + delta * 0.25f, TouchPhase.Moved));
                TouchFrame(input, module, TouchAt(96, point + delta * 0.5f, TouchPhase.Moved));
                Vector2 frozen = scroll.content.anchoredPosition;
                Vector2 other = Point();
                TouchFrame(input, module, TouchAt(96, point + delta * 0.5f, TouchPhase.Stationary), TouchAt(97, other, TouchPhase.Began));
                TouchFrame(input, module, TouchAt(96, point + delta * 0.5f, TouchPhase.Stationary), TouchAt(97, other - delta, TouchPhase.Moved));
                Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) < 0.1f,
                    "A second finger must not take over an established menu gesture: handle=" + handle);
                TouchFrame(input, module, TouchAt(96, point, TouchPhase.Moved), TouchAt(97, other - delta, TouchPhase.Stationary));
                Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) > 0.1f,
                    "Rejecting a second finger must leave the first gesture working.");
                typeof(GreedLastRuntimeUi).GetMethod("CancelPendingUiInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                TouchFrame(input, module, TouchAt(96, point, TouchPhase.Stationary), TouchAt(97, other - delta, TouchPhase.Ended));
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = scroll.verticalScrollbar.value = 0.5f;
                Canvas.ForceUpdateCanvases();
                scroll.Rebuild(CanvasUpdate.PostLayout);
                other = Point();
                frozen = scroll.content.anchoredPosition;
                TouchFrame(input, module, TouchAt(96, point, TouchPhase.Stationary), TouchAt(98, other, TouchPhase.Began));
                TouchFrame(input, module, TouchAt(96, point, TouchPhase.Ended), TouchAt(98, other + delta * 0.25f, TouchPhase.Moved));
                TouchFrame(input, module, TouchAt(98, other + delta * 0.5f, TouchPhase.Moved));
                Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) > 0.1f,
                    "Releasing a canceled old finger must not end a newer menu gesture.");
                TouchFrame(input, module, TouchAt(98, other + delta * 0.5f, TouchPhase.Canceled));
                input.Touches = Array.Empty<Touch>();
                input.MouseEnabled = true;
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = scroll.verticalScrollbar.value = 0.5f;
                Canvas.ForceUpdateCanvases();
                scroll.Rebuild(CanvasUpdate.PostLayout);
                frozen = scroll.content.anchoredPosition;
                point = Point();
                input.MousePosition = point;
                input.MouseDown = input.MouseHeld = true;
                module.Process();
                input.MouseDown = false;
                input.MousePosition = point + delta * 0.5f;
                module.Process();
                input.MousePosition = point + delta;
                module.Process();
                Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) > 0.1f,
                    "A genuine mouse drag must still move the menu: handle=" + handle);
                typeof(GreedLastRuntimeUi).GetMethod("CancelPendingUiInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                frozen = scroll.content.anchoredPosition;
                input.MousePosition = point + delta * 1.25f;
                module.Process();
                input.MouseHeld = false;
                input.MouseUp = true;
                module.Process();
                input.MouseUp = false;
                Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) < 0.1f,
                    "A canceled mouse drag must not revive before or on release.");
            }
            input.MouseEnabled = false;
        }

        private static void VerifyCrossControlScrollContacts(ScrollRect scroll,
            GreedLastPrototypeKeyboardInput input, StandaloneInputModule module)
        {
            Canvas canvas = scroll.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 Point(bool handle)
            {
                RectTransform rect = handle ? scroll.verticalScrollbar.handleRect : scroll.viewport;
                return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            }
            foreach (bool firstHandle in new[] { false, true })
            foreach (bool alreadyDragging in new[] { false, true })
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = scroll.verticalScrollbar.value = 0.5f;
                Canvas.ForceUpdateCanvases();
                scroll.Rebuild(CanvasUpdate.PostLayout);
                input.MouseEnabled = false;
                Vector2 first = Point(firstHandle);
                Vector2 firstDelta = (firstHandle ? Vector2.down : Vector2.up)
                    * Mathf.Max(48f, (EventSystem.current.pixelDragThreshold + 1f) * 4f);
                TouchFrame(input, module, TouchAt(101, first, TouchPhase.Began));
                Vector2 held = first;
                if (alreadyDragging)
                {
                    TouchFrame(input, module, TouchAt(101, first + firstDelta * 0.25f, TouchPhase.Moved));
                    held = first + firstDelta * 0.5f;
                    TouchFrame(input, module, TouchAt(101, held, TouchPhase.Moved));
                }
                Vector2 second = Point(!firstHandle);
                Vector2 secondDelta = -firstDelta;
                Vector2 frozen = scroll.content.anchoredPosition;
                TouchFrame(input, module, TouchAt(101, held, TouchPhase.Stationary), TouchAt(102, second, TouchPhase.Began));
                TouchFrame(input, module, TouchAt(101, held, TouchPhase.Stationary),
                    TouchAt(102, second + secondDelta * 0.5f, TouchPhase.Moved));
                TouchFrame(input, module, TouchAt(101, held, TouchPhase.Stationary),
                    TouchAt(102, second + secondDelta, TouchPhase.Moved));
                Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) < 0.1f,
                    "A second finger on the other scroll control must not move the same content: firstHandle="
                        + firstHandle + ", dragging=" + alreadyDragging);
                TouchFrame(input, module, TouchAt(101, held, TouchPhase.Stationary),
                    TouchAt(102, second + secondDelta, TouchPhase.Ended));
                Vector2 destination = alreadyDragging ? first : first + firstDelta * 0.5f;
                TouchFrame(input, module, TouchAt(101, first + firstDelta * 0.25f, TouchPhase.Moved));
                TouchFrame(input, module, TouchAt(101, destination, TouchPhase.Moved));
                Require(Vector2.Distance(scroll.content.anchoredPosition, frozen) > 0.1f,
                    "Releasing the rejected cross-control finger must leave the original scroll gesture working.");
                TouchFrame(input, module, TouchAt(101, destination, TouchPhase.Canceled));
            }
        }

        private static Touch TouchAt(int id, Vector2 point, TouchPhase phase)
            => new Touch { fingerId = id, position = point, rawPosition = point, phase = phase, type = TouchType.Direct };

        private static void TouchFrame(GreedLastPrototypeKeyboardInput input, StandaloneInputModule module, params Touch[] touches)
        {
            input.Touches = touches;
            module.Process();
        }

        private static void WithPointerInput(EventSystem system, GameObject control, Vector2 screenPoint,
            Action<GreedLastPrototypeKeyboardInput, StandaloneInputModule> check,
            GameObject alternateControl = null, Action<GameObject> observedHit = null)
        {
            Require(!system.sendNavigationEvents, "Visual clicks must use the production keyboard-isolation setting.");
            StandaloneInputModule module = system.GetComponent<StandaloneInputModule>();
            Require(module != null, "Visual clicks require the standalone input module.");
            BaseInput originalInput = module.inputOverride;
            FieldInfo moduleSystem = typeof(BaseInputModule).GetField("m_EventSystem", BindingFlags.Instance | BindingFlags.NonPublic);
            object originalModuleSystem = moduleSystem.GetValue(module);
            FieldInfo focus = typeof(EventSystem).GetField("m_HasFocus", BindingFlags.Instance | BindingFlags.NonPublic);
            bool originalFocus = system.isFocused;
            GameObject originalSelection = system.currentSelectedGameObject;
            EventSystem originalCurrent = EventSystem.current;
            var systems = (System.Collections.Generic.List<EventSystem>)typeof(EventSystem)
                .GetField("m_EventSystems", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            bool addedSystem = !systems.Contains(system);
            GraphicRaycaster raycaster = control.GetComponentInParent<Canvas>().GetComponent<GraphicRaycaster>();
            var registeredRaycasters = RaycasterManager.GetRaycasters();
            bool addedRaycaster = !registeredRaycasters.Contains(raycaster);
            GameObject host = new GameObject("GreedLastPointerModuleProbe");
            GreedLastPrototypeKeyboardInput input = null;
            try
            {
                input = host.AddComponent<GreedLastPrototypeKeyboardInput>();
                moduleSystem.SetValue(module, system);
                module.inputOverride = input;
                focus.SetValue(system, true);
                // Preview scenes do not run the event system's normal registration lifecycle.
                if (addedSystem) systems.Add(system);
                EventSystem.current = system;
                if (addedRaycaster) registeredRaycasters.Add(raycaster);
                var hits = new System.Collections.Generic.List<RaycastResult>();
                system.RaycastAll(new PointerEventData(system) { position = screenPoint }, hits);
                Require(hits.Count > 0 && (hits[0].gameObject == control
                    || hits[0].gameObject.transform.IsChildOf(control.transform)
                    || (alternateControl != null && (hits[0].gameObject == alternateControl
                        || hits[0].gameObject.transform.IsChildOf(alternateControl.transform)))),
                    "The input module's raycast must reach its control: " + control.name
                    + " at " + screenPoint + " / first hit " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
                observedHit?.Invoke(hits[0].gameObject);
                check(input, module);
            }
            finally
            {
                // Release synthetic pointers outside the UI, including when an assertion fails.
                try
                {
                    if (input != null)
                    {
                        input.MouseEnabled = false;
                        Touch[] canceled = input.Touches;
                        for (int i = 0; i < canceled.Length; i++)
                            canceled[i] = TouchAt(canceled[i].fingerId, new Vector2(-1000f, -1000f), TouchPhase.Canceled);
                        module.Process();
                        input.Touches = Array.Empty<Touch>();
                        input.MouseEnabled = true;
                        input.MouseDown = input.MouseHeld = false;
                        input.MouseUp = true;
                        input.MousePosition = new Vector2(-1000f, -1000f);
                        input.ScrollDelta = Vector2.zero;
                        module.Process();
                    }
                }
                finally
                {
                    module.inputOverride = originalInput;
                    moduleSystem.SetValue(module, originalModuleSystem);
                    system.SetSelectedGameObject(originalSelection);
                    focus.SetValue(system, originalFocus);
                    if (addedSystem) systems.Remove(system);
                    if (originalCurrent != null) EventSystem.current = originalCurrent;
                    if (addedRaycaster) registeredRaycasters.Remove(raycaster);
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
