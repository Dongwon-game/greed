using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    internal static class GreedLastPrototypeHudCheck
    {
        internal static void Run()
        {
            var host = new GameObject("GreedHudClearanceCheck", typeof(RectTransform));
            try
            {
                var ui = host.AddComponent<GreedLastRuntimeUi>();
                RectTransform parent = host.GetComponent<RectTransform>();
                parent.sizeDelta = new Vector2(1200f, 650f);
                Text guide = Create<Text>(parent, "TimingGuide", new Vector2(760f, 64f));
                guide.text = "Timing";
                guide.color = Color.white;
                Image cue = Create<Image>(parent, "RunnerPreparationCue", new Vector2(132f, 16f));
                cue.color = Color.white;
                cue.raycastTarget = false;
                Set(ui, "timingGuideText", guide);
                Set(ui, "runnerPreparationCue", cue);
                MethodInfo keepClear = typeof(GreedLastRuntimeUi).GetMethod("KeepRunnerCuesAboveTimingGuide", BindingFlags.Instance | BindingFlags.NonPublic);
                void Apply() => keepClear.Invoke(ui, null);
                int cases = 0;
                foreach (float parentAngle in new[] { -8f, 0f, 8f })
                foreach (float cueAngle in new[] { -35f, 0f, 35f })
                foreach (float guideScale in new[] { 1f, 1.3f })
                foreach (float cueScale in new[] { 0.8f, 1f, 1.5f })
                {
                    parent.localEulerAngles = new Vector3(0f, 0f, parentAngle);
                    parent.localScale = new Vector3(0.7f, 1.1f, 1f);
                    guide.rectTransform.anchoredPosition = new Vector2(0f, -90f);
                    guide.rectTransform.localScale = Vector3.one * guideScale;
                    cue.rectTransform.anchoredPosition = new Vector2(30f, -95f);
                    cue.rectTransform.localEulerAngles = new Vector3(0f, 0f, cueAngle);
                    cue.rectTransform.localScale = new Vector3(cueScale, 1.2f, 1f);
                    Vector3 scale = cue.rectTransform.localScale;
                    Quaternion rotation = cue.rectTransform.localRotation;
                    Vector3 guidePosition = guide.rectTransform.localPosition;
                    Apply();
                    Bounds text = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, guide.rectTransform);
                    Bounds effect = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, cue.rectTransform);
                    Require(effect.min.y >= text.max.y + 7.99f, "Rotated/scaled cues must clear the pulsing timing text.");
                    Require(Mathf.Abs(cue.rectTransform.anchoredPosition.x - 30f) < 0.001f
                        && cue.rectTransform.localScale == scale && cue.rectTransform.localRotation == rotation
                        && guide.rectTransform.localPosition == guidePosition,
                        "Clearance may lift the effect but must preserve text, horizontal alignment and pose.");
                    Vector3 settled = cue.rectTransform.localPosition;
                    for (int repeat = 0; repeat < 10; repeat++) Apply();
                    Require(Vector3.Distance(settled, cue.rectTransform.localPosition) < 0.001f,
                        "Repeated layout checks must not make a cue drift upward.");
                    cases++;
                }

                void RequireUnmoved(string context)
                {
                    Vector3 before = cue.rectTransform.localPosition;
                    Apply();
                    Require(Vector3.Distance(before, cue.rectTransform.localPosition) < 0.001f, context);
                }
                cue.rectTransform.anchoredPosition = new Vector2(900f, -95f);
                RequireUnmoved("An effect outside the text width must keep its authored pose.");
                cue.rectTransform.anchoredPosition = new Vector2(0f, 200f);
                RequireUnmoved("A cue already above the guide must not move.");
                cue.rectTransform.anchoredPosition = new Vector2(0f, -95f);
                guide.text = string.Empty;
                RequireUnmoved("Empty timing text must not reserve a visible exclusion area.");
                guide.text = "Timing";
                guide.enabled = false;
                RequireUnmoved("Disabled timing text must not affect the runner.");
                guide.enabled = true;
                guide.gameObject.SetActive(false);
                RequireUnmoved("Hidden timing text must not affect the runner.");
                guide.gameObject.SetActive(true);
                guide.color = Color.clear;
                RequireUnmoved("Transparent timing text must not affect the runner.");
                guide.color = Color.white;
                cue.color = Color.clear;
                RequireUnmoved("Transparent cues must not be relocated.");
                cue.color = Color.white;
                cue.enabled = false;
                RequireUnmoved("Disabled cues must not be relocated.");
                cue.enabled = true;
                cue.gameObject.SetActive(false);
                RequireUnmoved("Hidden cues must not be relocated.");
                Set(ui, "timingGuideText", null);
                Apply();
                Debug.Log("Greed Last HUD clearance passed: " + cases + " rotated/scaled cases, repeated-update stability, fixed text/input alignment and hidden/empty guards. Isolated geometry fixture only.");
            }
            finally
            {
                GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static T Create<T>(Transform parent, string name, Vector2 size) where T : Graphic
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            T graphic = node.AddComponent<T>();
            graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            graphic.rectTransform.sizeDelta = size;
            return graphic;
        }

        private static void Set(GreedLastRuntimeUi ui, string name, object value)
            => typeof(GreedLastRuntimeUi).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, value);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
