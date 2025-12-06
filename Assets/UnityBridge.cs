using NyteshadeGodot.Modules.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using Quaternion = System.Numerics.Quaternion;
using Transform = NyteshadeGodot.Modules.Maths.Transform;
using Vector3 = System.Numerics.Vector3;

namespace Nyteshade.Modules.Anim
{
    public static class UnityBridge
    {
        // ─────────────────────────────────────────────
        // Recursive obj Utilities
        // ─────────────────────────────────────────────
        public static AnimationPlayer FindAnimationPlayerRecursive(GameObject gameObject)
        {
            return gameObject.GetComponentInChildren<AnimationPlayer>();
        }

        public static string CleanName(string name)
        {
            if (name == null) return "";
            return name
                .Replace("mixamorig:", "mixamorig_")
                .Replace(":", "_")
                .Replace("Armature|", "")
                .Trim();
        }


        // Shouldn't need this, just use built in Transform
        // ─────────────────────────────────────────────
        // Skeleton Builders
        // ─────────────────────────────────────────────
        public static Transform BuildTransformFromUnity(GameObject obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            Transform t = Transform.UnityToNyteshadeTransform(obj.transform);

            foreach (UnityEngine.Transform child in obj.GetComponentsInChildren<UnityEngine.Transform>())
            {
                Transform childTransform = BuildTransformFromUnity(child.gameObject);
                childTransform.SetParent(t);
            }

            return t;
        }

        // TODO This is a good amount of the adaptation
        public static Skeleton BuildSkeleton(GameObject root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            SkinnedMeshRenderer unitySkeleton = root.transform.parent.GetComponentInChildren<SkinnedMeshRenderer>();

            if (unitySkeleton == null)
            {
                Debug.LogError("[Bridge] No Skeleton3D found. Falling back to transform hierarchy.");
                var fallbackRoot = BuildTransformFromUnity(root);
                return new Skeleton(fallbackRoot);
            }

            var rootTransform = new NyteshadeGodot.Modules.Maths.Transform { Name = "Root" };
            var boneMap = new Dictionary<int, NyteshadeGodot.Modules.Maths.Transform>();

            // Build bone transforms
            for (int i = 0; i < unitySkeleton.bones.Length; i++)
            {
                // TODO Bind transform of the bone
                Transform rest = Transform.UnityToNyteshadeTransform(unitySkeleton.bones[i].transform);

                boneMap[i] = new NyteshadeGodot.Modules.Maths.Transform
                {
                    Name = rest.Name,
                    Position = rest.Position,
                    Rotation = rest.Rotation,
                    Scale = rest.Scale,
                };
            }

            // Wire up hierarchy
            for (int i = 0; i < unitySkeleton.bones.Length; i++)
            {

                // TODO 

                boneMap.TryGetValue(unitySkeleton.bones[i].transform.parent.GetInstanceID(), out Transform parent);


                if (parent != null)
                    boneMap[i].SetParent(parent);
                else
                    boneMap[i].SetParent(rootTransform);
            }

            // Create and populate Skeleton
            var skeleton = new Skeleton(rootTransform);

            var nameToNyteshadeIndex = new Dictionary<string, int>(skeleton.BoneCount);
            for (int nIdx = 0; nIdx < skeleton.BoneCount; nIdx++)
            {
                nameToNyteshadeIndex[skeleton.GetBone(nIdx).Name] = nIdx;
            }

            for (int i = 0; i < unitySkeleton.bones.Length; i++)
            {
                // Find matching index
                string boneName = CleanName(unitySkeleton.bones[i].name);
                if (!nameToNyteshadeIndex.TryGetValue(boneName, out int nIdx))
                {
                    Debug.LogError($"[Bridge] Failed to map bone '{boneName}' (Index {i}) to Nyteshade skeleton.");
                    continue;
                }

                UnityEngine.Matrix4x4 rest = unitySkeleton.sharedMesh.bindposes[i];
                var restMatrix = ToNumericsMatrix(rest);

                if (!System.Numerics.Matrix4x4.Invert(restMatrix, out System.Numerics.Matrix4x4 invBind))
                {
                    Debug.LogError($"[Bridge] Failed to invert rest matrix for bone {unitySkeleton.bones[i].name}");
                    invBind = System.Numerics.Matrix4x4.Identity;
                }

                skeleton.InverseBindMatrices[nIdx] = invBind;

                var t = boneMap[i];

                skeleton.BasePose.LocalTransforms[nIdx] = new BoneTransform
                {
                    Translation = t.Position,
                    Rotation = t.Rotation,
                    Scale = t.Scale
                };
            }

            Debug.Log($"[Bridge] Built skeleton with {unitySkeleton.bones.Length} bones and BasePose stored.");
            return skeleton;
        }

