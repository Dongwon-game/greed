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
        private static readonly int ModuleDistanceProperty = Shader.PropertyToID("_CorridorStart");

        private readonly GameObject stage;
        private readonly Camera camera;
        private readonly Material stone;
        private readonly Mesh moduleMesh;
        private readonly Mesh sandTrapMesh;
        private readonly Transform sandTrap;
        private readonly Texture2D contactShadowTexture;
        private readonly Transform[] modules = new Transform[9];
        private readonly Mesh[] curvedMeshes = new Mesh[9];
        private readonly RunRoute route = new RunRoute();
        private readonly Dictionary<float, RunRoute.Pose> routeSections = new Dictionary<float, RunRoute.Pose>();
        private readonly MaterialPropertyBlock stonePosition = new MaterialPropertyBlock();
        private readonly Vector3[] moduleVertices;
        private readonly Vector3[] curvedVertices;
        private RenderTexture target;
        private int firstModule = int.MinValue;
        private float renderedDistance = -1f;
        private float renderedRoll;
        private bool trackingSandTrap;
        private float sandTrapElapsed;
        private float sandTrapDistance;
        private bool trackingTurn;
        private float turnElapsed;

        internal Texture Texture => target;
        internal Sprite ContactShadow { get; }
        internal Vector3 RunnerGroundViewport { get; private set; }
        internal float RunnerHeading { get; private set; }

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
            moduleVertices = moduleMesh.vertices;
            curvedVertices = new Vector3[moduleVertices.Length];
            for (int i = 0; i < modules.Length; i++)
                modules[i] = CreateStoneMesh("Stone corridor module", moduleMesh);
            sandTrapMesh = BuildSandTrap();
            sandTrap = CreateStoneMesh("Collapsing sand bed", sandTrapMesh);
            sandTrap.gameObject.SetActive(false);
            contactShadowTexture = BuildContactShadow();
            ContactShadow = Sprite.Create(contactShadowTexture, new Rect(0f, 0f, 32f, 32f), Vector2.one * 0.5f);
            ContactShadow.hideFlags = HideFlags.HideAndDontSave;
        }

        internal void Render(GreedLastRunSnapshot run, float roll, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            float scale = Mathf.Min(1f, Mathf.Min(1600f / width, 900f / height));
            width = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            bool targetChanged = target == null || target.width != width || target.height != height || !target.IsCreated();
            if (targetChanged)
            {
                ReleaseTarget();
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Greed corridor frame", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
            }

            float distance = Mathf.Max(0f, run.Distance);
            bool routeChanged = UpdateRoute(run, distance);
            bool trapChanged = RefreshSandTrap(run, distance);
            if (!targetChanged && distance == renderedDistance && roll == renderedRoll && !trapChanged && !routeChanged) return;
            int newFirst = Mathf.FloorToInt((distance - CameraFollowDistance) / ModuleLength) - 1;
            if (newFirst != firstModule || routeChanged)
            {
                route.ForgetBefore(newFirst * ModuleLength);
                for (int i = 0; i < modules.Length; i++) PlaceModule(i, (newFirst + i) * ModuleLength);
                firstModule = newFirst;
            }

            Vector3 eye = route.Sample(distance - CameraFollowDistance).Position;
            RunRoute.Pose runner = route.Sample(distance);
            RunnerHeading = Mathf.DeltaAngle(0f, runner.Rotation.eulerAngles.y);
            Vector3 ahead = route.Sample(distance + 1.6f).Position;
            camera.transform.localPosition = eye + Vector3.up * CameraHeight;
            camera.transform.localRotation = Quaternion.LookRotation(ahead - eye) * Quaternion.Euler(9f, 0f, -roll);
            RunnerGroundViewport = camera.WorldToViewportPoint(stage.transform.TransformPoint(runner.Position));
            camera.Render();
            renderedDistance = distance;
            renderedRoll = roll;
        }

        private bool UpdateRoute(GreedLastRunSnapshot run, float distance)
        {
            bool reset = distance < renderedDistance;
            if (reset)
            {
                route.Clear();
                trackingTurn = false;
            }

            bool isTurn = run.ActivePattern && GreedLastRunCore.IsRotationPattern(run.Pattern);
            bool enteredTurn = isTurn && (!trackingTurn || run.ElapsedSeconds + 0.05f < turnElapsed);
            if (enteredTurn)
            {
                float contact = distance - run.CoreDeltaSeconds * GreedLastRunCore.RunSpeedMetersPerSecond;
                route.AddBend(contact, run.Pattern.Channel == GreedLastRunChannel.Left ? -1f : 1f);
            }
            trackingTurn = isTurn;
            turnElapsed = run.ElapsedSeconds;
            return reset || enteredTurn;
        }

        private void PlaceModule(int index, float distance)
        {
            Transform module = modules[index];
            RunRoute.Pose origin = route.Sample(distance);
            module.SetLocalPositionAndRotation(origin.Position, origin.Rotation);
            SetStonePosition(module, distance);
            MeshFilter filter = module.GetComponent<MeshFilter>();
            if (!route.HasBend(distance, distance + ModuleLength))
            {
                filter.sharedMesh = moduleMesh;
                return;
            }

            Mesh curved = curvedMeshes[index];
            if (curved == null)
            {
                curved = UnityEngine.Object.Instantiate(moduleMesh);
                curved.name = "Greed curved corridor";
                curved.MarkDynamic();
                curvedMeshes[index] = curved;
            }
            routeSections.Clear();
            Quaternion toLocal = Quaternion.Inverse(origin.Rotation);
            for (int i = 0; i < moduleVertices.Length; i++)
            {
                Vector3 vertex = moduleVertices[i];
                if (!routeSections.TryGetValue(vertex.z, out RunRoute.Pose section))
                {
                    section = route.Sample(distance + vertex.z);
                    routeSections.Add(vertex.z, section);
                }
                Vector3 edge = section.Position + section.Rotation * new Vector3(vertex.x, vertex.y, 0f);
                curvedVertices[i] = toLocal * (edge - origin.Position);
            }
            curved.vertices = curvedVertices;
            curved.RecalculateBounds();
            filter.sharedMesh = curved;
        }

        private bool RefreshSandTrap(GreedLastRunSnapshot run, float distance)
        {
            bool wasVisible = sandTrap.gameObject.activeSelf;
            if (distance < renderedDistance)
            {
                sandTrap.gameObject.SetActive(false);
                trackingSandTrap = false;
            }
            bool isSandTrap = run.ActivePattern && run.Pattern.Channel == GreedLastRunChannel.Center
                && run.Pattern.PatternId.StartsWith("center_sand_perfect", StringComparison.Ordinal);
            bool moved = isSandTrap && (!trackingSandTrap || run.ElapsedSeconds + 0.05f < sandTrapElapsed);
            if (moved)
            {
                // Anchor it at the existing judgement point. The camera travels to it as the run advances.
                sandTrapDistance = distance - run.CoreDeltaSeconds * GreedLastRunCore.RunSpeedMetersPerSecond;
                RunRoute.Pose anchor = route.Sample(sandTrapDistance);
                sandTrap.SetLocalPositionAndRotation(anchor.Position, anchor.Rotation);
                SetStonePosition(sandTrap, sandTrapDistance);
                sandTrap.gameObject.SetActive(true);
            }
            trackingSandTrap = isSandTrap;
            sandTrapElapsed = run.ElapsedSeconds;
            if (distance - sandTrapDistance > CameraFollowDistance + 1f)
                sandTrap.gameObject.SetActive(false);
            return moved || wasVisible != sandTrap.gameObject.activeSelf;
        }

        private void SetStonePosition(Transform piece, float distance)
        {
            stonePosition.SetFloat(ModuleDistanceProperty, distance);
            piece.GetComponent<MeshRenderer>().SetPropertyBlock(stonePosition);
        }

        private Transform CreateStoneMesh(string name, Mesh mesh)
        {
            var piece = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = CorridorLayer };
            piece.transform.SetParent(stage.transform, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = stone;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return piece.transform;
        }

        public void Dispose()
        {
            ReleaseTarget();
            UnityEngine.Object.Destroy(stage);
            UnityEngine.Object.Destroy(stone);
            UnityEngine.Object.Destroy(moduleMesh);
            foreach (Mesh mesh in curvedMeshes)
                if (mesh != null) UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(sandTrapMesh);
            UnityEngine.Object.Destroy(ContactShadow);
            UnityEngine.Object.Destroy(contactShadowTexture);
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
            for (int row = 0; row < 8; row++)
            {
                mesh.Box(new Vector3(0f, -0.19f, row + 0.5f), new Vector3(7.8f, 0.28f, 1f), new Color32(44, 46, 40, 255));
                mesh.Box(new Vector3(0f, 5.25f, row + 0.5f), new Vector3(8f, 0.34f, 1f), new Color32(59, 74, 68, 255));
                float rowOffset = row % 2 == 0 ? 0f : 0.625f;
                for (int tile = -1; tile < 6; tile++)
                {
                    float left = Mathf.Max(-3.75f, -3.75f + tile * 1.25f + rowOffset);
                    float right = Mathf.Min(3.75f, -2.5f + tile * 1.25f + rowOffset);
                    if (right <= left) continue;
                    float x = (left + right) * 0.5f;
                    Color32 color = Mathf.Abs(x) > 2.65f ? Mix(slate, bronze, 0.26f) : sandstone;
                    mesh.Box(new Vector3(x, -0.045f, row + 0.5f), new Vector3(right - left - 0.027f, 0.09f, 0.977f),
                        Shade(color, 0.76f + (float)random.NextDouble() * 0.25f));
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 8; row++)
                {
                    mesh.Box(new Vector3(side * 3.94f, 2.52f, row + 0.5f), new Vector3(0.24f, 5.16f, 1f), Shade(slate, 0.48f));
                    mesh.Box(new Vector3(side * 3.70f, 1.04f, row + 0.5f), new Vector3(0.13f, 0.12f, 1f), Shade(bronze, 0.77f));
                }
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
            }
            foreach (float z in new[] { 1f, 5f })
                mesh.Box(new Vector3(0f, 4.87f, z), new Vector3(7.8f, 0.48f, 0.84f), Shade(slate, 0.82f));
            return mesh.Finish();
        }

        private static Mesh BuildSandTrap()
        {
            var mesh = new StoneGeometry();
            var rim = new Color32(143, 104, 61, 255);
            var sand = new Color32(110, 73, 41, 255);
            mesh.Box(new Vector3(0f, 0.025f, 0f), new Vector3(4.55f, 0.05f, 1.18f), new Color32(47, 33, 24, 255));
            for (int side = -1; side <= 1; side += 2)
            {
                mesh.Box(new Vector3(side * 2.22f, 0.08f, 0f), new Vector3(0.21f, 0.15f, 1.30f), rim);
                mesh.Box(new Vector3(0f, 0.055f, side * 0.59f), new Vector3(4.46f, 0.10f, 0.13f), rim);
            }
            for (int i = 0; i < 9; i++)
            {
                float x = (i - 4) * 0.47f;
                float height = i % 3 == 1 ? 0.14f : 0.085f;
                mesh.Box(new Vector3(x, height * 0.5f + 0.05f, i % 2 == 0 ? 0.24f : -0.24f),
                    new Vector3(0.43f, height, 0.44f), Shade(sand, 0.79f + (i % 4) * 0.08f));
            }
            return mesh.Finish();
        }

        private static Texture2D BuildContactShadow()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 center = new Vector2((x + 0.5f) / size, (y + 0.5f) / size) * 2f - Vector2.one;
                float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.99f, center.sqrMagnitude));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(opacity * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Color32 Shade(Color32 color, float scale) => (Color32)((Color)color * scale);
        private static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

        private sealed class StoneGeometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<Vector2> textureCoordinates = new List<Vector2>();
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
                for (int i = 0; i < 4; i++)
                {
                    Vector3 point = vertices[start + i];
                    textureCoordinates.Add(new Vector2(point.x, point.z));
                    colors.Add(tint);
                }
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }

            internal Mesh Finish()
            {
                var mesh = new Mesh { name = "Greed stone corridor", hideFlags = HideFlags.HideAndDontSave };
                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                mesh.SetUVs(0, textureCoordinates);
                mesh.SetTriangles(triangles, 0);
                return mesh;
            }
        }

        private sealed class RunRoute
        {
            private const float BendLength = 5.4f;
            private const float BendDegrees = 42f;
            private readonly List<Bend> bends = new List<Bend>();

            internal readonly struct Pose
            {
                internal readonly Vector3 Position;
                internal readonly Quaternion Rotation;
                internal Pose(Vector3 position, Quaternion rotation)
                {
                    Position = position;
                    Rotation = rotation;
                }
            }

            private readonly struct Bend
            {
                internal readonly float Start, Degrees;
                internal readonly Pose Entry;
                internal float End => Start + BendLength;

                internal Bend(float start, float degrees, Pose entry)
                {
                    Start = start;
                    Degrees = degrees;
                    Entry = entry;
                }

                internal Pose Sample(float distance)
                {
                    if (distance <= Start) return Advance(Entry, distance - Start);
                    float progress = Mathf.Clamp01((distance - Start) / BendLength);
                    float radians = Degrees * progress * Mathf.Deg2Rad;
                    float radius = BendLength / (Degrees * Mathf.Deg2Rad);
                    Vector3 offset = new Vector3((1f - Mathf.Cos(radians)) * radius, 0f, Mathf.Sin(radians) * radius);
                    var pose = new Pose(Entry.Position + Entry.Rotation * offset,
                        Entry.Rotation * Quaternion.Euler(0f, Degrees * progress, 0f));
                    return Advance(pose, Mathf.Max(0f, distance - End));
                }
            }

            internal void AddBend(float contact, float direction)
            {
                float start = contact - BendLength * 0.5f;
                if (bends.Count > 0) start = Mathf.Max(start, bends[bends.Count - 1].End);
                bends.Add(new Bend(start, direction * BendDegrees, Sample(start)));
            }

            internal bool HasBend(float start, float end)
            {
                foreach (Bend bend in bends)
                    if (start < bend.End && end > bend.Start) return true;
                return false;
            }

            internal void ForgetBefore(float distance)
            {
                while (bends.Count > 1 && bends[1].Start <= distance) bends.RemoveAt(0);
            }

            internal void Clear() => bends.Clear();

            internal Pose Sample(float distance)
            {
                if (bends.Count == 0) return new Pose(Vector3.forward * distance, Quaternion.identity);
                for (int i = bends.Count - 1; i > 0; i--)
                    if (distance >= bends[i].Start) return bends[i].Sample(distance);
                return bends[0].Sample(distance);
            }

            private static Pose Advance(Pose pose, float distance)
                => new Pose(pose.Position + pose.Rotation * (Vector3.forward * distance), pose.Rotation);
        }
    }
}
