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
        private readonly Transform shadow;
        private readonly Mesh shadowMesh;
        private readonly Material shadowMaterial;
        private static readonly int RunnerCourse = Shader.PropertyToID("_RunnerCourse");
        private static readonly int ShadowTint = Shader.PropertyToID("_Color");
        private readonly Transform hips, spine, head;
        private readonly Vector3 restHips;
        private readonly Leg leftLeg, rightLeg;
        private readonly Transform leftArm, rightArm, leftElbow, rightElbow;

        internal bool HasGroundShadow => shadow != null;

        internal static GreedLastCorridorRunner Create(Transform stage, Texture2D contactShadow)
        {
            TextAsset asset = Resources.Load<TextAsset>("GreedLast/PyramidRunner");
            Shader shader = Resources.Load<Shader>("GreedLast/RunnerSurface");
            if (asset == null || shader == null || !shader.isSupported) return null;
            return new GreedLastCorridorRunner(stage, JsonUtility.FromJson<RunnerAsset>(asset.text), shader, contactShadow);
        }

        private GreedLastCorridorRunner(Transform stage, RunnerAsset data, Shader shader, Texture2D contactShadow)
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

            Shader shadowShader = Resources.Load<Shader>("GreedLast/RunnerGroundShadow");
            if (shadowShader != null && shadowShader.isSupported)
            {
                var ground = new GameObject("Greed runner ground shadow") { hideFlags = HideFlags.HideAndDontSave, layer = avatar.layer };
                shadow = ground.transform;
                shadow.SetParent(stage, false);
                shadowMesh = new Mesh { name = ground.name, hideFlags = HideFlags.HideAndDontSave };
                shadowMesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
                shadowMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                shadowMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                shadowMaterial = new Material(shadowShader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = contactShadow };
                ground.AddComponent<MeshFilter>().sharedMesh = shadowMesh;
                var groundRenderer = ground.AddComponent<MeshRenderer>();
                groundRenderer.sharedMaterial = shadowMaterial;
                groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
                groundRenderer.receiveShadows = false;
            }
        }

        internal void Pose(Vector3 position, Quaternion heading, float distance, bool running,
            GreedLastAvoidanceType response, float responseProgress, float responseDirection, bool missed)
        {
            float phase = Mathf.Repeat(distance / StrideMeters, 1f);
            float progress = Mathf.Clamp01(responseProgress);
            // Get into the action quickly, then leave time for the weight to return to the stride.
            float reaction = Mathf.SmoothStep(0f, 1f, progress / 0.29f)
                * Mathf.SmoothStep(1f, 0f, (progress - 0.46f) / 0.54f);
            float jump = !missed && response == GreedLastAvoidanceType.Jump ? reaction : 0f;
            float slide = !missed && response == GreedLastAvoidanceType.Slide ? reaction : 0f;
            float twist = !missed && response == GreedLastAvoidanceType.Twist ? reaction * responseDirection : 0f;
            float stumble = missed ? reaction : 0f;
            float landing = Mathf.Sin(Mathf.PI * Mathf.InverseLerp(0.63f, 1f, progress)) * jump * 0.19f;
            float height = jump * 0.74f - landing;
            float sideStep = twist * 0.27f + responseDirection * (jump * 0.18f + slide * 0.16f);
            Vector3 ground = position + heading * (Vector3.right * sideStep);
            root.SetLocalPositionAndRotation(ground + Vector3.up * height, heading);
            material.SetVector(RunnerCourse, new Vector4(sideStep, height, distance, 0f));

            float stride = running ? 1f - Mathf.Max(jump, slide) : 0f;
            float swing = Mathf.Cos((phase - 0.04f) * Mathf.PI * 2f) * stride;
            float strideDip = (0.075f + Mathf.Cos((phase - 0.10f) * Mathf.PI * 4f) * 0.036f) * stride;
            hips.localPosition = restHips + new Vector3(swing * 0.027f,
                -strideDip - slide * 0.47f - stumble * 0.11f - Mathf.Abs(twist) * 0.10f - landing * 0.5f,
                0.025f * stride - slide * 0.18f);
            hips.localRotation = Quaternion.Euler(slide * -12f, swing * 8f + twist * 15f, -swing * 4f - twist * 8f);
            spine.localRotation = Quaternion.Euler(12f + slide * 55f + stumble * 25f - jump * 13f,
                -swing * 14f + twist * 35f, twist * -15f);
            head.localRotation = Quaternion.Euler(-9f - slide * 26f, -twist * 31f, twist * 13f);
            bool rightLeads = responseDirection > 0.01f;
            leftArm.localRotation = Quaternion.Euler(swing * 35f - jump * (rightLeads ? 55f : 29f)
                + slide * 17f - stumble * 14f, -twist * 12f, -26f - Mathf.Abs(twist) * 13f);
            rightArm.localRotation = Quaternion.Euler(-swing * 35f - jump * (rightLeads ? 29f : 55f)
                + slide * 28f - stumble * 14f, -twist * 12f, 26f + Mathf.Abs(twist) * 13f);
            leftElbow.localRotation = Quaternion.Euler(-72f + swing * 15f + slide * 30f, -8f, 0f);
            rightElbow.localRotation = Quaternion.Euler(-72f - swing * 15f + slide * 37f, 8f, 0f);
            leftLeg.Pose(phase, running, jump, slide, twist, rear: rightLeads);
            rightLeg.Pose(Mathf.Repeat(phase + 0.5f, 1f), running, jump, slide, twist, rear: !rightLeads);

            if (shadow != null)
            {
                shadow.SetLocalPositionAndRotation(ground + heading * new Vector3(0.03f, 0.012f, -0.05f + slide * 0.13f), heading);
                shadow.localScale = new Vector3(0.79f + height * 0.54f, 1f, 1.22f + height * 0.59f + slide * 0.34f);
                shadowMaterial.SetColor(ShadowTint, new Color(0.066f, 0.048f, 0.038f, 0.58f - height * 0.42f));
            }
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(material);
            if (shadow != null)
            {
                UnityEngine.Object.Destroy(shadowMesh);
                UnityEngine.Object.Destroy(shadowMaterial);
                UnityEngine.Object.Destroy(shadow.gameObject);
            }
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

            internal void Pose(float phase, bool running, float jump, float slide, float twist, bool rear)
            {
                float travel = StrideMeters * GroundShare;
                float swing = Mathf.InverseLerp(GroundShare, 1f, phase);
                // The swing matches the planted foot's speed at lift-off and contact.
                float swingSpeed = -StrideMeters * (1f - GroundShare);
                float edgeSpeed = swingSpeed * swing * (1f - swing) * (1f - 2f * swing);
                float reach = phase < GroundShare ? travel * 0.5f - phase * StrideMeters
                    : Mathf.SmoothStep(-travel * 0.5f, travel * 0.5f, swing) + edgeSpeed;
                float swingArc = Mathf.Sin(swing * Mathf.PI);
                float lift = swingArc * swingArc * 0.29f;
                var target = bindFoot + new Vector3(0f, running ? lift : 0f, running ? reach : 0f);
                target = Vector3.Lerp(target, bindFoot + new Vector3(0f, rear ? 0.15f : 0.27f, rear ? -0.37f : 0.30f), jump);
                target = Vector3.Lerp(target, bindFoot + new Vector3(0f, 0f, rear ? -0.24f : 0.57f), slide);
                target.x -= twist * (0.14f - swingArc * 0.08f);
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
                float footPitch = running ? Mathf.Sin(swing * Mathf.PI * 2f) * swingArc * 26f : 0f;
                footPitch = Mathf.Lerp(footPitch, rear ? 24f : -15f, jump);
                footPitch = Mathf.Lerp(footPitch, rear ? 0f : -10f, slide);
                foot.rotation = root.rotation * Quaternion.Euler(footPitch, twist * 9f, 0f);
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
