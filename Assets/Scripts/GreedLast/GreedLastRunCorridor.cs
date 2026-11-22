using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GreedLast
{
    internal sealed class GreedLastRunCorridor : IDisposable
    {
        private const float ModuleLength = 8f;
        private const float CameraHeight = 3.05f;
        private const float CameraFollowDistance = 6.2f;
        private const int CorridorLayer = 31;

        private readonly GameObject stage;
        private readonly Camera camera;
        private readonly Material stone;
        private readonly Mesh moduleMesh;
        private readonly Transform[] modules = new Transform[9];
        private RenderTexture target;
        private int firstModule = int.MinValue;
        private float renderedDistance = -1f;
        private float renderedRoll;

        internal Texture Texture => target;
        internal Vector3 RunnerGroundViewport { get; private set; }

        internal static GreedLastRunCorridor Create()
        {
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;
            Shader shader = Resources.Load<Shader>("GreedLast/RunCorridorStone");
            return shader != null && shader.isSupported ? new GreedLastRunCorridor(shader) : null;
        }

        private GreedLastRunCorridor(Shader shader)
        {
            // Keep the private stage outside the scene cameras' view, including scenes the UI persists through.
            stage = new GameObject("Greed forward corridor") { hideFlags = HideFlags.HideAndDontSave, layer = CorridorLayer };
            UnityEngine.Object.DontDestroyOnLoad(stage);
            stage.transform.position = Vector3.up * 600f;
            var cameraObject = new GameObject("Greed corridor camera") { hideFlags = HideFlags.HideAndDontSave, layer = CorridorLayer };
            cameraObject.transform.SetParent(stage.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.hideFlags = HideFlags.HideAndDontSave;
            camera.enabled = false;
            camera.fieldOfView = 51f;
            camera.nearClipPlane = 0.22f;
            camera.farClipPlane = 62f;
            camera.cullingMask = 1 << CorridorLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(15, 25, 26, 255);
            camera.allowHDR = false;
            camera.allowMSAA = false;

            stone = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            moduleMesh = BuildModule();
            for (int i = 0; i < modules.Length; i++)
            {
                var segment = new GameObject("Stone corridor module") { hideFlags = HideFlags.HideAndDontSave, layer = CorridorLayer };
                segment.transform.SetParent(stage.transform, false);
                segment.AddComponent<MeshFilter>().sharedMesh = moduleMesh;
                MeshRenderer renderer = segment.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = stone;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                modules[i] = segment.transform;
            }
        }

        internal void Render(float distance, float roll, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            float scale = Mathf.Min(1f, Mathf.Min(1600f / width, 900f / height));
            width = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            if (target == null || target.width != width || target.height != height || !target.IsCreated())
            {
                ReleaseTarget();
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Greed corridor frame", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                renderedDistance = -1f;
            }

            distance = Mathf.Max(0f, distance);
            if (distance == renderedDistance && roll == renderedRoll) return;
            int newFirst = Mathf.FloorToInt((distance - CameraFollowDistance) / ModuleLength) - 1;
            if (newFirst != firstModule)
            {
                for (int i = 0; i < modules.Length; i++)
                    modules[i].localPosition = Vector3.forward * ((newFirst + i) * ModuleLength);
                firstModule = newFirst;
            }

            camera.transform.localPosition = new Vector3(0f, CameraHeight, distance - CameraFollowDistance);
            camera.transform.localRotation = Quaternion.Euler(9f, 0f, -roll);
            RunnerGroundViewport = camera.WorldToViewportPoint(stage.transform.TransformPoint(0f, 0f, distance));
            camera.Render();
            renderedDistance = distance;
            renderedRoll = roll;
        }

        public void Dispose()
        {
            ReleaseTarget();
            UnityEngine.Object.Destroy(stage);
            UnityEngine.Object.Destroy(stone);
            UnityEngine.Object.Destroy(moduleMesh);
        }

        private void ReleaseTarget()
        {
            if (target == null) return;
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.Destroy(target);
            target = null;
        }

        private static Mesh BuildModule()
        {
            var mesh = new StoneGeometry();
            var random = new System.Random(731);
            Color32 slate = new Color32(73, 95, 89, 255);
            Color32 sandstone = new Color32(133, 120, 87, 255);
            Color32 bronze = new Color32(159, 119, 57, 255);
            mesh.Box(new Vector3(0f, -0.19f, 4f), new Vector3(7.8f, 0.28f, 8f), new Color32(44, 46, 40, 255));
            mesh.Box(new Vector3(0f, 5.25f, 4f), new Vector3(8f, 0.34f, 8f), new Color32(59, 74, 68, 255));
            for (int row = 0; row < 8; row++)
            {
                for (int tile = 0; tile < 6; tile++)
                {
                    float x = (tile - 2.5f) * 1.25f;
                    Color32 color = tile == 0 || tile == 5 ? Mix(slate, bronze, 0.26f) : sandstone;
                    mesh.Box(new Vector3(x, -0.045f, row + 0.5f), new Vector3(1.215f, 0.09f, 0.965f),
                        Shade(color, 0.76f + (float)random.NextDouble() * 0.25f));
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                mesh.Box(new Vector3(side * 3.94f, 2.52f, 4f), new Vector3(0.24f, 5.16f, 8f), Shade(slate, 0.48f));
                for (int height = 0; height < 6; height++)
                for (int block = 0; block < 4; block++)
                {
                    float heightOffset = 0.44f + height * 0.83f;
                    mesh.Box(new Vector3(side * 3.82f, heightOffset, block * 2f + 1f), new Vector3(0.25f, 0.80f, 1.97f),
                        Shade(slate, 0.76f + (float)random.NextDouble() * 0.26f));
                }

                foreach (float z in new[] { 1f, 5f })
                {
                    float x = side * 3.56f;
                    mesh.Box(new Vector3(x, 2.7f, z), new Vector3(0.63f, 4.96f, 0.83f), slate);
                    mesh.Box(new Vector3(x, 0.20f, z), new Vector3(0.83f, 0.40f, 1.02f), Mix(slate, sandstone, 0.45f));
                    mesh.Box(new Vector3(x, 0.48f, z), new Vector3(0.71f, 0.13f, 0.94f), bronze);
                    mesh.Box(new Vector3(x, 4.66f, z), new Vector3(0.83f, 0.36f, 1.05f), Mix(slate, sandstone, 0.55f));
                    mesh.Box(new Vector3(x, 4.44f, z), new Vector3(0.73f, 0.11f, 0.93f), bronze);
                    mesh.Box(new Vector3(side * 3.18f, 2.77f, z), new Vector3(0.18f, 0.49f, 0.34f), Shade(bronze, 0.65f));
                    mesh.Box(new Vector3(side * 3.08f, 2.83f, z), new Vector3(0.13f, 0.26f, 0.21f), new Color32(244, 186, 93, 255));
                }
                mesh.Box(new Vector3(side * 3.70f, 1.04f, 4f), new Vector3(0.13f, 0.12f, 8f), Shade(bronze, 0.77f));
            }
            foreach (float z in new[] { 1f, 5f })
                mesh.Box(new Vector3(0f, 4.87f, z), new Vector3(7.8f, 0.48f, 0.84f), Shade(slate, 0.82f));
            return mesh.Finish();
        }

        private static Color32 Shade(Color32 color, float scale) => (Color32)((Color)color * scale);
        private static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

        private sealed class StoneGeometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<int> triangles = new List<int>();

            internal void Box(Vector3 center, Vector3 size, Color32 tint)
            {
                Vector3 s = size * 0.5f;
                Vector3 a = center + new Vector3(-s.x, -s.y, -s.z);
                Vector3 b = center + new Vector3(s.x, -s.y, -s.z);
                Vector3 c = center + new Vector3(s.x, s.y, -s.z);
                Vector3 d = center + new Vector3(-s.x, s.y, -s.z);
                Vector3 e = center + new Vector3(-s.x, -s.y, s.z);
                Vector3 f = center + new Vector3(s.x, -s.y, s.z);
                Vector3 g = center + new Vector3(s.x, s.y, s.z);
                Vector3 h = center + new Vector3(-s.x, s.y, s.z);
                Quad(a, d, c, b, Shade(tint, 0.83f));
                Quad(e, f, g, h, Shade(tint, 0.75f));
                Quad(a, e, h, d, Shade(tint, 0.91f));
                Quad(b, c, g, f, Shade(tint, 0.74f));
                Quad(d, h, g, c, tint);
                Quad(a, b, f, e, Shade(tint, 0.60f));
            }

            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 tint)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int i = 0; i < 4; i++) colors.Add(tint);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }

            internal Mesh Finish()
            {
                var mesh = new Mesh { name = "Greed stone corridor", hideFlags = HideFlags.HideAndDontSave };
                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                return mesh;
            }
        }
    }
}
