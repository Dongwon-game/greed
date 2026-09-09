using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeVisualCheck
    {
        private const string MenuPath = "Greed Last/Capture Prototype Visual Checks";
        private static readonly Queue<Frame> Frames = new Queue<Frame>();
        private static Scene captureScene;
        private static GreedLastRuntimeUi ui;
        private static Canvas canvas;
        private static Camera camera;
        private static RenderTexture target;
        private static Frame currentFrame;
        private static double frameStartedAt;
        private static bool running;

        private sealed class Frame
        {
            public string Name;
            public int Width;
            public int Height;
            public float SafeInset;
            public Action Show;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play mode before capturing prototype frames.");
            }

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                throw new InvalidOperationException("Visual capture needs a graphics device; do not use -nographics.");
            }

            running = true;
            try
            {
                captureScene = EditorSceneManager.NewPreviewScene();
                camera = new GameObject("GreedVisualCheckCamera", typeof(Camera)).GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(11, 16, 18, 255);
                camera.cameraType = CameraType.Game;
                camera.scene = captureScene;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.enabled = false;
                ui = new GameObject("GreedVisualCheckUi").AddComponent<GreedLastRuntimeUi>();
                GameObject existingEventSystem = GameObject.Find("GreedLastEventSystem");
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui);
                canvas = Read<GameObject>("rootPanel").GetComponentInParent<Canvas>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, captureScene);
                SceneManager.MoveGameObjectToScene(ui.gameObject, captureScene);
                SceneManager.MoveGameObjectToScene(canvas.gameObject, captureScene);
                GameObject createdEventSystem = GameObject.Find("GreedLastEventSystem");
                if (existingEventSystem == null && createdEventSystem != null)
                {
                    SceneManager.MoveGameObjectToScene(createdEventSystem, captureScene);
                }
                canvas.GetComponent<CanvasScaler>().enabled = false;
                // World-space projection preserves the same 1920-unit layout in an isolated preview camera.
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                ui.ToggleDevCleanView();

                // The fixture owns only a transient core, never the user's saved profile.
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot latest = default;
                core.SnapshotChanged += snapshot => latest = snapshot;
                float now = Time.realtimeSinceStartup;
                core.Enter();
                GreedLastRunSnapshot choice = latest;
                core.RestartPractice(now - 4f);
                core.Tick(now - 1f);
                core.Tick(now);
                if (!latest.ActivePattern) throw new InvalidOperationException("Live visual fixture needs an active trap.");
                GreedLastRunSnapshot live = latest;
                core.TogglePause(now + 0.1f);
                GreedLastRunSnapshot paused = latest;
                core.TogglePause(now + 0.2f);
                GreedLastRunSnapshot resume = latest;
                Add("00-title", 1280, 720, () => ShowScreen(GreedLastScreenState.BootInit, "GREED LAST"));
                Add("01-lobby", 1280, 720, () => ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구"));
                Add("02-start-choice", 1280, 720, () => ShowRun(choice));
                Add("03-live-run", 1280, 720, () => ShowRun(live));
                Add("04-pause", 1280, 720, () => ShowRun(paused));
                Add("05-resume", 1280, 720, () => ShowRun(resume));
                Add("06-lobby-desktop", 1920, 1080, () => ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구"));
                Add("07-live-wide-safe", 2340, 1080, () => ShowRun(live), 96f);
                Add("08-pause-wide-safe", 2340, 1080, () => ShowRun(paused), 96f);
                Directory.CreateDirectory("Logs/VisualChecks");
                BeginNextFrame();
                EditorApplication.update += Tick;
                AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            }
            catch
            {
                Cleanup();
                throw;
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun() => !running && !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Add(string name, int width, int height, Action show, float safeInset = 0f)
        {
            Frames.Enqueue(new Frame { Name = name, Width = width, Height = height, Show = show, SafeInset = safeInset });
        }

        private static void ShowScreen(GreedLastScreenState state, string title)
        {
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(state, title, string.Empty, 0));
        }

        private static void ShowRun(GreedLastRunSnapshot snapshot)
        {
            ShowScreen(GreedLastScreenState.RunCoreTest, "피라미드 질주");
            ui.RenderRun(snapshot);
        }

        private static void BeginNextFrame()
        {
            currentFrame = Frames.Dequeue();
            ReleaseTarget();
            target = new RenderTexture(currentFrame.Width, currentFrame.Height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            canvas.scaleFactor = currentFrame.Height / 1920f;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f * currentFrame.Width / currentFrame.Height, 1920f);
            canvasRect.position = Vector3.zero;
            canvasRect.rotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * (10f / 1920f);
            currentFrame.Show();
            frameStartedAt = EditorApplication.timeSinceStartup;
        }

        private static void Tick()
        {
            try
            {
                typeof(GreedLastRuntimeUi).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                if (EditorApplication.timeSinceStartup - frameStartedAt < 0.5)
                {
                    return;
                }
                CaptureFrame();
                if (Frames.Count > 0)
                {
                    BeginNextFrame();
                    return;
                }
                Debug.Log("Greed Last visual checks captured: 9 nonblank frames in Logs/VisualChecks. These are isolated UI fixtures, not real-device input tests.");
                Cleanup();
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Cleanup();
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private static void CaptureFrame()
        {
            RectTransform root = Read<RectTransform>("rootPanelRect");
            float inset = currentFrame.SafeInset / currentFrame.Width;
            root.anchorMin = new Vector2(inset, 0);
            root.anchorMax = new Vector2(1f - inset, 1f);
            root.offsetMin = root.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>())
            {
                graphic.SetAllDirty();
                graphic.Rebuild(CanvasUpdate.PreRender);
            }
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(currentFrame.Width, currentFrame.Height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                Color32[] pixels = image.GetPixels32();
                int brightSamples = 0;
                int detailSamples = 0;
                Color32 first = pixels[0];
                for (int i = 0; i < pixels.Length; i += 97)
                {
                    Color32 pixel = pixels[i];
                    if (pixel.r + pixel.g + pixel.b > 150) brightSamples++;
                    if (Math.Abs(pixel.r - first.r) + Math.Abs(pixel.g - first.g) + Math.Abs(pixel.b - first.b) > 35) detailSamples++;
                }
                if (brightSamples < 30 || detailSamples < 100)
                {
                    File.WriteAllBytes("Logs/VisualChecks/" + currentFrame.Name + ".failed.png", image.EncodeToPNG());
                    throw new InvalidOperationException("Blank or incomplete visual capture: " + currentFrame.Name
                        + " / bright " + brightSamples + " / detail " + detailSamples
                        + " / canvas " + canvas.GetComponent<RectTransform>().rect
                        + " / root " + root.rect
                        + " / title vertices " + Read<Text>("titleText").cachedTextGenerator.vertexCount
                        + " / title culled " + Read<Text>("titleText").canvasRenderer.cull
                        + " / camera rect " + camera.pixelRect);
                }
                string path = "Logs/VisualChecks/" + currentFrame.Name + ".png";
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log("Visual frame: " + path + " / " + target.width + "x" + target.height);
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static T Read<T>(string name) where T : class
        {
            return typeof(GreedLastRuntimeUi).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui) as T;
        }

        private static void ReleaseTarget()
        {
            if (camera != null) camera.targetTexture = null;
            if (target == null) return;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            target = null;
        }

        private static void Cleanup()
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            ReleaseTarget();
            if (captureScene.IsValid()) EditorSceneManager.ClosePreviewScene(captureScene);
            if (ui != null) UnityEngine.Object.DestroyImmediate(ui.gameObject);
            if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            if (camera != null) UnityEngine.Object.DestroyImmediate(camera.gameObject);
            ui = null;
            canvas = null;
            camera = null;
            Frames.Clear();
            running = false;
        }
    }
}