        private static System.Numerics.Matrix4x4 ToNumericsMatrix(UnityEngine.Matrix4x4 m)
        {
            return new System.Numerics.Matrix4x4(
                m.m00, m.m01, m.m02, m.m03,
                m.m10, m.m11, m.m12, m.m13,
                m.m20, m.m21, m.m22, m.m23,
                m.m30, m.m31, m.m32, m.m33
            );
        }



        // ─────────────────────────────────────────────
        // Cross-Armature Animation Baking
        // ─────────────────────────────────────────────

        public static AnimationClip BuildClipFromFbx(
    GameObject animRoot,
    Skeleton targetSkeleton,
    float frameRate = 30f,
    string animName = null)
        {
            if (animRoot == null)
                throw new ArgumentNullException(nameof(animRoot));
            if (targetSkeleton == null)
                throw new ArgumentNullException(nameof(targetSkeleton));

            // ---------------------------------------
            // 1. Locate Unity AnimationClips
            // ---------------------------------------
            var animator = animRoot.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("[Bridge] No Animator found in FBX!");
                return new AnimationClip();
            }

            RuntimeAnimatorController rac = animator.runtimeAnimatorController;
            if (rac == null)
            {
                Debug.LogError("[Bridge] Animator has no controller; FBX must import clips.");
                return new AnimationClip();
            }

            var unityClips = rac.animationClips;
            if (unityClips == null || unityClips.Length == 0)
            {
                Debug.LogError("[Bridge] FBX contains no AnimationClips.");
                return new AnimationClip();
            }

            // pick requested animation or default
            UnityEngine.AnimationClip unityClip = null;

            if (!string.IsNullOrEmpty(animName))
            {
                unityClip = unityClips.FirstOrDefault(c => c.name == animName);
            }

            if (unityClip == null)
            {
                // fallback — longest clip
                unityClip = unityClips.OrderByDescending(c => c.length).First();
                animName = unityClip.name;
            }

            Debug.Log($"[Bridge] Using FBX AnimationClip: {animName}");

            // ---------------------------------------
            // 2. Find bones in FBX skeleton
            // ---------------------------------------
            var smr = animRoot.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null)
            {
                Debug.LogError("[Bridge] FBX has no SkinnedMeshRenderer → cannot sample skeleton.");
                return new AnimationClip();
            }

            UnityEngine.Transform[] animBones = smr.bones;

            // map CleanName → unity bone index
            var animBoneNameMap = new Dictionary<string, int>(animBones.Length);
            for (int i = 0; i < animBones.Length; i++)
            {
                animBoneNameMap[CleanName(animBones[i].name)] = i;
            }

            // ---------------------------------------
            // 3. Prepare sampling
            // ---------------------------------------
            float duration = unityClip.length > 0 ? unityClip.length : 1f;

            var bakedClip = new AnimationClip();
            float time = 0f;

            Animator animatorForSampling = animator;

            // ---------------------------------------
            // 4. Bake frame-by-frame
            // ---------------------------------------
            while (time <= duration + (1f / frameRate))
            {
                unityClip.SampleAnimation(animRoot, time);

                var absolutePose = new SpatialPose(targetSkeleton.BoneCount);

                // Loop through Nyteshade target skeleton
                for (int i = 0; i < targetSkeleton.BoneCount; i++)
                {
                    var bone = targetSkeleton.GetBone(i);

                    // Find matching FBX bone
                    if (animBoneNameMap.TryGetValue(bone.Name, out int unityBoneIndex))
                    {
                        var uBone = animBones[unityBoneIndex];

                        // Unity transform → BoneTransform
                        var t = new BoneTransform
                        {
                            Translation = new Vector3(uBone.localPosition.x, uBone.localPosition.y, uBone.localPosition.z),
                            Rotation = new Quaternion(uBone.localRotation.x, uBone.localRotation.y, uBone.localRotation.z, uBone.localRotation.w),
                            Scale = new Vector3(uBone.localScale.x, uBone.localScale.y, uBone.localScale.z)
                        };

                        absolutePose.LocalTransforms[i] = t;
                    }
                    else
                    {
                        if (bone.Name != "Root")
                            Debug.Log($"[Bridge] No FBX bone named '{bone.Name}'.");

                        absolutePose.LocalTransforms[i] = BoneTransform.Identity;
                    }
                }

                // Compute delta vs BasePose
                var deltaPose = SpatialPose.Deconcatenate(absolutePose, targetSkeleton.BasePose);
                bakedClip.Keyframes.Add(new Keyframe { Time = time, Pose = deltaPose });

                time += 1f / frameRate;
            }

