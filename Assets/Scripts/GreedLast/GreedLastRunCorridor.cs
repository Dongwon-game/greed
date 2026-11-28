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
        private static readonly int TrapWarningProperty = Shader.PropertyToID("_TrapWarning");

        private readonly GameObject stage;
        private readonly Camera camera;
        private readonly GreedLastCorridorRunner runnerModel;
        private readonly Material stone;
        private readonly Mesh moduleMesh;
        private readonly WorldTrap[] traps;
        private readonly WorldTrap[] courseTraps;
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
        private float renderedResponse;
        private bool renderedRunning;
        private bool trackingTurn;
        private float turnElapsed;

        internal Texture Texture => target;
        internal Sprite ContactShadow { get; }
        internal Vector3 RunnerGroundViewport { get; private set; }
        internal float RunnerHeading { get; private set; }
        internal bool HasRunnerModel => runnerModel != null;
        internal bool HasRunnerGroundShadow => runnerModel?.HasGroundShadow == true;

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
            camera.backgroundColor = new Color32(19, 22, 23, 255);
            camera.allowHDR = false;
            camera.allowMSAA = true;
            contactShadowTexture = BuildContactShadow();
            runnerModel = GreedLastCorridorRunner.Create(stage.transform, contactShadowTexture);

            stone = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            moduleMesh = BuildModule();
            moduleVertices = moduleMesh.vertices;
            curvedVertices = new Vector3[moduleVertices.Length];
            for (int i = 0; i < modules.Length; i++)
                modules[i] = CreateStoneMesh("Stone corridor module", moduleMesh);
            traps = new[]
            {
                CreateTrap("center_sand_perfect", "Collapsing sand bed", BuildSandTrap(), Vector3.zero,
                    Vector3.zero, Vector3.zero, CameraFollowDistance + 1f),
                CreateTrap("right_pillar_clutch", "Right hanging pillar", BuildHangingPillar(), new Vector3(0.65f, 1.43f, 0f),
                    Vector3.up * 0.95f, Vector3.up * 3.8f, 2.6f),
                CreateTrap("left_blade_offbeat", "Left wall blade", BuildWallBlade(-1f), new Vector3(-0.20f, 1.29f, 0f),
                    Vector3.left * 0.85f, Vector3.left * 3.8f, 2.6f),
                CreateTrap("left_floor_perfect", "Left floor spikes", BuildFloorSpikes(), new Vector3(-0.42f, 0.64f, 0f),
                    Vector3.down * 0.66f, Vector3.down * 0.72f, 2.6f, BuildSpikeHousing()),
                CreateTrap("center_gate_clutch", "Center pressure gate", BuildPressureGate(), new Vector3(0f, 1.38f, 0f),
                    Vector3.up * 2.70f, Vector3.up * 3.6f, 2.6f, BuildGateHousing()),
                CreateTrap("right_blade_offbeat", "Right wall blade", BuildWallBlade(1f), new Vector3(0.20f, 1.29f, 0f),
                    Vector3.right * 2.46f, Vector3.right * 3.8f, 2.6f, BuildBladeHousing(1f))
            };
            courseTraps = new[]
            {
                CreateTrap(null, "Left raised stone", BuildRaisedStone(-1f), new Vector3(-0.45f, 0.54f, 0f),
                    Vector3.down * 0.48f, Vector3.down * 0.63f, 2.6f),
                CreateTrap(null, "Center raised stone", BuildRaisedStone(0f), new Vector3(0f, 0.54f, 0f),
                    Vector3.down * 0.48f, Vector3.down * 0.63f, 2.6f),
                CreateTrap(null, "Right raised stone", BuildRaisedStone(1f), new Vector3(0.45f, 0.54f, 0f),
                    Vector3.down * 0.48f, Vector3.down * 0.63f, 2.6f),
                CreateTrap(null, "Left overhead slab", BuildPressureGate(-0.68f), new Vector3(-0.68f, 1.38f, 0f),
                    Vector3.up * 2.7f, Vector3.up * 3.6f, 2.6f, BuildGateHousing(-0.68f)),
                CreateTrap(null, "Center overhead slab", BuildPressureGate(), new Vector3(0f, 1.38f, 0f),
                    Vector3.up * 2.7f, Vector3.up * 3.6f, 2.6f, BuildGateHousing()),
                CreateTrap(null, "Right overhead slab", BuildPressureGate(0.68f), new Vector3(0.68f, 1.38f, 0f),
                    Vector3.up * 2.7f, Vector3.up * 3.6f, 2.6f, BuildGateHousing(0.68f)),
                CreateTrap(null, "Left wall spear", BuildWallBlade(-1f), new Vector3(-0.20f, 1.29f, 0f),
                    Vector3.left * 2.46f, Vector3.left * 3.8f, 2.6f, BuildBladeHousing(-1f)),
                CreateTrap(null, "Right wall spear", BuildWallBlade(1f), new Vector3(0.20f, 1.29f, 0f),
                    Vector3.right * 2.46f, Vector3.right * 3.8f, 2.6f, BuildBladeHousing(1f)),
                CreateTrap(null, "Left turn stone inlay", BuildTurnInlay(-1f), new Vector3(0f, 0.08f, 0f),
                    Vector3.zero, Vector3.zero, CameraFollowDistance + 1f),
                CreateTrap(null, "Right turn stone inlay", BuildTurnInlay(1f), new Vector3(0f, 0.08f, 0f),
                    Vector3.zero, Vector3.zero, CameraFollowDistance + 1f)
            };
            ContactShadow = Sprite.Create(contactShadowTexture, new Rect(0f, 0f, 32f, 32f), Vector2.one * 0.5f);
            ContactShadow.hideFlags = HideFlags.HideAndDontSave;
        }

        internal void Render(GreedLastRunSnapshot run, float roll, int width, int height,
            bool running, GreedLastAvoidanceType response, float responseProgress, float responseDirection, bool missed)
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
                var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24) { msaaSamples = 2 };
                descriptor.msaaSamples = Mathf.Max(1, SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor));
                target = new RenderTexture(descriptor)
                {
                    name = "Greed corridor frame", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
            }

            float distance = Mathf.Max(0f, run.Distance);
            bool routeChanged = UpdateRoute(run, distance);
            bool trapChanged = RefreshTraps(run, distance);
            bool freezeRunner = renderedDistance >= 0f && distance >= renderedDistance
                && (run.Paused || run.ResumeCountdownActive);
            bool poseChanged = runnerModel != null && !freezeRunner
                && (distance != renderedDistance || renderedRunning != running || renderedResponse != responseProgress);
            if (!targetChanged && distance == renderedDistance && roll == renderedRoll
                && !trapChanged && !routeChanged && !poseChanged) return;
            int newFirst = Mathf.FloorToInt((distance - CameraFollowDistance) / ModuleLength) - 1;
            if (newFirst != firstModule || routeChanged)
            {
                route.ForgetBefore(newFirst * ModuleLength);
                for (int i = 0; i < modules.Length; i++) PlaceModule(i, (newFirst + i) * ModuleLength);
                firstModule = newFirst;
            }

            Vector3 eye = route.Sample(distance - CameraFollowDistance).Position;
            RunRoute.Pose runner = route.Sample(distance);
            if (poseChanged)
            {
                runnerModel.Pose(runner.Position, runner.Rotation, distance, running, response, responseProgress, responseDirection, missed);
                renderedRunning = running;
                renderedResponse = responseProgress;
            }
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

        private bool RefreshTraps(GreedLastRunSnapshot run, float distance)
        {
            WorldTrap selected = run.ActivePattern ? SelectTrap(run) : null;
            return RefreshTrapSet(traps, selected, run, distance) | RefreshTrapSet(courseTraps, selected, run, distance);
        }

        private WorldTrap SelectTrap(GreedLastRunSnapshot run)
        {
            GreedLastPatternModel pattern = run.Pattern;
            foreach (WorldTrap trap in traps)
            {
                if (trap.MatchesTemplate(pattern.PatternId)) return trap;
            }
            if (run.InfiniteMode) return null;

            bool right = pattern.Channel == GreedLastRunChannel.Right;
            if (GreedLastRunCore.IsRotationPattern(pattern)) return courseTraps[right ? 9 : 8];
            int channel = Mathf.Clamp((int)pattern.Channel - 1, 0, 2);
            switch (pattern.AvoidanceType)
            {
                case GreedLastAvoidanceType.Jump: return courseTraps[channel];
                case GreedLastAvoidanceType.Slide: return courseTraps[3 + channel];
                case GreedLastAvoidanceType.Twist: return courseTraps[right ? 7 : 6];
                default: return null;
            }
        }

        private bool RefreshTrapSet(WorldTrap[] set, WorldTrap selected, GreedLastRunSnapshot run, float distance)
        {
            bool changed = false;
            foreach (WorldTrap trap in set)
            {
                bool wasVisible = trap.Root.gameObject.activeSelf;
                if (distance < renderedDistance)
                {
                    trap.Root.gameObject.SetActive(false);
                    trap.Tracking = false;
                }
                bool active = trap == selected;
                bool spawned = active && (!trap.Tracking || run.ElapsedSeconds + 0.05f < trap.Elapsed
                    || trap.PatternId != run.Pattern.PatternId);
                if (spawned)
                {
                    // Place the object at the existing rhythm judgement, on the same route as the runner.
                    trap.PatternId = run.Pattern.PatternId;
                    trap.Distance = distance - run.CoreDeltaSeconds * GreedLastRunCore.RunSpeedMetersPerSecond;
                    trap.Anchor = route.Sample(trap.Distance);
                    if (trap.Housing != null) SetStonePosition(trap.Housing, trap.Distance);
                    trap.Root.gameObject.SetActive(true);
                    changed = true;
                }
                trap.Tracking = active;
                trap.Elapsed = run.ElapsedSeconds;
                float passed = distance - trap.Distance;
                if (passed > trap.RetreatDistance)
                    trap.Root.gameObject.SetActive(false);
                if (trap.Root.gameObject.activeSelf)
                {
                    trap.Place(passed);
                    float warning = active
                        ? Mathf.Clamp01(1f - Mathf.Abs(run.CoreDeltaSeconds) / Mathf.Max(0.01f, run.Pattern.GoodWindowSeconds))
                        : 0f;
                    if (spawned || trap.Warning != warning)
                    {
                        changed = true;
                        trap.Warning = warning;
                        SetStonePosition(trap.Moving, trap.Distance, warning);
                    }
                }
                changed |= wasVisible != trap.Root.gameObject.activeSelf;
            }
            return changed;
        }

        internal bool TryGetTrapProjection(string patternId, out float distance, out Vector3 viewport)
        {
            return TryGetTrapProjection(traps, patternId, out distance, out viewport)
                || TryGetTrapProjection(courseTraps, patternId, out distance, out viewport);
        }

        private bool TryGetTrapProjection(WorldTrap[] set, string patternId, out float distance, out Vector3 viewport)
        {
            foreach (WorldTrap trap in set)
            {
                if (patternId == null || !trap.Root.gameObject.activeSelf
                    || (patternId != trap.PatternId && patternId != trap.Template)) continue;
                distance = trap.Distance;
                viewport = camera.WorldToViewportPoint(trap.Moving.TransformPoint(trap.FeaturePoint));
                return true;
            }
            distance = 0f;
            viewport = Vector3.zero;
            return false;
        }

        private WorldTrap CreateTrap(string pattern, string name, Mesh mesh, Vector3 featurePoint,
            Vector3 approachOffset, Vector3 retreatOffset, float retreatDistance, Mesh housing = null)
        {
            var root = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = CorridorLayer }.transform;
            root.SetParent(stage.transform, false);
            root.gameObject.SetActive(false);
            Transform moving = CreateStoneMesh("Mechanism", mesh);
            moving.SetParent(root, false);
            Transform frame = null;
            if (housing != null)
            {
                frame = CreateStoneMesh("Housing", housing);
                frame.SetParent(root, false);
            }
            return new WorldTrap(pattern, root, moving, frame, featurePoint, approachOffset, retreatOffset, retreatDistance);
        }

        private sealed class WorldTrap
        {
            internal readonly string Template;
            internal string PatternId;
            private readonly Vector3 approachOffset;
            private readonly Vector3 retreatOffset;
            internal readonly Transform Root;
            internal readonly Transform Moving;
            internal readonly Transform Housing;
            internal readonly Vector3 FeaturePoint;
            internal readonly float RetreatDistance;
            internal RunRoute.Pose Anchor;
            internal bool Tracking;
            internal float Elapsed, Distance, Warning;

            internal WorldTrap(string pattern, Transform root, Transform moving, Transform housing, Vector3 featurePoint,
                Vector3 approachOffset, Vector3 retreatOffset, float retreatDistance)
            {
                Template = pattern;
                Root = root;
                Moving = moving;
                Housing = housing;
                FeaturePoint = featurePoint;
                this.approachOffset = approachOffset;
                this.retreatOffset = retreatOffset;
                RetreatDistance = retreatDistance;
            }

            internal bool MatchesTemplate(string patternId) => patternId != null && Template != null
                && patternId.StartsWith(Template, StringComparison.Ordinal);

            internal void Place(float passed)
            {
                float approach = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-4.4f, -0.2f, passed));
                float retreat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, RetreatDistance, passed));
                Vector3 offset = approachOffset * approach + retreatOffset * retreat;
                Root.SetLocalPositionAndRotation(Anchor.Position, Anchor.Rotation);
                Moving.localPosition = offset;
            }
        }

        private void SetStonePosition(Transform piece, float distance, float warning = 0f)
        {
            stonePosition.SetFloat(ModuleDistanceProperty, distance);
            stonePosition.SetFloat(TrapWarningProperty, warning);
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
            runnerModel?.Dispose();
            UnityEngine.Object.Destroy(stone);
            UnityEngine.Object.Destroy(moduleMesh);
            foreach (Mesh mesh in curvedMeshes)
                if (mesh != null) UnityEngine.Object.Destroy(mesh);
            DestroyTrapMeshes(traps);
            DestroyTrapMeshes(courseTraps);
            UnityEngine.Object.Destroy(ContactShadow);
            UnityEngine.Object.Destroy(contactShadowTexture);
            UnityEngine.Object.Destroy(stage);
        }

        private static void DestroyTrapMeshes(WorldTrap[] set)
        {
            foreach (WorldTrap trap in set)
            {
                UnityEngine.Object.Destroy(trap.Moving.GetComponent<MeshFilter>().sharedMesh);
                if (trap.Housing != null) UnityEngine.Object.Destroy(trap.Housing.GetComponent<MeshFilter>().sharedMesh);
            }
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
            Color32 stone = new Color32(114, 105, 88, 255);
            Color32 sandstone = new Color32(151, 131, 103, 255);
            Color32 bronze = new Color32(141, 103, 56, 255);
            for (int row = 0; row < 8; row++)
            {
                mesh.Box(new Vector3(0f, -0.19f, row + 0.5f), new Vector3(7.8f, 0.28f, 1f), new Color32(56, 48, 39, 255));
                mesh.Box(new Vector3(0f, 5.25f, row + 0.5f), new Vector3(8f, 0.34f, 1f), new Color32(78, 74, 66, 255));
                float rowOffset = (float)random.NextDouble() * 0.83f;
                for (int tile = -1; tile < 6; tile++)
                {
                    float left = Mathf.Max(-3.75f, -3.75f + tile * 1.25f + rowOffset);
                    float right = Mathf.Min(3.75f, -2.5f + tile * 1.25f + rowOffset);
                    if (right <= left) continue;
                    float x = (left + right) * 0.5f;
                    Color32 color = Mathf.Abs(x) > 2.65f ? stone : sandstone;
                    float width = right - left - 0.013f;
                    if (width <= 0.02f) continue;
                    mesh.PavingStone(new Vector3(x, 0f, row + 0.5f), width, 0.984f,
                        Shade(color, 0.82f + (float)random.NextDouble() * 0.24f));
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 8; row++)
                {
                    mesh.Box(new Vector3(side * 3.94f, 2.52f, row + 0.5f), new Vector3(0.24f, 5.16f, 1f), Shade(stone, 0.55f));
                    mesh.Box(new Vector3(side * 3.70f, 1.04f, row + 0.5f), new Vector3(0.13f, 0.12f, 1f), Shade(bronze, 0.77f));
                }
                for (int height = 0; height < 6; height++)
                for (int block = -1; block < 4; block++)
                {
                    float offset = height % 2 == 0 ? 0f : 0.93f;
                    float start = Mathf.Max(0f, block * 2f + offset);
                    float end = Mathf.Min(ModuleLength, (block + 1) * 2f + offset);
                    if (end <= start) continue;
                    float heightOffset = 0.44f + height * 0.83f;
                    mesh.Box(new Vector3(side * 3.82f, heightOffset, (start + end) * 0.5f),
                        new Vector3(0.25f, 0.81f, end - start - 0.022f),
                        Shade(stone, 0.81f + (float)random.NextDouble() * 0.25f));
                }

                foreach (float z in new[] { 1f, 5f })
                {
                    float x = side * 3.56f;
                    mesh.Box(new Vector3(x, 2.7f, z), new Vector3(0.63f, 4.96f, 0.83f), stone);
                    mesh.Box(new Vector3(x, 0.20f, z), new Vector3(0.83f, 0.40f, 1.02f), Mix(stone, sandstone, 0.45f));
                    mesh.Box(new Vector3(x, 0.48f, z), new Vector3(0.71f, 0.13f, 0.94f), bronze);
                    mesh.Box(new Vector3(x, 4.66f, z), new Vector3(0.83f, 0.36f, 1.05f), Mix(stone, sandstone, 0.55f));
                    mesh.Box(new Vector3(x, 4.44f, z), new Vector3(0.73f, 0.11f, 0.93f), bronze);
                    mesh.Box(new Vector3(side * 3.19f, 2.54f, z), new Vector3(0.17f, 0.51f, 0.23f), Shade(bronze, 0.57f));
                    mesh.Box(new Vector3(side * 3.04f, 2.57f, z), new Vector3(0.38f, 0.17f, 0.43f), Shade(bronze, 0.72f));
                    mesh.Box(new Vector3(side * 3.04f, 2.67f, z), new Vector3(0.43f, 0.09f, 0.48f), bronze);
                    float lean = ((float)random.NextDouble() - 0.5f) * 0.17f;
                    mesh.Flame(new Vector3(side * 3.04f, 2.69f, z), 0.16f, 0.48f, lean,
                        new Color32(251, 155, 62, 255));
                    mesh.Flame(new Vector3(side * 3.04f, 2.70f, z - 0.04f), 0.11f, 0.28f, lean * 0.5f,
                        new Color32(255, 224, 141, 255));
                }
            }
            foreach (float z in new[] { 1f, 5f })
                mesh.Box(new Vector3(0f, 4.87f, z), new Vector3(7.8f, 0.48f, 0.84f), Shade(stone, 0.82f));
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

        private static Mesh BuildHangingPillar()
        {
            var mesh = new StoneGeometry();
            var sandstone = new Color32(127, 113, 86, 255);
            var bronze = new Color32(192, 132, 58, 255);
            mesh.Box(new Vector3(2.16f, 4.69f, 0f), new Vector3(1.66f, 0.8f, 1.17f), Shade(sandstone, 0.69f));
            mesh.Box(new Vector3(1.92f, 3.45f, 0f), new Vector3(1.2f, 2.48f, 0.81f), sandstone);
            mesh.Box(new Vector3(1.92f, 2.20f, 0f), new Vector3(1.65f, 0.61f, 0.98f), Shade(sandstone, 0.78f));
            mesh.Box(new Vector3(0.65f, 1.72f, 0f), new Vector3(3.54f, 0.60f, 0.88f), sandstone);
            mesh.Box(new Vector3(0.65f, 1.49f, -0.46f), new Vector3(3.55f, 0.12f, 0.09f), bronze);
            for (int i = 0; i < 5; i++)
                mesh.Box(new Vector3(-0.69f + i * 0.66f, 1.73f, -0.46f), new Vector3(0.41f, 0.19f, 0.075f), Shade(bronze, 0.66f));
            return mesh.Finish();
        }

        private static Mesh BuildSpikeHousing()
        {
            var mesh = new StoneGeometry();
            var bronze = new Color32(152, 109, 65, 255);
            mesh.Box(new Vector3(-0.64f, 0.04f, 0f), new Vector3(3.45f, 0.085f, 1.08f), new Color32(53, 43, 35, 255));
            for (int side = -1; side <= 1; side += 2)
                mesh.Box(new Vector3(-0.64f, 0.09f, side * 0.52f), new Vector3(3.48f, 0.12f, 0.13f), bronze);
            for (int i = 0; i < 6; i++)
                mesh.Box(new Vector3(-2.1f + i * 0.58f, 0.08f, 0f), new Vector3(0.08f, 0.10f, 1.04f), Shade(bronze, 0.76f));
            return mesh.Finish();
        }

        private static Mesh BuildFloorSpikes()
        {
            var mesh = new StoneGeometry();
            var copper = new Color32(194, 128, 76, 255);
            for (int row = -1; row <= 1; row += 2)
            for (int i = 0; i < 6; i++)
                mesh.Spike(new Vector3(-2f + i * 0.52f, 0.08f, row * 0.24f), 0.31f,
                    i % 2 == 0 ? 0.63f : 0.50f, copper);
            return mesh.Finish();
        }

        private static Mesh BuildGateHousing(float x = 0f)
        {
            var mesh = new StoneGeometry();
            var bronze = new Color32(152, 109, 65, 255);
            mesh.Box(new Vector3(x, 4.29f, 0f), new Vector3(4.5f, 0.27f, 1.28f), bronze);
            for (int side = -1; side <= 1; side += 2)
                mesh.Box(new Vector3(x + side * 2.18f, 3.54f, 0f), new Vector3(0.28f, 1.66f, 0.79f), Shade(bronze, 0.72f));
            return mesh.Finish();
        }

        private static Mesh BuildPressureGate(float x = 0f)
        {
            var mesh = new StoneGeometry();
            var stone = new Color32(132, 101, 83, 255);
            var bronze = new Color32(207, 140, 65, 255);
            mesh.Box(new Vector3(x, 2.33f, 0f), new Vector3(4.12f, 1.9f, 0.64f), stone);
            mesh.Box(new Vector3(x, 1.40f, -0.12f), new Vector3(4.32f, 0.23f, 0.78f), bronze);
            for (int i = 0; i < 7; i++)
                mesh.Box(new Vector3(x + (i - 3) * 0.56f, 2.08f, -0.34f), new Vector3(0.15f, 1.10f, 0.11f), Shade(bronze, 0.78f));
            return mesh.Finish();
        }

        private static Mesh BuildRaisedStone(float side)
        {
            var mesh = new StoneGeometry();
            var stone = new Color32(138, 105, 80, 255);
            var rim = new Color32(190, 129, 72, 255);
            float x = side * 0.68f;
            mesh.Box(new Vector3(x, 0.18f, 0.07f), new Vector3(2.73f, 0.49f, 1.06f), stone);
            mesh.Box(new Vector3(x, 0.44f, 0.07f), new Vector3(2.81f, 0.13f, 1.10f), rim);
            for (int i = -1; i <= 1; i++)
                mesh.Box(new Vector3(x + i * 0.81f, 0.55f, 0.07f), new Vector3(0.70f, 0.12f, 0.82f), Shade(stone, 1.14f));
            return mesh.Finish();
        }

        private static Mesh BuildTurnInlay(float side)
        {
            var mesh = new StoneGeometry();
            var bronze = new Color32(185, 122, 70, 255);
            for (int i = 0; i < 3; i++)
            {
                float shift = i * -0.60f;
                var near = new Vector3(side * (0.34f + shift), 0.045f, -0.96f);
                var far = new Vector3(side * (0.34f + shift), 0.045f, 0.96f);
                var tip = new Vector3(side * (1.42f + shift), 0.045f, 0f);
                if (side > 0f) mesh.Triangle(near, far, tip, bronze);
                else mesh.Triangle(far, near, tip, bronze);
            }
            return mesh.Finish();
        }

        private static Mesh BuildBladeHousing(float side)
        {
            var mesh = new StoneGeometry();
            mesh.Box(new Vector3(side * 3.13f, 1.29f, 0f), new Vector3(0.37f, 1.25f, 1.03f), new Color32(127, 96, 69, 255));
            mesh.Box(new Vector3(side * 2.94f, 1.29f, 0f), new Vector3(0.06f, 0.61f, 0.66f), new Color32(42, 34, 29, 255));
            return mesh.Finish();
        }

        private static Mesh BuildWallBlade(float side)
        {
            var mesh = new StoneGeometry();
            var bronze = new Color32(170, 113, 61, 255);
            var blade = new Color32(128, 134, 126, 255);
            var edge = new Color32(207, 182, 138, 255);
            mesh.Box(new Vector3(side * 3.54f, 1.28f, 0f), new Vector3(0.53f, 1.38f, 0.97f), new Color32(88, 79, 64, 255));
            mesh.Box(new Vector3(side * 3.22f, 1.29f, 0f), new Vector3(0.41f, 0.58f, 0.76f), bronze);
            mesh.Box(new Vector3(side * 1.90f, 1.29f, 0f), new Vector3(2.49f, 0.26f, 0.46f), blade);
            mesh.Box(new Vector3(side * 1.84f, 1.18f, -0.235f), new Vector3(2.35f, 0.06f, 0.06f), edge);
            var near = new Vector3(side * 0.66f, 1.42f, -0.23f * side);
            var far = new Vector3(side * 0.66f, 1.42f, 0.23f * side);
            var point = new Vector3(side * 0.065f, 1.27f, -0.06f);
            var lowerNear = near - Vector3.up * 0.26f;
            var lowerFar = far - Vector3.up * 0.26f;
            mesh.Triangle(near, point, far, Shade(blade, 1.28f));
            mesh.Triangle(lowerFar, point, lowerNear, Shade(blade, 0.74f));
            mesh.Triangle(near, lowerNear, point, side > 0f ? edge : blade);
            mesh.Triangle(far, point, lowerFar, side > 0f ? blade : edge);
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

            internal void Spike(Vector3 center, float width, float height, Color32 tint)
            {
                float r = width * 0.5f;
                Vector3 a = center + new Vector3(-r, 0f, -r);
                Vector3 b = center + new Vector3(r, 0f, -r);
                Vector3 c = center + new Vector3(r, 0f, r);
                Vector3 d = center + new Vector3(-r, 0f, r);
                Vector3 tip = center + Vector3.up * height;
                Triangle(a, tip, b, tint);
                Triangle(b, tip, c, Shade(tint, 0.74f));
                Triangle(c, tip, d, Shade(tint, 0.81f));
                Triangle(d, tip, a, Shade(tint, 0.90f));
            }

            internal void PavingStone(Vector3 center, float width, float depth, Color32 tint)
            {
                float x = width * 0.5f, z = depth * 0.5f;
                float bevel = Mathf.Min(0.029f, x * 0.33f);
                Vector3 a = center + new Vector3(-x + bevel, 0f, -z + bevel);
                Vector3 b = center + new Vector3(x - bevel, 0f, -z + bevel);
                Vector3 c = center + new Vector3(x - bevel, 0f, z - bevel);
                Vector3 d = center + new Vector3(-x + bevel, 0f, z - bevel);
                Vector3 e = center + new Vector3(-x, -0.049f, -z);
                Vector3 f = center + new Vector3(x, -0.049f, -z);
                Vector3 g = center + new Vector3(x, -0.049f, z);
                Vector3 h = center + new Vector3(-x, -0.049f, z);
                Quad(a, d, c, b, tint);
                Quad(e, a, b, f, Shade(tint, 0.83f));
                Quad(f, b, c, g, Shade(tint, 0.76f));
                Quad(g, c, d, h, Shade(tint, 0.83f));
                Quad(h, d, a, e, Shade(tint, 0.76f));
            }

            internal void Flame(Vector3 origin, float radius, float height, float lean, Color32 tint)
            {
                Vector3 a = origin + new Vector3(-radius, height * 0.24f, 0f);
                Vector3 b = origin + new Vector3(0f, height * 0.30f, radius);
                Vector3 c = origin + new Vector3(radius, height * 0.22f, 0f);
                Vector3 d = origin + new Vector3(0f, height * 0.28f, -radius);
                Vector3 tip = origin + new Vector3(lean, height, -lean * 0.5f);
                Triangle(a, b, tip, tint, emissive: true);
                Triangle(b, c, tip, tint, emissive: true);
                Triangle(c, d, tip, tint, emissive: true);
                Triangle(d, a, tip, tint, emissive: true);
                Triangle(b, a, origin, tint, emissive: true);
                Triangle(c, b, origin, tint, emissive: true);
                Triangle(d, c, origin, tint, emissive: true);
                Triangle(a, d, origin, tint, emissive: true);
            }

            internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Color32 tint, bool emissive = false)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                tint.a = emissive ? (byte)255 : (byte)0;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 point = vertices[start + i];
                    textureCoordinates.Add(new Vector2(point.x, point.z));
                    colors.Add(tint);
                    triangles.Add(start + i);
                }
            }

            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 tint)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                tint.a = 0;
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
                mesh.RecalculateNormals();
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
