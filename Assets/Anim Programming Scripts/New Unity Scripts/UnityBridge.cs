using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using Nyteshade.Modules.Anim;
using Nyteshade.Modules.Maths;

using UnityVector3 = UnityEngine.Vector3;
using UnityQuaternion = UnityEngine.Quaternion;
using UnityMatrix4x4 = UnityEngine.Matrix4x4;
using NumVector3 = System.Numerics.Vector3;
using NumQuaternion = System.Numerics.Quaternion;
using NumMatrix4x4 = System.Numerics.Matrix4x4;
using MathTransform = Nyteshade.Modules.Maths.Transform;
using Transform = UnityEngine.Transform;
using UnityVector4 = UnityEngine.Vector4;

namespace Nyteshade.Modules.Anim.Unity_Specific
{
    public static class UnityBridge
    {
        // -------------------------------------------------------------
        //  Conversion Helpers
        // -------------------------------------------------------------
        public static NumVector3 ToNumerics(UnityVector3 v) => new NumVector3(v.x, v.y, v.z);
        public static UnityVector3 ToUnity(NumVector3 v) => new UnityVector3(v.X, v.Y, v.Z);

        public static NumQuaternion ToNumerics(UnityQuaternion q) =>
            new NumQuaternion(q.x, q.y, q.z, q.w);

        public static UnityQuaternion ToUnity(NumQuaternion q) =>
            new UnityQuaternion(q.X, q.Y, q.Z, q.W);

        public static NumMatrix4x4 ToNumerics(UnityMatrix4x4 m) =>
            new NumMatrix4x4(
                m.m00, m.m01, m.m02, m.m03,
                m.m10, m.m11, m.m12, m.m13,
                m.m20, m.m21, m.m22, m.m23,
                m.m30, m.m31, m.m32, m.m33
            );

        public static UnityMatrix4x4 ToUnity(NumMatrix4x4 m)
        {
            UnityMatrix4x4 u = new UnityMatrix4x4();
            u.SetRow(0, new UnityVector4(m.M11, m.M12, m.M13, m.M14));
            u.SetRow(1, new UnityVector4(m.M21, m.M22, m.M23, m.M24));
            u.SetRow(2, new UnityVector4(m.M31, m.M32, m.M33, m.M34));
            u.SetRow(3, new UnityVector4(m.M41, m.M42, m.M43, m.M44));
            return u;
        }

        public static string CleanName(string name)
        {
            if (name == null) return string.Empty;

            return name
                .Replace("mixamorig:", "mixamorig_")
                .Replace(":", "_")
                .Replace("Armature|", "")
                .Trim();
        }

        // -------------------------------------------------------------
        //  Hierarchy Search
        // -------------------------------------------------------------
        public static SkinnedMeshRenderer FindSkinnedMeshRendererRecursive(GameObject root)
        {
            if (root == null) return null;

            var smr = root.GetComponent<SkinnedMeshRenderer>();
            if (smr != null) return smr;

            foreach (Transform child in root.transform)
            {
                var result = FindSkinnedMeshRendererRecursive(child.gameObject);
                if (result != null) return result;
            }

            return null;
        }

        // -------------------------------------------------------------
        //  Transform Hierarchy Builder
        // -------------------------------------------------------------
        public static MathTransform BuildTransformFromUnity(Transform unityTransform)
        {
            var t = new MathTransform
            {
                Name     = CleanName(unityTransform.name),
                Position = ToNumerics(unityTransform.localPosition),
                Rotation = ToNumerics(unityTransform.localRotation),
                Scale    = ToNumerics(unityTransform.localScale)
            };

            foreach (Transform child in unityTransform)
            {
                var childT = BuildTransformFromUnity(child);
                childT.SetParent(t);
            }

            return t;
        }

        // -------------------------------------------------------------
        //  Skeleton Builder
        // -------------------------------------------------------------
        public static Skeleton BuildSkeletonFromSkinnedMesh(SkinnedMeshRenderer smr)
        {
            var unityBones = smr.bones;
            if (unityBones == null || unityBones.Length == 0)
            {
                Debug.LogError("[UnityBridge] No bones in SkinnedMeshRenderer!");
                return new Skeleton(BuildTransformFromUnity(smr.transform));
            }

            var root = new MathTransform { Name = "Root" };
            var boneMap = new Dictionary<Transform, MathTransform>();

            // Build MathTransforms
            foreach (var ub in unityBones)
            {
                var t = new MathTransform
                {
                    Name     = CleanName(ub.name),
                    Position = ToNumerics(ub.localPosition),
                    Rotation = ToNumerics(ub.localRotation),
                    Scale    = ToNumerics(ub.localScale)
                };

                boneMap[ub] = t;
            }

            // Wire hierarchy
            foreach (var ub in unityBones)
            {
                var nt = boneMap[ub];
                var parent = ub.parent;

                if (parent != null && boneMap.TryGetValue(parent, out var parentNt))
                    nt.SetParent(parentNt);
                else
                    nt.SetParent(root);
            }

            // Build Skeleton
            var skeleton = new Skeleton(root);

            // Assign bindposes and base pose
            var bindposes = smr.sharedMesh.bindposes;
            for (int i = 0; i < unityBones.Length; i++)
            {
                string name = CleanName(unityBones[i].name);
                int idx = skeleton.GetBoneIndex(name);
                if (idx < 0) continue;

                skeleton.InverseBindMatrices[idx] = ToNumerics(bindposes[i]);

                skeleton.BasePose.LocalTransforms[idx] = new BoneTransform
                {
                    Translation = ToNumerics(unityBones[i].localPosition),
                    Rotation = NumQuaternion.Normalize(ToNumerics(unityBones[i].localRotation)),
                    Scale = ToNumerics(unityBones[i].localScale)
                };
            }

            Debug.Log($"[UnityBridge] Built skeleton with {skeleton.BoneCount} bones.");
            return skeleton;
        }

