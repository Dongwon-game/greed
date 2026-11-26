using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GreedLast
{
    internal sealed class GreedLastCorridorRunner : IDisposable
    {
        private const float StrideMeters = 2.32f;
        private const float GroundShare = 0.34f;

        private readonly Transform root;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Transform hips, spine, head;
        private readonly Vector3 restHips;
        private readonly Leg leftLeg, rightLeg;
        private readonly Transform leftArm, rightArm, leftElbow, rightElbow;

        internal static GreedLastCorridorRunner Create(Transform stage)
        {
            TextAsset asset = Resources.Load<TextAsset>("GreedLast/PyramidRunner");
            Shader shader = Resources.Load<Shader>("GreedLast/RunnerSurface");
            if (asset == null || shader == null || !shader.isSupported) return null;
            return new GreedLastCorridorRunner(stage, JsonUtility.FromJson<RunnerAsset>(asset.text), shader);
        }

        private GreedLastCorridorRunner(Transform stage, RunnerAsset data, Shader shader)
        {
            var avatar = new GameObject("Greed 3D runner") { hideFlags = HideFlags.HideAndDontSave, layer = stage.gameObject.layer };
            root = avatar.transform;
            root.SetParent(stage, false);
            var joints = new Transform[data.joints.Length];
            var jointByName = new Dictionary<string, Transform>(data.joints.Length, StringComparer.Ordinal);
            var binds = new Matrix4x4[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                BindJoint joint = data.joints[i];
                Transform bone = new GameObject(joint.name) { hideFlags = HideFlags.HideAndDontSave, layer = avatar.layer }.transform;
                bone.SetParent(joint.parent < 0 ? root : joints[joint.parent], false);
                Vector3 position = new Vector3(joint.x, joint.y, joint.z);
                bone.localPosition = joint.parent < 0 ? position
                    : position - new Vector3(data.joints[joint.parent].x, data.joints[joint.parent].y, data.joints[joint.parent].z);
                joints[i] = bone;
                jointByName.Add(joint.name, bone);
                binds[i] = Matrix4x4.Translate(-position);
            }

            int count = data.positions.Length / 3;
            var vertices = new Vector3[count];
            var colors = new Color32[count];
            var weights = new BoneWeight[count];
            for (int i = 0; i < count; i++)
            {
                int xyz = i * 3, jointsAt = i * 4;
                vertices[i] = new Vector3(data.positions[xyz], data.positions[xyz + 1], data.positions[xyz + 2]);
                colors[i] = new Color32(data.colors[xyz], data.colors[xyz + 1], data.colors[xyz + 2], 255);
                weights[i] = new BoneWeight
                {
                    boneIndex0 = data.influences[jointsAt], weight0 = data.strengths[jointsAt],
                    boneIndex1 = data.influences[jointsAt + 1], weight1 = data.strengths[jointsAt + 1],
                    boneIndex2 = data.influences[jointsAt + 2], weight2 = data.strengths[jointsAt + 2],
                    boneIndex3 = data.influences[jointsAt + 3], weight3 = data.strengths[jointsAt + 3]
                };
            }
            mesh = new Mesh { name = "Greed linen-clad runner", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(data.triangles, 0);
            mesh.bindposes = binds;
            mesh.boneWeights = weights;
            mesh.RecalculateNormals();
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var skin = avatar.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.sharedMaterial = material;
            skin.bones = joints;
            skin.rootBone = root;
            skin.localBounds = new Bounds(Vector3.up, new Vector3(2.5f, 3.5f, 2.8f));
            skin.quality = SkinQuality.Bone4;
            skin.shadowCastingMode = ShadowCastingMode.Off;

            hips = jointByName["root"];
            spine = jointByName["spine04"];
            head = jointByName["head"];
            restHips = hips.localPosition;
            leftLeg = new Leg(root, jointByName["upperleg01.L"], jointByName["lowerleg01.L"], jointByName["foot.L"]);
            rightLeg = new Leg(root, jointByName["upperleg01.R"], jointByName["lowerleg01.R"], jointByName["foot.R"]);
            leftArm = jointByName["upperarm01.L"];
            rightArm = jointByName["upperarm01.R"];
            leftElbow = jointByName["lowerarm01.L"];
            rightElbow = jointByName["lowerarm01.R"];
        }

        internal void Pose(Vector3 position, Quaternion heading, float distance, bool running,
            GreedLastAvoidanceType response, float responseProgress, float responseDirection, bool missed)
        {
            float phase = Mathf.Repeat(distance / StrideMeters, 1f);
            float swing = running ? Mathf.Sin((phase - 0.10f) * Mathf.PI * 2f) : 0f;
            float reaction = Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(responseProgress) * Mathf.PI));
            float jump = !missed && response == GreedLastAvoidanceType.Jump ? Mathf.Pow(reaction, 0.72f) : 0f;
            float slide = !missed && response == GreedLastAvoidanceType.Slide ? reaction : 0f;
            float twist = !missed && response == GreedLastAvoidanceType.Twist ? reaction * responseDirection : 0f;
            float stumble = missed ? reaction : 0f;
            root.SetLocalPositionAndRotation(position + heading * new Vector3(twist * 0.22f, jump * 0.77f, 0f), heading);

            float strideDip = running ? 0.09f - Mathf.Cos(phase * Mathf.PI * 4f) * 0.028f : 0f;
            hips.localPosition = restHips + new Vector3(swing * 0.026f,
                -strideDip - slide * 0.40f - stumble * 0.11f, -slide * 0.23f);
            hips.localRotation = Quaternion.Euler(0f, swing * 8f + twist * 19f, -swing * 3f);
            spine.localRotation = Quaternion.Euler(10f + slide * 45f + stumble * 18f - jump * 10f,
                -swing * 13f + twist * 42f, twist * -14f);
            head.localRotation = Quaternion.Euler(-6f - slide * 21f, -twist * 22f, twist * 7f);
            leftArm.localRotation = Quaternion.Euler(swing * 35f - jump * 44f + slide * 10f,
                0f, -30f - Mathf.Abs(twist) * 12f);
            rightArm.localRotation = Quaternion.Euler(-swing * 35f - jump * 32f + slide * 10f,
                0f, 30f + Mathf.Abs(twist) * 12f);
            leftElbow.localRotation = Quaternion.Euler(-71f + slide * 24f, -8f, 0f);
            rightElbow.localRotation = Quaternion.Euler(-71f + slide * 24f, 8f, 0f);
            leftLeg.Pose(phase, running, jump, slide, rear: false);
            rightLeg.Pose(Mathf.Repeat(phase + 0.5f, 1f), running, jump, slide, rear: true);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(material);
            UnityEngine.Object.Destroy(root.gameObject);
        }

        private sealed class Leg
        {
            private readonly Transform root, thigh, shin, foot;
            private readonly float thighLength, shinLength;
            private readonly Vector3 bindThigh, bindShin, bindFoot;

            internal Leg(Transform root, Transform thigh, Transform shin, Transform foot)
            {
                this.root = root;
                this.thigh = thigh;
                this.shin = shin;
                this.foot = foot;
                bindThigh = root.InverseTransformVector(shin.position - thigh.position);
                bindShin = root.InverseTransformVector(foot.position - shin.position);
                bindFoot = root.InverseTransformPoint(foot.position);
                thighLength = bindThigh.magnitude;
                shinLength = bindShin.magnitude;
            }

            internal void Pose(float phase, bool running, float jump, float slide, bool rear)
            {
                float travel = StrideMeters * GroundShare;
                float swing = Mathf.InverseLerp(GroundShare, 1f, phase);
                float reach = phase < GroundShare ? travel * 0.5f - phase * StrideMeters
                    : Mathf.Lerp(-travel * 0.5f, travel * 0.5f, Mathf.SmoothStep(0f, 1f, swing));
                float lift = Mathf.Sin(swing * Mathf.PI) * 0.23f;
                var target = bindFoot + new Vector3(0f, running ? lift : 0f, running ? reach : 0f);
                target = Vector3.Lerp(target, bindFoot + new Vector3(0f, rear ? 0.18f : 0.12f, rear ? -0.29f : 0.31f), jump);
                target = Vector3.Lerp(target, bindFoot + new Vector3(0f, 0f, rear ? -0.27f : 0.53f), slide);
                Vector3 hip = root.InverseTransformPoint(thigh.position);
                Vector3 axis = (target - hip).normalized;
                float span = Mathf.Clamp(Vector3.Distance(target, hip), Mathf.Abs(thighLength - shinLength) + 0.001f,
                    thighLength + shinLength - 0.001f);
                Vector3 kneeForward = Vector3.ProjectOnPlane(Vector3.forward, axis).normalized;
                float along = (thighLength * thighLength - shinLength * shinLength + span * span) / (2f * span);
                float bend = Mathf.Sqrt(Mathf.Max(0f, thighLength * thighLength - along * along));
                Vector3 knee = hip + axis * along + kneeForward * bend;
                thigh.rotation = root.rotation * Quaternion.FromToRotation(bindThigh, knee - hip);
                shin.rotation = root.rotation * Quaternion.FromToRotation(bindShin, hip + axis * span - knee);
                foot.rotation = root.rotation * Quaternion.Euler(running ? Mathf.Sin(swing * Mathf.PI) * -18f : 0f, 0f, 0f);
            }
        }

        [Serializable]
        private sealed class RunnerAsset
        {
            public BindJoint[] joints;
            public float[] positions, strengths;
            public int[] influences, triangles;
            public byte[] colors;
        }

        [Serializable]
        private sealed class BindJoint
        {
            public string name;
            public int parent;
            public float x, y, z;
        }
    }
}
