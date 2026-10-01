using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeRotationCheck
    {
        [MenuItem("Greed Last/Run Rotation Sensor Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Sensor checks require Edit mode.");
            GameObject previousCamera = GameObject.Find("GreedLastCamera");
            GameObject previousEvents = GameObject.Find("GreedLastEventSystem");
            var host = new GameObject("GreedLastRotationSensorCheck");
            GameObject canvas = null;
            int accepted = 0;
            int samples = 0;
            try
            {
                var app = host.AddComponent<GreedLastApp>();
                Require(Read<GreedLastMockBackend>(app, "backend") == null,
                    "The sensor fixture must not boot the personal backend.");
                var ui = host.AddComponent<GreedLastRuntimeUi>();
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui, playerControls: true);
                canvas = Read<GameObject>(ui, "rootPanel").GetComponentInParent<Canvas>().gameObject;
                var state = new GreedLastStateMachine(new GreedLastRequestGate());
                Write(app, "stateMachine", state);
                Write(app, "runtimeUi", ui);
                state.StateChanged += ui.Render;
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                float clock = Time.realtimeSinceStartup;
                MethodInfo process = Method(app, "TryHandleRotationTiltSample");

                bool Sample(float raw, int orientation = 2, float dt = 1f / 60f)
                {
                    clock += dt;
                    samples++;
                    bool result = (bool)process.Invoke(app, new object[] { raw, orientation, clock, dt });
                    if (result) accepted++;
                    return result;
                }

                void FreshRotation(bool resetSensor = true)
                {
                    core = new GreedLastRunCore();
                    Write(app, "runCore", core);
                    core.SnapshotChanged += value =>
                    {
                        run = value;
                        Method(app, "HandleRunSnapshotChanged").Invoke(app, new object[] { value });
                    };
                    state.SetRunCoreTest();
                    core.Enter();
                    float start = Time.realtimeSinceStartup - 3f;
                    for (int cycle = 0; cycle < 4; cycle++)
                        core.JumpToDevShortcut(GreedLastDevShortcut.AvoidanceCycle, start);
                    core.Tick(Time.realtimeSinceStartup);
                    core.RequestNextPattern(Time.realtimeSinceStartup);
                    Require(run.ActivePattern && !run.CountdownActive && GreedLastRunCore.IsRotationPattern(run.Pattern),
                        "The sensor fixture needs an actual authored live rotation corridor.");
                    Method(app, "ResetRunLaneInputGuard").Invoke(app, null);
                    Write(app, "runEntryLaneInputBlockUntil", 0f);
                    if (resetSensor)
                    {
                        Method(app, "ResetRotationTiltSensorTracking").Invoke(app, new object[] { true });
                        Write(app, "lastRotationTiltInputAt", -10f);
                        clock = Time.realtimeSinceStartup;
                    }
                }

                foreach (int orientation in new[] { 2, 3 })
                {
                    foreach (float axis in new[] { -0.9f, 0.9f })
                    {
                        FreshRotation();
                        Require(!Sample(axis, orientation), "An already tilted first sample must require neutral.");
                        for (int frame = 0; frame < 12; frame++)
                            Require(!Sample(axis, orientation), "Holding the initial tilt must not judge a trap.");
                        for (int frame = 0; frame < 16; frame++) Sample(0f, orientation);
                        Require(Read<bool>(app, "rotationTiltReady"), "A real stable neutral must rearm either landscape grip.");
                        int before = accepted;
                        for (int frame = 0; frame < 20; frame++) Sample(axis, orientation);
                        Require(accepted == before + 1 && run.LastJudgedInputChannel
                            == (axis < 0f ? GreedLastRunChannel.Left : GreedLastRunChannel.Right),
                            "A deliberate neutral-to-hold movement must reach the actual app input channel exactly once.");
                        Require(run.SuccessCount + run.GoodCount + run.MissCount == 1,
                            "The actual core must resolve one judgement, not just a synthetic sensor callback.");
                    }
                }

                // Positive control: the old filtered-only gate crosses neutral even without a neutral raw sample.
                float filtered = 0.9f;
                bool crossed = false;
                for (int frame = 0; frame < 12; frame++)
                {
                    filtered = StaticFloat("BuildRotationTiltFilteredAxis", filtered, -0.9f, 1f / 60f, 14f, true);
                    crossed |= Mathf.Abs(filtered) <= 0.18f;
                }
                Require(crossed, "The fixture must reproduce filtered neutral during a raw direction flip.");
                FreshRotation();
                Sample(0f);
                Write(app, "rotationTiltFilteredAxis", 0.9f);
                Write(app, "rotationTiltReady", false);
                int beforeFlip = accepted;
                for (int frame = 0; frame < 24; frame++)
                {
                    Require(!Sample(-0.9f), "A raw direction flip is not a neutral release.");
                    Require(!Read<bool>(app, "rotationTiltReady"), "Filtered neutral must not rearm a still-tilted phone.");
                }
                Require(accepted == beforeFlip, "A direction reversal without raw neutral must remain one-shot.");
                for (int frame = 0; frame < 16; frame++) Sample(0f);
                Require(Read<bool>(app, "rotationTiltReady"), "Neutral must restore input after a blocked reversal.");
                int beforeRearm = accepted;
                for (int frame = 0; frame < 20; frame++) Sample(-0.9f);
                Require(accepted == beforeRearm + 1, "A fresh movement after real neutral must still work.");

                foreach (float gap in new[] { 0.16f, 0.5f, 5f })
                {
                    FreshRotation();
                    Sample(0f);
                    for (int frame = 0; frame < 4; frame++) Sample(-0.9f);
                    Require(Read<int>(app, "rotationTiltCandidateDirection") == -1,
                        "The gap fixture needs a genuinely pending hold candidate.");
                    int before = accepted;
                    Require(!Sample(-0.9f, dt: gap), "Unobserved wall time must not complete the old hold candidate.");
                    for (int frame = 0; frame < 12; frame++) Sample(-0.9f);
                    Require(accepted == before && !Read<bool>(app, "rotationTiltReady"),
                        "A stalled sample stream must require fresh neutral before any input.");
                }

                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    FreshRotation();
                    Sample(0f);
                    for (int frame = 0; frame < 4; frame++) Sample(-0.9f);
                    Require(!Sample(invalid) && !Read<bool>(app, "rotationTiltAxisInitialized")
                        && Read<int>(app, "rotationTiltCandidateDirection") == 0,
                        "Invalid sensor values must clear confidence and never reach judgement or cursor math.");
                    for (int frame = 0; frame < 12; frame++)
                        Require(!Sample(-0.9f), "An invalid sample must not revive the old hold.");
                }

                FreshRotation();
                Sample(0f);
                for (int frame = 0; frame < 4; frame++) Sample(-0.9f);
                Require(!Sample(0.9f, dt: 0.04f)
                    && Read<int>(app, "rotationTiltCandidateDirection") == 0,
                    "A raw reversal must cancel the old candidate even while the filter still points the old way.");

                foreach (int rate in new[] { 10, 30, 60, 120 })
                {
                    FreshRotation();
                    float dt = 1f / rate;
                    Sample(0f, dt: dt);
                    int before = accepted;
                    for (int frame = 0; frame < rate; frame++) Sample(-0.9f, dt: dt);
                    Require(accepted == before + 1 && run.LastJudgedInputChannel == GreedLastRunChannel.Left,
                        "Continuous observed hold must remain usable without frame-rate-dependent repeat input: " + rate);
                }

                FreshRotation();
                Sample(0f);
                for (int frame = 0; frame < 4; frame++) Sample(-0.9f);
                clock -= 0.5f;
                Require(!Sample(-0.9f) && !Read<bool>(app, "rotationTiltReady"),
                    "A backwards sample clock must discard stale hold confidence.");

                FreshRotation();
                Sample(0f);
                for (int frame = 0; frame < 4; frame++) Sample(-0.9f);
                Require(!Sample(-0.9f, orientation: 3) && !Read<bool>(app, "rotationTiltReady"),
                    "Changing landscape grip must clear the old hold without remapping compensated X twice.");

                foreach (string interruption in new[] { "pause", "focus", "suspend", "viewport", "preference", "choice" })
                {
                    FreshRotation();
                    Sample(0f);
                    for (int frame = 0; frame < 4; frame++) Sample(-0.9f);
                    Require(Read<int>(app, "rotationTiltCandidateDirection") != 0, "The interruption needs a pending candidate.");
                    if (interruption == "pause") core.TogglePause(Time.realtimeSinceStartup);
                    else if (interruption == "focus") Method(app, "OnApplicationFocus").Invoke(app, new object[] { false });
                    else if (interruption == "suspend") Method(app, "OnApplicationPause").Invoke(app, new object[] { true });
                    else if (interruption == "viewport") Method(app, "HandleViewportChanged").Invoke(app, null);
                    else if (interruption == "preference") Method(app, "HandleRotationTiltPreferenceChanged").Invoke(app, new object[] { true });
                    else core.JumpToDevShortcut(GreedLastDevShortcut.GiftChoice, Time.realtimeSinceStartup);
                    Require(Read<int>(app, "rotationTiltCandidateDirection") == 0
                        && !Read<bool>(app, "rotationTiltAxisInitialized"),
                        "Leaving the input window must discard the old hold and filter: " + interruption);
                    int before = accepted;
                    Sample(-0.9f);
                    Require(accepted == before, "The first post-interruption held sample must not submit: " + interruption);
                    Method(app, "OnApplicationPause").Invoke(app, new object[] { false });
                    FreshRotation(resetSensor: false);
                    for (int frame = 0; frame < 12; frame++)
                        Require(!Sample(-0.9f), "A newly live corridor must not revive the interrupted hold: " + interruption);
                    for (int frame = 0; frame < 16; frame++) Sample(0f);
                    Require(Read<bool>(app, "rotationTiltReady"),
                        "Real neutral must restore readiness instead of permanently locking sensor input: " + interruption);
                    before = accepted;
                    for (int frame = 0; frame < 20; frame++) Sample(-0.9f);
                    Require(accepted == before + 1 && run.SuccessCount + run.GoodCount + run.MissCount == 1,
                        "A fresh deliberate movement must reach the actual core once after recovery: " + interruption);
                }
                Debug.Log("Greed Last rotation sensor flow passed: " + samples + " synthetic samples, " + accepted
                    + " actual app submissions; both landscape grips, raw neutral hysteresis, observed hold continuity, invalid samples and six interruptions. No personal slots or settings changed. Hardware latency and physical orientation remain device checks.");
            }
            finally
            {
                GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                UnityEngine.Object.DestroyImmediate(host);
                if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                DestroyCreated("GreedLastCamera", previousCamera);
                DestroyCreated("GreedLastEventSystem", previousEvents);
            }
        }

        private static MethodInfo Method(object target, string name) => target.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static T Read<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Write(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static float StaticFloat(string method, params object[] args) => (float)typeof(GreedLastApp)
            .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static void DestroyCreated(string name, GameObject previous)
        {
            GameObject created = GameObject.Find(name);
            if (previous == null && created != null) UnityEngine.Object.DestroyImmediate(created);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