        // -------------------------------------------------------------
        //  Animation Baking (FBX only, no Animator)
        // -------------------------------------------------------------
        public static AnimationClip BuildClipFromUnityClip(
            GameObject rigPrefab,
            UnityEngine.AnimationClip unityClip,
            Skeleton targetSkeleton,
            float frameRate = 30f)
        {
            var preview = UnityEngine.Object.Instantiate(rigPrefab);
            preview.hideFlags = HideFlags.HideAndDontSave;

            var allT = preview.GetComponentsInChildren<Transform>(true);
            var nameToBone = new Dictionary<string, Transform>();
            foreach (var t in allT)
                nameToBone[CleanName(t.name)] = t;

            var baked = new AnimationClip();

            float duration = Mathf.Max(unityClip.length, 1e-3f);
            float dt = 1f / frameRate;

            float time = 0f;
            while (time <= duration + dt * 0.5f)
            {
                unityClip.SampleAnimation(preview, time);

                var absolutePose = new SpatialPose(targetSkeleton.BoneCount);

                for (int i = 0; i < targetSkeleton.BoneCount; i++)
                {
                    var bone = targetSkeleton.GetBone(i);

                    if (nameToBone.TryGetValue(bone.Name, out var tbone))
                    {
                        absolutePose.LocalTransforms[i] = new BoneTransform
                        {
                            Translation = ToNumerics(tbone.localPosition),
                            Rotation    = NumQuaternion.Normalize(ToNumerics(tbone.localRotation)),
                            Scale       = ToNumerics(tbone.localScale)
                        };
                    }
                    else
                        absolutePose.LocalTransforms[i] = BoneTransform.Identity;
                }

                // Convert absolute pose --> delta pose
                var delta = SpatialPose.Deconcatenate(absolutePose, targetSkeleton.BasePose);

                baked.Keyframes.Add(new Keyframe { Time = time, Pose = delta });

                time += dt;
            }

            baked.SortKeyframes();
            UnityEngine.Object.Destroy(preview);

            Debug.Log($"[UnityBridge] Baked '{unityClip.name}' → {baked.Keyframes.Count} frames.");
            return baked;
        }

        // -------------------------------------------------------------
        //  Applying animation back to Unity bones
        // -------------------------------------------------------------
       public static void ApplyBoneOverridesToUnity(
    SkinnedMeshRenderer smr,
    Skeleton skeleton)
{
    if (smr == null || skeleton == null) return;

    var unityBones = smr.bones;
    if (unityBones == null || unityBones.Length == 0) return;

    // Map cleaned bone names --> Unity Transforms
    var nameToUnityBone = new Dictionary<string, Transform>(unityBones.Length);
    foreach (var ub in unityBones)
    {
        if (ub == null) continue;
        nameToUnityBone[CleanName(ub.name)] = ub;
    }

    for (int i = 0; i < skeleton.BoneCount; i++)
    {
        var bone = skeleton.GetBone(i);
        string bn = bone.Name;

        // Skip if Unity has no matching bone
        if (!nameToUnityBone.TryGetValue(bn, out var unityBone))
            continue;

        // --- 1. WORLD matrix from Nyteshade ---
        NumMatrix4x4 world = bone.GetLocalToWorldMatrix(); // numerics world matrix

        // --- 2. PARENT WORLD from Nyteshade hierarchy ---
        NumMatrix4x4 parentWorld =
            (bone.Parent != null)
                ? bone.Parent.GetLocalToWorldMatrix()
                : NumMatrix4x4.Identity;

        // --- 3. Compute parent^-1 ---
        if (!NumMatrix4x4.Invert(parentWorld, out var parentInv))
            parentInv = NumMatrix4x4.Identity;

        // --- 4. LOCAL matrix = world * parent^-1 ---
        NumMatrix4x4 local = world * parentInv;

        // --- 5. Decompose into T / R / S ---
        if (!NumMatrix4x4.Decompose(local, out var s, out var r, out var t))
            continue;

        r = NumQuaternion.Normalize(r);

        // Clamp zero-scale issues (Unity explodes on 0 scale)
        s = new NumVector3(
            MathF.Max(MathF.Abs(s.X), 1e-4f),
            MathF.Max(MathF.Abs(s.Y), 1e-4f),
            MathF.Max(MathF.Abs(s.Z), 1e-4f)
        );

        // --- 6. APPLY TO UNITY BONE (LOCAL SPACE) ---
        unityBone.localPosition = ToUnity(t);
        unityBone.localRotation = ToUnity(r);
        unityBone.localScale    = ToUnity(s);
    }
}


    }
}
