using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeWallClockCheck
    {
        private static Check active;

        [MenuItem("Greed Last/Run 40 Second Wall Clock UI Check")]
        public static void Run()
        {
            Require(active == null && !EditorApplication.isPlayingOrWillChangePlaymode,
                "The wall-clock check requires Edit mode and no other wall-clock check.");
            GreedLastPrototypePlayModeCheck.RequireAsyncBatchLifetime();
            active = new Check();
            try { active.Begin(); }
            catch (Exception error) { active.Finish(error); }
        }

        [Serializable]
        private sealed class Report
        {
            public string startedUtc;
            public string completedUtc;
            public string unityVersion;
            public string archivedReport;
            public string scope = "Real editor wall-clock core progression; manual UI Update and automated input. Not Play mode, hardware input, audio latency, allocation, GPU or device FPS measurement.";
            public bool succeeded;
            public string error;
            public int updates;
            public int publications;
            public int judgements;
            public int pauses;
            public int resumes;
            public int visibleActionCueUpdates;
            public int compactHudChecks;
            public int detailedHudChecks;
            public int pausedHudChecks;
            public int countdownHudChecks;
            public bool freshPracticeHudReset;
            public float elapsedSeconds;
        }

        private sealed class Check
        {
            private readonly Report report = new Report();
            private GameObject host;
            private GameObject canvas;
            private GameObject previousCamera;
            private GameObject previousEvents;
            private GreedLastRuntimeUi ui;
            private GreedLastRunCore core;
            private GreedLastRunSnapshot run;
            private GreedLastRunSnapshot pausedAnchor;
            private Action updateUi;
            private Func<string> freshProfile;
            private Func<GreedLastRunSnapshot, string> freshHealth;
            private Func<GreedLastRunSnapshot, string> freshFocus;
            private Func<GreedLastRunSnapshot, string> freshCompactHud;
            private Func<GreedLastRunSnapshot, string> freshDetailedHud;
            private Func<GreedLastRunSnapshot, bool, bool, string> freshActionCue;
            private Func<bool> rotationTiltAvailable;
            private float startedAt;
            private float resumeAt = float.PositiveInfinity;
            private float nextPauseAt = 6f;
            private bool detailsShown;
            private bool detailsHidden;
            private bool finished;
            private string loggedError;

            internal void Begin()
            {
                report.startedUtc = DateTime.UtcNow.ToString("O");
                report.unityVersion = Application.unityVersion;
                Application.logMessageReceived += CollectErrors;
                previousCamera = GameObject.Find("GreedLastCamera");
                previousEvents = GameObject.Find("GreedLastEventSystem");
                host = new GameObject("GreedLastWallClockCheck");
                ui = host.AddComponent<GreedLastRuntimeUi>();
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui, playerControls: true);
                canvas = Read<GameObject>("rootPanel").GetComponentInParent<Canvas>().gameObject;
                Field("sfxVolume").SetValue(ui, 0f);
                Method("RefreshAudioPlaybackState").Invoke(ui, null);
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                    GreedLastScreenState.InfiniteRun, "Wall-clock UI check", string.Empty, 0));
                updateUi = (Action)Method("Update").CreateDelegate(typeof(Action), ui);
                freshHealth = BindLabel("FormatHealthValue");
                freshFocus = BindLabel("FormatFocusStatus");
                freshCompactHud = BindLabel("BuildCompactLiveRunHud");
                freshDetailedHud = (Func<GreedLastRunSnapshot, string>)Method("BuildDetailedRunHud")
                    .CreateDelegate(typeof(Func<GreedLastRunSnapshot, string>), ui);
                freshActionCue = (Func<GreedLastRunSnapshot, bool, bool, string>)typeof(GreedLastRuntimeUi)
                    .GetMethod("BuildCompactActiveActionCueText", BindingFlags.Static | BindingFlags.NonPublic)
                    .CreateDelegate(typeof(Func<GreedLastRunSnapshot, bool, bool, string>));
                rotationTiltAvailable = (Func<bool>)typeof(GreedLastRuntimeUi)
                    .GetMethod("IsRuntimeRotationTiltAvailable", BindingFlags.Static | BindingFlags.NonPublic)
                    .CreateDelegate(typeof(Func<bool>));
                core = new GreedLastRunCore();
                freshProfile = (Func<string>)typeof(GreedLastRunCore)
                    .GetMethod("BuildTimingProfileText", BindingFlags.Instance | BindingFlags.NonPublic)
                    .CreateDelegate(typeof(Func<string>), core);
                core.SnapshotChanged += Observe;
                startedAt = Time.realtimeSinceStartup;
                core.EnterInfiniteRun();
                core.ToggleDevInvincible();
                EditorApplication.update += Tick;
            }

            private void Observe(GreedLastRunSnapshot snapshot)
            {
                run = snapshot;
                report.publications++;
                Require(snapshot.TimingProfile == freshProfile(),
                    "A real-time publication must retain the exact current timing profile.");
                ui.RenderRun(snapshot);
                Require(Read<Text>("healthGaugeText").text == "체력 " + freshHealth(snapshot)
                    && Read<Text>("focusGaugeText").text == freshFocus(snapshot),
                    "Real-time gauge graphics must preserve the current original formats.");
                VerifyCurrentHud();
            }

            private void VerifyCurrentHud()
            {
                if (run.Stopped) return;
                bool compact = ui.UsePlayerRunControls;
                string expected = compact ? freshCompactHud(run) : freshDetailedHud(run);
                Require(Read<Text>("runHudText").text == expected,
                    "Real-time run HUD must retain the current original text across pause, countdown and F11.");
                if (compact) report.compactHudChecks++;
                else report.detailedHudChecks++;
                if (run.Paused && !run.ResumeCountdownActive) report.pausedHudChecks++;
                if (run.CountdownActive) report.countdownHudChecks++;
            }

            private void Tick()
            {
                try
                {
                    Require(string.IsNullOrEmpty(loggedError), "The wall-clock check emitted Unity errors: " + loggedError);
                    float now = Time.realtimeSinceStartup;
                    report.elapsedSeconds = now - startedAt;
                    bool wasPaused = run.Paused;
                    core.Tick(now);
                    if (wasPaused && !run.Paused) report.resumes++;
                    Require(!run.Stopped, "The isolated real-time run must remain live until its explicit settlement.");
                    if (run.Paused)
                    {
                        Require(run.Score == pausedAnchor.Score && run.Health == pausedAnchor.Health
                            && run.Distance == pausedAnchor.Distance
                            && run.FocusStateActiveSeconds == pausedAnchor.FocusStateActiveSeconds,
                            "Pause and resume countdown must freeze score, health, distance and focus time.");
                        if (!run.ResumeCountdownActive && now >= resumeAt)
                        {
                            core.TogglePause(now);
                            resumeAt = float.PositiveInfinity;
                            Require(run.ResumeCountdownActive, "Wall-clock resume must use its actual countdown.");
                        }
                    }
                    else if (!run.CountdownActive && report.pauses < 3 && report.elapsedSeconds >= nextPauseAt)
                    {
                        pausedAnchor = run;
                        core.TogglePause(now);
                        Require(run.Paused && !run.ResumeCountdownActive, "Wall-clock pause must actually stop the run.");
                        report.pauses++;
                        nextPauseAt += 9f;
                        resumeAt = now + 0.5f;
                    }
                    if (!detailsShown && report.elapsedSeconds >= 10f)
                    {
                        ui.ToggleDevCleanView();
                        detailsShown = true;
                    }
                    if (!detailsHidden && report.elapsedSeconds >= 13f)
                    {
                        ui.ToggleDevCleanView();
                        detailsHidden = true;
                    }
                    if (run.ActivePattern && !run.Paused && !run.CountdownActive)
                    {
                        int count = run.SuccessCount + run.GoodCount + run.MissCount;
                        float delta = count % 4 == 1
                            ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                        if (count % 4 != 3
                            && run.ElapsedSeconds + core.InputTimingOffsetSeconds >= run.Pattern.CoreSeconds + delta)
                        {
                            ui.RecordLaneInput(run.Pattern.Channel);
                            core.HandleInput(run.Pattern.Channel, now);
                        }
                    }
                    updateUi();
                    VerifyCurrentHud();
                    Text actionCue = Read<Text>("activeActionCueText");
                    if (ui.UsePlayerRunControls && actionCue.isActiveAndEnabled && actionCue.color.a > 0f
                        && !string.IsNullOrEmpty(actionCue.text))
                    {
                        Require(actionCue.text == freshActionCue(run, true, rotationTiltAvailable()),
                            "A visible real-time basic action cue must match the current original text.");
                        report.visibleActionCueUpdates++;
                    }
                    Canvas.ForceUpdateCanvases();
                    report.updates++;
                    if (report.elapsedSeconds < 40f) return;
                    report.judgements = run.SuccessCount + run.GoodCount + run.MissCount;
                    Require(report.pauses == 3 && report.resumes == 3 && report.judgements >= 8
                        && report.publications > report.judgements && detailsShown && detailsHidden
                        && report.visibleActionCueUpdates > 0 && report.compactHudChecks > 0
                        && report.detailedHudChecks > 0 && report.pausedHudChecks > 0
                        && report.countdownHudChecks > 0,
                        "The real-time check must cover three completed pause/resume cycles, mixed judgements and both views.");
                    core.ForceStopInfiniteRunForTest();
                    Require(run.Stopped, "The real-time run must retain its terminal settlement.");
                    core.RestartPractice(now);
                    updateUi();
                    VerifyCurrentHud();
                    report.freshPracticeHudReset = run.PracticeMode && !run.Stopped && run.Score == 0
                        && run.Distance == 0f && run.Combo == 0 && run.SuccessCount == 0
                        && run.GoodCount == 0 && run.MissCount == 0;
                    Require(report.freshPracticeHudReset,
                        "A fresh practice HUD must not inherit the settled infinite run's totals.");
                    Finish(null);
                }
                catch (Exception error) { Finish(error); }
            }

            private void CollectErrors(string message, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    loggedError = message;
            }

            internal void Finish(Exception error)
            {
                if (finished) return;
                finished = true;
                EditorApplication.update -= Tick;
                if (core != null) core.SnapshotChanged -= Observe;
                try
                {
                    if (host != null)
                    {
                        GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                        UnityEngine.Object.DestroyImmediate(host);
                    }
                    if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                    DestroyCreated("GreedLastCamera", previousCamera);
                    DestroyCreated("GreedLastEventSystem", previousEvents);
                }
                catch (Exception cleanupError) { if (error == null) error = cleanupError; }
                Application.logMessageReceived -= CollectErrors;
                if (error == null && !string.IsNullOrEmpty(loggedError))
                    error = new InvalidOperationException("The wall-clock check emitted Unity errors: " + loggedError);
                report.completedUtc = DateTime.UtcNow.ToString("O");
                report.succeeded = error == null;
                report.error = error?.ToString() ?? string.Empty;
                try
                {
                    string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "WallClockChecks");
                    Directory.CreateDirectory(directory);
                    report.archivedReport = "report-wallclock-"
                        + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", System.Globalization.CultureInfo.InvariantCulture) + ".json";
                    string json = JsonUtility.ToJson(report, true);
                    File.WriteAllText(Path.Combine(directory, report.archivedReport), json);
                    File.WriteAllText(Path.Combine(directory, "latest-report.json"), json);
                }
                catch (Exception reportingError)
                {
                    report.succeeded = false;
                    report.error += "\nReport write failed: " + reportingError;
                }
                active = null;
                if (report.succeeded)
                    Debug.Log($"Greed wall-clock UI check passed: {report.elapsedSeconds:F2}s, {report.updates} manual UI updates,"
                        + $" {report.publications} real-time publications, {report.judgements} judgements, {report.visibleActionCueUpdates} visible action cue checks,"
                        + $" HUD compact/detailed={report.compactHudChecks}/{report.detailedHudChecks}, paused/countdown={report.pausedHudChecks}/{report.countdownHudChecks},"
                        + " three pause/resume cycles, F11 and fresh practice HUD reset. Personal settings and slots unchanged. " + report.scope);
                else Debug.LogError("Greed wall-clock UI check failed: " + report.error);
                if (Application.isBatchMode) EditorApplication.Exit(report.succeeded ? 0 : 1);
            }

            private static Func<GreedLastRunSnapshot, string> BindLabel(string name) =>
                (Func<GreedLastRunSnapshot, string>)typeof(GreedLastRuntimeUi)
                    .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
                    .CreateDelegate(typeof(Func<GreedLastRunSnapshot, string>));
            private static MethodInfo Method(string name) => typeof(GreedLastRuntimeUi)
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            private static FieldInfo Field(string name) => typeof(GreedLastRuntimeUi)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            private T Read<T>(string name) => (T)Field(name).GetValue(ui);
            private static void DestroyCreated(string name, GameObject previous)
            {
                GameObject created = GameObject.Find(name);
                if (previous == null && created != null) UnityEngine.Object.DestroyImmediate(created);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
