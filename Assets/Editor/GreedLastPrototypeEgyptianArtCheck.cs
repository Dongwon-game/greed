using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    internal static class GreedLastPrototypeEgyptianArtCheck
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Methods = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] Names = {
            "PrototypeEgyptianRunner", "PrototypeEgyptianRunnerJump",
            "PrototypeEgyptianRunnerSlide", "PrototypeEgyptianRunnerTwist"
        };

        [MenuItem("Greed Last/Run Egyptian Runner Art Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Egyptian art checks require Edit mode.");
            var textures = new Texture2D[4];
            for (int pose = 0; pose < Names.Length; pose++)
            {
                string path = "Assets/Resources/GreedLast/" + Names[pose] + ".png";
                textures[pose] = Resources.Load<Texture2D>("GreedLast/" + Names[pose]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(textures[pose] != null && importer != null && !importer.isReadable
                    && !importer.mipmapEnabled && importer.alphaIsTransparency && importer.DoesSourceTextureHaveAlpha()
                    && importer.wrapMode == TextureWrapMode.Clamp && importer.npotScale == TextureImporterNPOTScale.None
                    && importer.maxTextureSize <= 1024, "Egyptian poses need compact transparent clamp imports: " + Names[pose]);
                VerifySourceBounds(path, pose);
            }
            Require(Resources.Load<Texture2D>("GreedLast/PrototypeEgyptianEntrance") != null,
                "The entrance must use the matching human character background.");

            int policies = 0;
            foreach (int response in new[] { -1, 0, 1, 2, 3, 4 })
            foreach (GreedLastJudgementResult result in Enum.GetValues(typeof(GreedLastJudgementResult)))
            foreach (float progress in new[] { -1f, 0f, 0.039f, 0.04f, 0.5f, 0.92f, 0.921f, 1f, float.NaN, float.PositiveInfinity })
            foreach (bool active in new[] { false, true })
            {
                int expected = active && (result == GreedLastJudgementResult.Success || result == GreedLastJudgementResult.Good)
                    && !float.IsNaN(progress) && progress >= 0.04f && progress <= 0.92f && response >= 1 && response <= 3 ? response : 0;
                Require((int)Call(null, "BuildPrototypeRunnerPoseIndex", response, result, progress, active) == expected,
                    "Only active, successful response frames may select an action illustration.");
                policies++;
            }

            GameObject previousCamera = GameObject.Find("GreedLastCamera");
            GameObject previousEvents = GameObject.Find("GreedLastEventSystem");
            var host = new GameObject("GreedEgyptianRunnerArtCheck");
            GameObject canvas = null;
            int transforms = 0;
            try
            {
                var ui = host.AddComponent<GreedLastRuntimeUi>();
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui, playerControls: true);
                var root = Read<GameObject>(ui, "runnerAvatarRoot").GetComponent<RectTransform>();
                var picture = Read<RawImage>(ui, "runnerIllustration");
                canvas = Read<RectTransform>(ui, "rootPanelRect").GetComponentInParent<Canvas>().gameObject;
                Require(picture != null && picture.texture == textures[0] && !picture.raycastTarget,
                    "The normal human sprite must be bound without changing the input surface.");
                Require(root.Find("RunnerLeftHorn") == null && root.Find("RunnerRightHorn") == null,
                    "The procedural fallback must not retain oni horns.");
                foreach (string field in new[] { "runnerLeftBand", "runnerRightBand" })
                    Require(Read<Image>(ui, field).rectTransform.sizeDelta.y <= 8f,
                        "Fallback headwear must be a flat human headband.");
                Vector2 size = picture.rectTransform.sizeDelta;
                Require(size == new Vector2(138f, 207f), "The illustration must preserve its fixed canvas size.");
                Type responseType = typeof(GreedLastRuntimeUi).GetNestedType("RunnerResponseType", BindingFlags.NonPublic);
                foreach (int pose in new[] { 1, 2, 3 })
                foreach (float direction in new[] { -1f, 1f })
                foreach (float progress in new[] { 0.08f, 0.5f, 0.88f })
                foreach (bool good in new[] { false, true })
                {
                    float arc = Mathf.Sin(progress * Mathf.PI) * (good ? 0.68f : 1f);
                    var transform = (Vector4)Call(null, "BuildRunnerResponsePoseTransform",
                        Enum.ToObject(responseType, pose), arc, direction);
                    root.localRotation = Quaternion.identity;
                    root.localScale = new Vector3(transform.z, transform.w, 1f);
                    root.anchoredPosition = new Vector2(transform.x, transform.y);
                    Call(ui, "ApplyPrototypeRunnerIllustrationPose", pose, direction, transform);
                    Require(picture.texture == textures[pose] && picture.rectTransform.sizeDelta == size
                        && picture.uvRect == new Rect(0f, 0f, 1f, 1f), "A pose must use a complete separate texture on the same canvas.");
                    Vector3 x = root.parent.InverseTransformVector(picture.rectTransform.TransformVector(Vector3.right));
                    Vector3 y = root.parent.InverseTransformVector(picture.rectTransform.TransformVector(Vector3.up));
                    Near(x.magnitude, 1f, "Painted width must not inherit procedural squash.");
                    Near(y.magnitude, 1f, "Painted height must not inherit procedural squash.");
                    Require(Mathf.Sign(x.x) == (pose == 3 && direction < 0f ? -1f : 1f),
                        "Only the twist pose mirrors with the existing response direction.");
                    Vector3 foot = root.parent.InverseTransformPoint(picture.rectTransform.TransformPoint(Vector3.zero));
                    Vector3 origin = root.parent.InverseTransformPoint(root.TransformPoint(Vector3.zero));
                    Near(foot.y - origin.y + transform.y, pose == 2 ? 0f : transform.y,
                        "Ducking must stay grounded while jump and sidestep retain their movement arc.");
                    transforms++;
                }
                foreach (int missing in new[] { 1, 2, 3 })
                {
                    string field = missing == 1 ? "runnerJumpTexture" : missing == 2 ? "runnerSlideTexture" : "runnerTwistTexture";
                    Write(ui, field, null);
                    try
                    {
                        Call(ui, "ApplyPrototypeRunnerIllustrationPose", missing, -1f, new Vector4(3f, -10f, 1.2f, 0.6f));
                        Require(picture.texture == textures[0] && picture.rectTransform.localScale == Vector3.one
                            && picture.rectTransform.anchoredPosition == new Vector2(4f, 0f),
                            "Missing optional action art must restore the normal human sprite and clear old compensation.");
                    }
                    finally { Write(ui, field, textures[missing]); }
                }
                root.localScale = Vector3.one;
                Call(ui, "ApplyPrototypeRunnerIllustrationPose", 3, -1f, new Vector4(0f, 0f, 1f, 1f));
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.LobbyReady, "Lobby", "", 0));
                Require(picture.texture == textures[0] && picture.rectTransform.localScale == Vector3.one,
                    "Returning to the lobby must clear the old action pose and mirror.");
            }
            finally
            {
                if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                RemoveCreated("GreedLastCamera", previousCamera);
                RemoveCreated("GreedLastEventSystem", previousEvents);
                GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                UnityEngine.Object.DestroyImmediate(host);
            }
            VerifyPublishedPoseBindings();
            Debug.Log("Greed Egyptian runner art passed: four source-alpha/import checks, " + policies
                + " pose policy boundaries, " + transforms + " painted-proportion/foot/mirror transforms, three missing-pose recoveries and lobby reset. No desktop capture, player preferences or hardware-feel assertion.");
        }

        private static void VerifyPublishedPoseBindings()
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            const BindingFlags methods = BindingFlags.Instance | BindingFlags.NonPublic;
            var previousCamera = GameObject.Find("GreedLastCamera");
            var previousEvents = GameObject.Find("GreedLastEventSystem");
            var host = new GameObject("GreedEgyptianPublishedPoseCheck");
            GameObject canvas = null;
            int outcomes = 0, pauses = 0, choices = 0, directions = 0;
            try
            {
                var ui = host.AddComponent<GreedLastRuntimeUi>();
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui, playerControls: true);
                var type = typeof(GreedLastRuntimeUi);
                var picture = (RawImage)type.GetField("runnerIllustration", fields).GetValue(ui);
                canvas = ((RectTransform)type.GetField("rootPanelRect", fields).GetValue(ui)).GetComponentInParent<Canvas>().gameObject;
                var normal = Resources.Load<Texture2D>("GreedLast/PrototypeEgyptianRunner");
                var update = type.GetMethod("UpdateRunnerAvatar", methods);
                foreach (var result in new[] { GreedLastJudgementResult.Success, GreedLastJudgementResult.Good, GreedLastJudgementResult.Miss })
                {
                    var core = new GreedLastRunCore();
                    GreedLastRunSnapshot run = default;
                    core.SnapshotChanged += value => run = value;
                    core.Enter();
                    float clock = 10f;
                    int count = result == GreedLastJudgementResult.Miss ? 1 : 5;
                    for (int action = 0; action < count; action++)
                    {
                        core.JumpToDevShortcut(GreedLastDevShortcut.AvoidanceCycle, clock);
                        core.Tick(clock += 2f);
                        core.Tick(clock += 1f);
                        if (!run.ActivePattern) throw new InvalidOperationException("Published pose checks need an active pattern.");
                        ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.RunCoreTest, "Run", "", 0));
                        ui.RenderRun(run);
                        float offset = result == GreedLastJudgementResult.Good
                            ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                        clock = clock - run.CoreDeltaSeconds - core.InputTimingOffsetSeconds + offset;
                        var channel = result == GreedLastJudgementResult.Miss
                            ? (run.Pattern.Channel == GreedLastRunChannel.Left ? GreedLastRunChannel.Right : GreedLastRunChannel.Left)
                            : run.Pattern.Channel;
                        core.HandleInput(channel, clock);
                        if (run.LastResult != result) throw new InvalidOperationException("Unexpected published pose judgement: " + run.LastResult);
                        ui.RenderRun(run);
                        // Edit-mode test sampling only: core outcomes use public inputs; only the illustration clock is positioned at mid-response.
                        type.GetField("runnerResponseStartedAt", fields).SetValue(ui, Time.unscaledTime - 1f);
                        type.GetField("runnerResponseUntil", fields).SetValue(ui, Time.unscaledTime + 1f);
                        update.Invoke(ui, null);
                        string suffix = action == 0 ? "Jump" : action == 1 ? "Slide" : "Twist";
                        bool live = !run.ChoiceActive && !run.Stopped && !run.CountdownActive && !run.Paused;
                        Texture2D expected = result == GreedLastJudgementResult.Miss || !live ? normal
                            : Resources.Load<Texture2D>("GreedLast/PrototypeEgyptianRunner" + suffix);
                        if (picture.texture != expected) throw new InvalidOperationException("Published outcome did not bind the expected action texture: " + action + "/" + result);
                        outcomes++;
                        if (!live)
                        {
                            if (!run.ChoiceActive) throw new InvalidOperationException("The shortcut's chapter end must be an actual choice gate.");
                            choices++;
                            clock += 4f;
                            continue;
                        }
                        if (Read<GreedLastRunChannel>(ui, "runnerResponseChannel") == GreedLastRunChannel.Center)
                        {
                            VerifyResponseDirectionStable(ui);
                            directions++;
                        }
                        core.TogglePause(clock + 0.1f);
                        ui.RenderRun(run);
                        update.Invoke(ui, null);
                        if (!run.Paused || picture.texture != normal || picture.rectTransform.localScale.x < 0f)
                            throw new InvalidOperationException("Pause must clear the action texture and mirror.");
                        VerifyBlockedRunnerAtRest(ui);
                        core.TogglePause(clock + 0.2f);
                        ui.RenderRun(run);
                        Require(run.ResumeCountdownActive, "The paused response must enter a real resume countdown.");
                        VerifyBlockedRunnerAtRest(ui);
                        pauses++;
                        clock += 4f;
                    }
                }
                foreach (var result in new[] { GreedLastJudgementResult.Success, GreedLastJudgementResult.Good })
                {
                    var core = new GreedLastRunCore();
                    GreedLastRunSnapshot run = default;
                    core.SnapshotChanged += value => run = value;
                    core.EnterInfiniteRun();
                    float clock = Time.realtimeSinceStartup;
                    bool foundTwist = false;
                    ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.InfiniteRun, "Infinite", "", 0));
                    for (int step = 0; step < 1000 && !foundTwist; step++)
                    {
                        core.Tick(clock += 0.1f);
                        if (!run.ActivePattern || run.CountdownActive || run.Paused || run.Stopped || run.ChoiceActive) continue;
                        bool twist = run.Pattern.AvoidanceType == GreedLastAvoidanceType.Twist;
                        ui.RenderRun(run);
                        float offset = result == GreedLastJudgementResult.Good
                            ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                        clock = clock - run.CoreDeltaSeconds - core.InputTimingOffsetSeconds + offset;
                        core.HandleInput(run.Pattern.Channel, clock);
                        if (run.LastResult != result) throw new InvalidOperationException("Unexpected infinite pose judgement.");
                        ui.RenderRun(run);
                        if (!twist) continue;
                        if (run.Stopped || run.ChoiceActive) throw new InvalidOperationException("Infinite twist needs an uninterrupted run.");
                        type.GetField("runnerResponseStartedAt", fields).SetValue(ui, Time.unscaledTime - 1f);
                        type.GetField("runnerResponseUntil", fields).SetValue(ui, Time.unscaledTime + 1f);
                        update.Invoke(ui, null);
                        if (picture.texture != Resources.Load<Texture2D>("GreedLast/PrototypeEgyptianRunnerTwist"))
                            throw new InvalidOperationException("The published live infinite twist must bind the twist texture.");
                        outcomes++;
                        core.TogglePause(clock + 0.1f);
                        ui.RenderRun(run);
                        update.Invoke(ui, null);
                        if (!run.Paused || picture.texture != normal || picture.rectTransform.localScale.x < 0f)
                            throw new InvalidOperationException("Infinite twist pause must restore the unmirrored normal texture.");
                        VerifyBlockedRunnerAtRest(ui);
                        core.TogglePause(clock + 0.2f);
                        ui.RenderRun(run);
                        Require(run.ResumeCountdownActive, "The infinite response must enter a real resume countdown.");
                        VerifyBlockedRunnerAtRest(ui);
                        pauses++;
                        foundTwist = true;
                    }
                    if (!foundTwist) throw new InvalidOperationException("The public infinite run did not reach a twist in the bounded test.");
                }
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.LobbyReady, "Lobby", "", 0));
                if (picture.texture != normal) throw new InvalidOperationException("Lobby must clear the last action image.");
                Require(directions > 0, "Published pose checks must include a real central-channel response.");
            }
            finally
            {
                if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                foreach (string name in new[] { "GreedLastCamera", "GreedLastEventSystem" })
                {
                    var previous = name == "GreedLastCamera" ? previousCamera : previousEvents;
                    if (previous == null) { var created = GameObject.Find(name); if (created != null) UnityEngine.Object.DestroyImmediate(created); }
                }
                GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                UnityEngine.Object.DestroyImmediate(host);
            }
            Debug.Log("Greed Egyptian published pose bindings passed: " + outcomes + " inspected public core outcomes, " + pauses
                + " pause/resume body-and-shadow resets, " + directions + " central-channel direction holds, " + choices
                + " actual choice gates and lobby reset. Includes live infinite exact/Good twists. Edit-mode UI sampling with a positioned private illustration clock, not engine frames, native input, screenshots or device feel.");
        }

        private static void VerifyResponseDirectionStable(GreedLastRuntimeUi ui)
        {
            var root = Read<GameObject>(ui, "runnerAvatarRoot").GetComponent<RectTransform>();
            var shadow = Read<Image>(ui, "runnerShadow").rectTransform;
            float phase = Read<float>(ui, "beatPhase");
            Write(ui, "runEntranceHandoffUntil", 0f);
            try
            {
                Write(ui, "beatPhase", 0.25f);
                Write(ui, "runnerResponseStartedAt", Time.unscaledTime - 1f);
                Write(ui, "runnerResponseUntil", Time.unscaledTime + 1f);
                Call(ui, "UpdateRunnerAvatar");
                float x = root.anchoredPosition.x;
                float shadowX = shadow.anchoredPosition.x;
                Quaternion rotation = root.localRotation;
                Write(ui, "beatPhase", 0.75f);
                Write(ui, "runnerResponseStartedAt", Time.unscaledTime - 1f);
                Write(ui, "runnerResponseUntil", Time.unscaledTime + 1f);
                Call(ui, "UpdateRunnerAvatar");
                Require(Mathf.Abs(x - root.anchoredPosition.x) < 0.01f
                    && Mathf.Abs(shadowX - shadow.anchoredPosition.x) < 0.01f
                    && Quaternion.Angle(rotation, root.localRotation) < 0.01f,
                    "Central-channel response must not reverse body or shadow direction when the beat phase changes.");
            }
            finally { Write(ui, "beatPhase", phase); }
        }

        private static void VerifyBlockedRunnerAtRest(GreedLastRuntimeUi ui)
        {
            var root = Read<GameObject>(ui, "runnerAvatarRoot").GetComponent<RectTransform>();
            var shadow = Read<Image>(ui, "runnerShadow").rectTransform;
            float until = Read<float>(ui, "runnerResponseUntil");
            // Exclude the separate entrance clock; compare only the old judgement's effect.
            Write(ui, "runEntranceHandoffUntil", 0f);
            Call(ui, "UpdateRunnerAvatar");
            Vector2 position = root.anchoredPosition;
            Vector3 scale = root.localScale;
            Quaternion rotation = root.localRotation;
            Vector2 shadowPosition = shadow.anchoredPosition;
            Vector3 shadowScale = shadow.localScale;
            try
            {
                Write(ui, "runnerResponseUntil", 0f);
                Call(ui, "UpdateRunnerAvatar");
                Require(Vector2.Distance(position, root.anchoredPosition) < 0.01f
                    && Vector3.Distance(scale, root.localScale) < 0.01f
                    && Quaternion.Angle(rotation, root.localRotation) < 0.01f
                    && Vector2.Distance(shadowPosition, shadow.anchoredPosition) < 0.01f
                    && Vector3.Distance(shadowScale, shadow.localScale) < 0.01f,
                    "Blocked runner must not retain the previous judgement's body or shadow motion.");
            }
            finally { Write(ui, "runnerResponseUntil", until); }
        }

        private static void VerifySourceBounds(string path, int pose)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Require(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)), "Source PNG must decode.");
                Require(texture.width == 1024 && texture.height == 1536, "All poses require the same full canvas.");
                Color32[] pixels = texture.GetPixels32();
                int minY = texture.height, maxY = -1, occupied = 0, edges = 0;
                for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a < 32) continue;
                    occupied++; minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    if (x < 2 || x >= texture.width - 2 || y < 2 || y >= texture.height - 2) edges++;
                }
                Require(occupied > 100000 && occupied < pixels.Length * 0.8f && edges == 0,
                    "Pose sources must be nonblank cutouts with transparent outer edges, not clipped sheets.");
                float pivot = (float)Call(null, "BuildPrototypeRunnerFootPivot", pose);
                Require(Mathf.Abs(pivot - (minY + 0.5f) / texture.height) <= 1f / texture.height,
                    "The frame pivot must match the source-alpha sole baseline.");
                if (pose == 2) Require(maxY < texture.height * 0.67f, "The duck must remain visibly lower than the standing canvas.");
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static object Call(object target, string name, params object[] args)
            => typeof(GreedLastRuntimeUi).GetMethod(name, Methods).Invoke(target, args);
        private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
        private static void Write(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
        private static void RemoveCreated(string name, GameObject previous)
        { if (previous == null) { GameObject created = GameObject.Find(name); if (created != null) UnityEngine.Object.DestroyImmediate(created); } }
        private static void Near(float actual, float expected, string message)
            => Require(Mathf.Abs(actual - expected) < 0.01f, message + " Actual=" + actual + ", expected=" + expected);
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
