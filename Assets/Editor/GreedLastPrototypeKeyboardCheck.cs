using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    internal static class GreedLastPrototypeKeyboardCheck
    {
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

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
