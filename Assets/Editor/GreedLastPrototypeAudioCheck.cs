using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GreedLast.Editor
{
    internal static class GreedLastPrototypeAudioCheck
    {
        internal static void ReleaseEditorAudio(GameObject host)
        {
            if (host == null) return;
            Require(!Application.isPlaying, "Explicit audio cleanup is for isolated Edit-mode fixtures only.");
            GreedLastRuntimeUi ui = host.GetComponent<GreedLastRuntimeUi>();
            if (ui != null) Call(ui, "OnDestroy");
        }

        public static void VerifyOwnedClipLifecycle()
        {
            Require(!Application.isPlaying, "Clip ownership checks require Edit mode.");
            AudioClip shared = AudioClip.Create("shared_audio_ownership_control", 64, 1, 44100, false);
            var allocated = new List<AudioClip>();
            GameObject host = null;
            try
            {
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    host = new GameObject("GreedLastAudioOwnershipCheck");
                    GreedLastRuntimeUi ui = host.AddComponent<GreedLastRuntimeUi>();
                    Call(ui, "CreateAudio");
                    var clips = new HashSet<AudioClip>();
                    foreach (FieldInfo field in typeof(GreedLastRuntimeUi).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                    {
                        if (field.FieldType == typeof(AudioClip) && field.GetValue(ui) is AudioClip clip) clips.Add(clip);
                        if (field.FieldType == typeof(AudioClip[]) && field.GetValue(ui) is AudioClip[] array)
                            foreach (AudioClip item in array) if (item != null) clips.Add(item);
                    }
                    Require(clips.Count == 30, "Every audio-only UI instance must create its 30 prototype tones.");
                    allocated.AddRange(clips);
                    AudioSource ownedSource = Read<AudioSource>(ui, "rhythmAudio");
                    ownedSource.clip = shared;
                    AudioSource externalSource = host.AddComponent<AudioSource>();
                    externalSource.playOnAwake = false;
                    externalSource.clip = shared;
                    externalSource.volume = 0.37f;
                    Call(ui, "OnDisable");
                    foreach (AudioClip clip in clips)
                        Require(clip != null, "A temporary UI disable must preserve its sounds for re-enable.");
                    Call(ui, "OnEnable");
                    ReleaseEditorAudio(host);
                    ReleaseEditorAudio(host);
                    Require(ownedSource == null && host.GetComponents<AudioSource>().Length == 1,
                        "UI-only teardown must release its generated source without requiring host destruction.");
                    Require(externalSource != null && externalSource.clip == shared
                        && Mathf.Approximately(externalSource.volume, 0.37f),
                        "UI-only teardown must leave an unrelated host source, clip and volume unchanged.");
                    UnityEngine.Object.DestroyImmediate(host);
                    host = null;
                    foreach (AudioClip clip in clips)
                        Require(clip == null, "Destroying the owning UI must release every generated audio clip.");
                    Require(shared != null, "Destroying one UI must not release an externally supplied clip.");
                }
                Debug.Log("Greed Last audio ownership passed: three explicit Edit-mode teardown cycles released 90 owned tones and three generated sources, preserved unrelated sources/clips/volume and tolerated duplicate cleanup before host destruction.");
            }
            finally
            {
                if (host != null)
                {
                    ReleaseEditorAudio(host);
                    UnityEngine.Object.DestroyImmediate(host);
                }
                foreach (AudioClip clip in allocated) if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (shared != null) UnityEngine.Object.DestroyImmediate(shared);
            }
        }

        public static void Run(GreedLastRuntimeUi ui)
        {
            Require(!Application.isPlaying, "Audio state checks require an isolated Edit-mode UI.");
            AudioSource source = Read<AudioSource>(ui, "rhythmAudio");
            Require(source != null, "The prototype must create its real audio source.");
            float originalVolume = Read<float>(ui, "sfxVolume");
            bool originalFocus = Read<bool>(ui, "sensoryHasFocus");
            bool originalPause = Read<bool>(ui, "sensoryAppPaused");
            bool originalEnabled = Read<bool>(ui, "sensoryComponentEnabled");
            bool originalHaptics = Read<bool>(ui, "hapticsEnabled");
            float originalHapticTime = Read<float>(ui, "lastHapticAt");
            try
            {
                // Change only transient state, never the user's sound or vibration preferences.
                Write(ui, "hapticsEnabled", true);
                Call(ui, "OnApplicationFocus", true);
                Call(ui, "OnApplicationPause", false);
                Call(ui, "OnEnable");
                SetTransientVolume(ui, 0.5f);
                Require(CanPlay(ui) && !source.mute && Mathf.Approximately(source.volume, 0.5f),
                    "The source must own the master gain once, including for already-playing voices.");
                Require(Mathf.Approximately(source.volume * 0.4f, 0.2f), "Authored 40% at master 50% must remain 20%, not double-scaled.");
                SetTransientVolume(ui, 0f);
                Require(source.mute && Mathf.Approximately(source.volume, 0f), "Zero volume must immediately mute the source.");
                Require(CanPlay(ui), "Sound mute must not disable the separate vibration preference.");
                Write(ui, "lastHapticAt", -10f);
                Call(ui, "PlayHaptic", 0.03f);
                Require(Read<float>(ui, "lastHapticAt") >= 0f, "Muted sound must still allow enabled foreground vibration.");
                Write(ui, "hapticsEnabled", false);
                Write(ui, "lastHapticAt", -10f);
                Call(ui, "PlayHaptic", 0.03f);
                Require(Read<float>(ui, "lastHapticAt") == -10f, "Vibration OFF must block feedback even in the foreground.");
                Write(ui, "hapticsEnabled", true);
                SetTransientVolume(ui, 0.65f);
                Require(!source.mute && Mathf.Approximately(source.volume, 0.65f), "Restoring sound must use the selected master value.");

                Call(ui, "OnApplicationFocus", false);
                RequireBlocked(ui, source, "Focus loss");
                Call(ui, "OnApplicationPause", true);
                Call(ui, "OnApplicationFocus", true);
                RequireBlocked(ui, source, "Focus returning before app resume");
                Call(ui, "OnApplicationPause", false);
                Require(CanPlay(ui) && !source.mute, "Audio may reopen only after both focus and app resume.");

                Call(ui, "OnApplicationFocus", false);
                Call(ui, "OnApplicationPause", true);
                Call(ui, "OnApplicationPause", false);
                RequireBlocked(ui, source, "App resume before focus returning");
                Call(ui, "OnApplicationFocus", true);
                Require(CanPlay(ui) && !source.mute, "The reverse callback order must also restore audio.");

                Call(ui, "OnDisable");
                SetTransientVolume(ui, 0.25f);
                RequireBlocked(ui, source, "Disabled UI with changed sound setting");
                Call(ui, "OnEnable");
                Require(CanPlay(ui) && !source.mute && Mathf.Approximately(source.volume, 0.25f),
                    "Re-enabling the UI must restore the selected volume, not a hardcoded maximum.");
                Call(ui, "OnApplicationPause", true);
                Call(ui, "OnDisable");
                Call(ui, "OnEnable");
                RequireBlocked(ui, source, "Re-enabling while the app remains paused");
                Call(ui, "OnApplicationPause", false);

                Write(ui, "lastHapticAt", -10f);
                Call(ui, "PlayHaptic", 0.03f);
                Require(Read<float>(ui, "lastHapticAt") >= 0f, "Foreground feedback must still pass its haptic gate.");
                VerifyBeatLifecycle(ui);
                VerifyOpeningCountdown(ui);
                VerifySynchronizedPulseCurve();
                VerifyInfiniteBeatTempos(ui);
                Debug.Log("Greed Last audio state checks passed: master gain once, immediate mute, independent haptics, focus/pause callback order, disable/re-enable and blocked background feedback. No device audio capture was performed.");
            }
            finally
            {
                Write(ui, "sfxVolume", originalVolume);
                Write(ui, "sensoryHasFocus", originalFocus);
                Write(ui, "sensoryAppPaused", originalPause);
                Write(ui, "sensoryComponentEnabled", originalEnabled);
                Write(ui, "hapticsEnabled", originalHaptics);
                Write(ui, "lastHapticAt", originalHapticTime);
                Call(ui, "RefreshAudioPlaybackState");
            }
        }

        private static void VerifyBeatLifecycle(GreedLastRuntimeUi ui)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            core.Enter();
            float clock = Time.realtimeSinceStartup;
            core.RestartPractice(clock);
            core.Tick(clock += 2f);
            core.Tick(clock += 1f);
            Require(run.ActivePattern, "Beat checks need an active practice trap.");
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.RunCoreTest, "Audio check", string.Empty, 0));
            ui.RenderRun(run);
            Write(ui, "lastCountdownBeatActive", false);
            Write(ui, "nextBeatSoundAt", Time.unscaledTime - 1f);
            int before = Read<int>(ui, "rhythmBeatIndex");
            Call(ui, "Update");
            Require(Read<int>(ui, "rhythmBeatIndex") == before + 1, "A live UI update must emit one due beat, not a catch-up burst.");
            Require(Mathf.Approximately(Pulse(ui), 1f), "The visual pulse must peak on the same UI frame as its beat event.");
            Write(ui, "beatPhase", 100.75f);
            Require(Mathf.Approximately(Pulse(ui), 1f), "Ambient movement phase must not shift a gameplay beat.");
            Write(ui, "rhythmPulseStartedAt", Time.unscaledTime - 1f);
            Write(ui, "nextBeatSoundAt", Time.unscaledTime + 100f);
            Write(ui, "downbeatPulseUntil", 0f);
            Call(ui, "Update");
            float restingScale = Read<UnityEngine.UI.Image>(ui, "beatMarker").rectTransform.localScale.x;
            Require(Pulse(ui) == 0f, "An expired beat must not generate a periodic phantom flash.");
            float volume = Read<float>(ui, "sfxVolume");
            SetTransientVolume(ui, 0f);
            Write(ui, "nextBeatSoundAt", Time.unscaledTime - 1f);
            Call(ui, "Update");
            Require(Mathf.Approximately(Pulse(ui), 1f)
                && Read<UnityEngine.UI.Image>(ui, "beatMarker").rectTransform.localScale.x > restingScale,
                "Muted sound must preserve the rendered beat pulse in the very same frame.");
            SetTransientVolume(ui, volume);

            core.TogglePause(clock += 0.1f);
            ui.RenderRun(run);
            Require(Pulse(ui) == 0f, "Opening a pause menu must immediately clear its visible beat pulse.");
            before = Read<int>(ui, "rhythmBeatIndex");
            Write(ui, "nextBeatSoundAt", Time.unscaledTime - 10f);
            Call(ui, "Update");
            Call(ui, "Update");
            Require(Read<int>(ui, "rhythmBeatIndex") == before, "Paused UI updates must not emit gameplay beats.");
            core.TogglePause(clock += 0.1f);
            ui.RenderRun(run);
            Write(ui, "choiceConfirmationCountdownBeatNotBefore", 0f);
            Call(ui, "Update");
            Require(Read<int>(ui, "rhythmBeatIndex") == 1, "A resume countdown must start at its first beat.");
            Require(Mathf.Approximately(Pulse(ui), 1f), "Resume sound and visual pulse must restart together.");

            // Backgrounding can prevent any UI Update between interrupting and restarting a countdown.
            Write(ui, "rhythmBeatIndex", 3);
            Write(ui, "nextBeatSoundAt", Time.unscaledTime + 100f);
            core.PauseForAppBackground(clock += 0.1f);
            ui.RenderRun(run);
            Require(run.Paused && !run.ResumeCountdownActive, "The interrupted countdown must be a stable pause.");
            core.TogglePause(clock += 0.1f);
            ui.RenderRun(run);
            Call(ui, "Update");
            Require(Read<int>(ui, "rhythmBeatIndex") == 1,
                "A restarted countdown must discard the previous beat schedule even without an intervening UI frame.");

            core.Tick(clock += 2f);
            ui.RenderRun(run);
            Require(!run.Paused && !run.CountdownActive && run.ActivePattern, "GO must restore the active trap.");
            before = Read<int>(ui, "rhythmBeatIndex");
            Call(ui, "Update");
            Require(Read<int>(ui, "rhythmBeatIndex") == before + 1, "GO must emit its release beat exactly once.");
            Call(ui, "Update");
            Require(Read<int>(ui, "rhythmBeatIndex") == before + 1, "The next frame must wait for the following beat interval.");

            core.TogglePause(clock += 0.1f);
            ui.RenderRun(run);
            core.TogglePause(clock += 0.1f);
            ui.RenderRun(run);
            Call(ui, "Update");
            Require(Read<bool>(ui, "lastCountdownBeatActive"), "A second resume must establish its countdown state.");
            Write(ui, "nextBeatSoundAt", Time.unscaledTime + 100f);
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.LobbyReady, "Lobby", string.Empty, 0));
            // A new screen can replace the countdown before the old screen gets another Update.
            core.RestartPractice(clock += 0.1f);
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.RunCoreTest, "Practice", string.Empty, 0));
            ui.RenderRun(run);
            Write(ui, "choiceConfirmationCountdownBeatNotBefore", 0f);
            Call(ui, "Update");
            Require(Read<int>(ui, "rhythmBeatIndex") == 1 && !Read<bool>(ui, "lastCountdownWasResume"),
                "A fresh run reached without an intervening lobby Update must begin its own first beat.");

            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.LobbyReady, "Lobby", string.Empty, 0));
            before = Read<int>(ui, "rhythmBeatIndex");
            Write(ui, "nextBeatSoundAt", Time.unscaledTime - 10f);
            Call(ui, "Update");
            Require(Read<int>(ui, "rhythmBeatIndex") == before, "Lobby updates must not replay a due gameplay beat.");
            Require(Pulse(ui) == 0f, "The lobby must not retain a gameplay pulse.");
            Debug.Log("Greed Last beat lifecycle passed: live tick -> silent pause -> resume -> interrupted countdown without UI frames -> fresh first beat -> single GO -> immediate new run -> lobby isolation.");
        }

        private static void VerifyOpeningCountdown(GreedLastRuntimeUi ui)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            core.RestartPractice(clock);
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.RunCoreTest, "Practice", string.Empty, 0));
            ui.RenderRun(run);
            Call(ui, "Update");
            Require(Read<int>(ui, "rhythmBeatIndex") == 1, "Opening countdown must begin with one beat.");

            core.PauseForAppBackground(clock += 0.2f);
            ui.RenderRun(run);
            Call(ui, "OnApplicationFocus", false);
            Call(ui, "OnApplicationPause", true);
            float remaining = run.StartCountdownRemainingSeconds;
            float distance = run.Distance;
            core.Tick(clock += 3600f);
            ui.RenderRun(run);
            Call(ui, "Update");
            Require(run.Paused && Mathf.Approximately(run.StartCountdownRemainingSeconds, remaining)
                && Mathf.Approximately(run.Distance, distance) && !run.ActivePattern,
                "Long background time must not consume the opening countdown or spawn a trap.");
            Require(Read<int>(ui, "rhythmBeatIndex") == 0, "The backgrounded opening must remain silent without accumulating beats.");

            Call(ui, "OnApplicationFocus", true);
            Require(!CanPlay(ui), "Focus alone must not reopen sensory feedback while the app is paused.");
            Call(ui, "OnApplicationPause", false);
            core.TogglePause(clock += 0.1f);
            ui.RenderRun(run);
            Call(ui, "Update");
            Require(run.ResumeCountdownActive && Read<int>(ui, "rhythmBeatIndex") == 1,
                "Returning to the opening must start a fresh resume beat without catch-up audio.");
            core.Tick(clock += 1.5f);
            ui.RenderRun(run);
            Require(!run.Paused && run.StartCountdownActive && !run.ActivePattern,
                "Completing resume must preserve the unfinished opening, not release a trap early.");
            int before = Read<int>(ui, "rhythmBeatIndex");
            Write(ui, "nextBeatSoundAt", Time.unscaledTime + 100f);
            Call(ui, "Update");
            Require(Read<int>(ui, "rhythmBeatIndex") == before, "The unfinished opening must not emit a premature GO cue.");
            core.Tick(clock += 2f);
            ui.RenderRun(run);
            Require(!run.CountdownActive && !run.ActivePattern && run.AutoFlow,
                "Opening completion must leave the pre-trap buffer intact.");
            Call(ui, "Update");
            Require(Read<int>(ui, "rhythmBeatIndex") == before + 1, "Actual opening completion must emit one GO cue.");
            Call(ui, "Update");
            Require(Read<int>(ui, "rhythmBeatIndex") == before + 1, "GO must not burst through missed background beats.");
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.LobbyReady, "Lobby", string.Empty, 0));
            Debug.Log("Greed Last opening audio lifecycle passed: first count -> simulated hour in background -> fresh resume -> preserved opening -> single GO and pre-trap buffer. No real waiting or device audio capture.");
        }

        private static float Pulse(GreedLastRuntimeUi ui) => (float)typeof(GreedLastRuntimeUi)
            .GetMethod("GetGameplayBeatPulse", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);

        private static void VerifyInfiniteBeatTempos(GreedLastRuntimeUi ui)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            core.EnterInfiniteRun();
            core.Tick(clock + 3f);
            core.Tick(clock + 4f);
            Require(run.InfiniteMode && run.ActivePattern && !run.CountdownActive && !run.Stopped,
                "Tempo checks must enter a live infinite run through the real core.");
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.InfiniteRun, "Tempo check", string.Empty, 0));
            for (int threat = 1; threat <= 5; threat++)
            {
                if (threat > 1) core.RaiseInfiniteThreatForTest();
                Require(run.InfiniteThreatLevel == threat, "The tempo fixture must visit every threat exactly once.");
                ui.RenderRun(run);
                Write(ui, "lastCountdownBeatActive", false);
                Write(ui, "nextBeatSoundAt", Time.unscaledTime - 10f);
                int before = Read<int>(ui, "rhythmBeatIndex");
                Call(ui, "Update");
                float expectedInterval = Mathf.Lerp(0.5f, 0.36f, (threat - 1) / 4f);
                Require(Read<int>(ui, "rhythmBeatIndex") == before + 1 && Pulse(ui) == 1f,
                    "Each threat must issue one sound event and one simultaneous visual peak.");
                Require(Mathf.Approximately(Read<float>(ui, "rhythmPulseInterval"), expectedInterval)
                    && Mathf.Abs(Read<float>(ui, "nextBeatSoundAt") - Time.unscaledTime - expectedInterval) < 0.002f,
                    "The visual envelope and next sound must use the same actual threat tempo.");
                Call(ui, "Update");
                Require(Read<int>(ui, "rhythmBeatIndex") == before + 1,
                    "A slow frame must not replay the skipped beats after changing threat.");
            }
            core.ForceStopInfiniteRunForTest();
            ui.RenderRun(run);
            Require(Pulse(ui) == 0f, "An infinite result must hide its last running beat immediately.");
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.LobbyReady, "Lobby", string.Empty, 0));
            Debug.Log("Greed Last infinite tempo checks passed: five real-core threat levels, shared sound/visual intervals, one event after a late frame and stopped-result isolation.");
        }

        private static void VerifySynchronizedPulseCurve()
        {
            MethodInfo curve = typeof(GreedLastRuntimeUi).GetMethod("BuildSynchronizedBeatPulse", BindingFlags.Static | BindingFlags.NonPublic);
            float At(float elapsed, float interval) => (float)curve.Invoke(null, new object[] { elapsed, interval });
            foreach (float interval in new[] { 0.5f, 0.465f, 0.43f, 0.395f, 0.36f })
            {
                Require(At(0f, interval) == 1f && At(0.08f, interval) > 0f && At(0.08f, interval) < 1f,
                    "Every normal/infinite tempo must peak on the event and decay smoothly.");
                Require(At(interval, interval) == 0f && At(interval * 4f, interval) == 0f,
                    "Visual beats must not repeat without a new audio-schedule event.");
            }
            Require(At(-0.1f, 0.5f) == 0f && At(0f, 0f) == 0f, "Future or missing beats must not light the HUD.");
            Debug.Log("Greed Last synchronized beat visuals passed: shared event peak, same-frame marker update, independent ambient motion, muted visuals, no phantom pulses, pause/resume reset and five tempos.");
        }

        private static void RequireBlocked(GreedLastRuntimeUi ui, AudioSource source, string context)
        {
            Require(!CanPlay(ui) && source.mute, context + " must keep sound and vibration blocked.");
            Write(ui, "lastHapticAt", -10f);
            Call(ui, "PlayHaptic", 0.03f);
            Require(Read<float>(ui, "lastHapticAt") == -10f, context + " must not enqueue a haptic.");
        }

        private static void SetTransientVolume(GreedLastRuntimeUi ui, float value)
        {
            Write(ui, "sfxVolume", value);
            Call(ui, "RefreshAudioPlaybackState");
        }

        private static bool CanPlay(GreedLastRuntimeUi ui) => (bool)typeof(GreedLastRuntimeUi)
            .GetProperty("CanPlaySensoryFeedback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
        private static FieldInfo Field(string name) => typeof(GreedLastRuntimeUi).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static T Read<T>(GreedLastRuntimeUi ui, string name) => (T)Field(name).GetValue(ui);
        private static void Write(GreedLastRuntimeUi ui, string name, object value) => Field(name).SetValue(ui, value);
        private static void Call(GreedLastRuntimeUi ui, string name, params object[] args)
            => typeof(GreedLastRuntimeUi).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, args);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