            bakedClip.SortKeyframes();

            Debug.Log($"[Bridge] Baked FBX animation '{animName}' ({bakedClip.Keyframes.Count} frames, {bakedClip.Duration:F2}s)");
            return bakedClip;
        }

        // ─────────────────────────────────────────────
        // Pose & Skinning Application
        // ─────────────────────────────────────────────
        public static void ApplyBoneOverridesFromSkeleton(SkinnedMeshRenderer unitySkeleton, Nyteshade.Modules.Anim.Skeleton skeleton)
        {
            if (!unitySkeleton || skeleton == null)
                return;

            UnityEngine.Transform[] unityBones = unitySkeleton.bones;

            // Build dictionary from cleaned bone names → Unity bone Transform
            var nameToUnityBone = new Dictionary<string, UnityEngine.Transform>(unityBones.Length);
            for (int i = 0; i < unityBones.Length; i++)
            {
                var ub = unityBones[i];
                if (ub != null)
                    nameToUnityBone[CleanName(ub.name)] = ub;
            }

            // Loop Nyteshade bones
            for (int i = 0; i < skeleton.BoneCount; i++)
            {
                var bone = skeleton.GetBone(i);

                if (!nameToUnityBone.TryGetValue(bone.Name, out UnityEngine.Transform unityBone))
                    continue; // skip "Root" or bones not in the renderer

                // Get Nyteshade world transform matrix (System.Numerics.Matrix4x4)
                var world = bone.GetLocalToWorldMatrix();

                if (!System.Numerics.Matrix4x4.Decompose(world, out var s, out var r, out var t))
                    continue;

                // sanitize scale
                s = new System.Numerics.Vector3(
                    MathF.Max(MathF.Abs(s.X), 1e-4f),
                    MathF.Max(MathF.Abs(s.Y), 1e-4f),
                    MathF.Max(MathF.Abs(s.Z), 1e-4f)
                );

                // normalize rotation
                r = System.Numerics.Quaternion.Normalize(r);

                // Convert Numerics → Unity
                UnityEngine.Vector3 unityPos = new UnityEngine.Vector3(t.X, t.Y, t.Z);
                UnityEngine.Quaternion unityRot = new UnityEngine.Quaternion(r.X, r.Y, r.Z, r.W);
                UnityEngine.Vector3 unityScale = new UnityEngine.Vector3(s.X, s.Y, s.Z);

                // Apply the world-space pose to the Unity bone
                SetUnityBoneWorldTRS(unityBone, unityPos, unityRot, unityScale);
            }
        }

        private static void SetUnityBoneWorldTRS(UnityEngine.Transform bone,UnityEngine.Vector3 worldPos,UnityEngine.Quaternion worldRot,UnityEngine.Vector3 worldScale)
        {
            UnityEngine.Transform parent = bone.parent;

            if (parent == null)
            {
                // Direct world assignment
                bone.position = worldPos;
                bone.rotation = worldRot;
                bone.localScale = worldScale;
                return;
            }

            // Convert world → local using the parent
            bone.localPosition =
                parent.InverseTransformPoint(worldPos);

            bone.localRotation =
                UnityEngine.Quaternion.Inverse(parent.rotation) * worldRot;

            // Scale is trickiest: convert world scale → local scale
            UnityEngine.Vector3 parentScale = parent.lossyScale;
            bone.localScale = new UnityEngine.Vector3(
                worldScale.x / (parentScale.x == 0 ? 1 : parentScale.x),
                worldScale.y / (parentScale.y == 0 ? 1 : parentScale.y),
                worldScale.z / (parentScale.z == 0 ? 1 : parentScale.z)
            );
        }

    }
}