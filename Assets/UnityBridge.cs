using System;
using System.Collections.Generic;
using System.Numerics;
using NyteshadeGodot.Modules.Maths;
using UnityEngine;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;
using Transform = NyteshadeGodot.Modules.Maths.Transform;

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


                if (parent >= 0)
                    boneMap[i].SetParent(boneMap[parent]);
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
                    Debug.LogError($"[Bridge] Failed to map Godot bone '{boneName}' (Godot index {i}) to Nyteshade skeleton.");
                    continue;
                }

                Transform rest = unitySkeleton.GetBoneRest(i);
                var restMatrix = ToNumericsMatrix(rest);

                if (!Matrix4x4.Invert(restMatrix, out Matrix4x4 invBind))
                {
                    Debug.LogError($"[Bridge] Failed to invert rest matrix for bone {unitySkeleton.bones[i].name}");
                    invBind = Matrix4x4.Identity;
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

        private static Matrix4x4 ToNumericsMatrix(Transform t)
        {
            return new Matrix4x4(
                t.Basis.X.X, t.Basis.X.Y, t.Basis.X.Z, 0,
                t.Basis.Y.X, t.Basis.Y.Y, t.Basis.Y.Z, 0,
                t.Basis.Z.X, t.Basis.Z.Y, t.Basis.Z.Z, 0,
                t.Origin.X,  t.Origin.Y,  t.Origin.Z,  1
            );
        }



        // ─────────────────────────────────────────────
        // Cross-Armature Animation Baking
        // ─────────────────────────────────────────────
        
        public static AnimationClip BuildClipFromAnimationPlayer(
            GameObject animRoot,
            Skeleton targetSkeleton,
            string animName = "Idle",
            float frameRate = 30f)
        {
            if (animRoot == null)
                throw new ArgumentNullException(nameof(animRoot));
            if (targetSkeleton == null)
                throw new ArgumentNullException(nameof(targetSkeleton));

            // 1. Find AnimationPlayer
            var animPlayer = FindAnimationPlayerRecursive(animRoot);
            if (animPlayer == null)
            {
                Debug.LogError("[Bridge] No AnimationPlayer found in animation GLB!");
                return new AnimationClip();
            }

            // 2. Find Animation
            var animNames = animPlayer.GetAnimationList();
            Animation anim = null;
            Debug.Log($"[Bridge] Available animations ({animNames.Length}):");
            foreach (var name in animNames) Debug.Log($"  - {name}");

            if (!string.IsNullOrEmpty(animName) && animPlayer.HasAnimation(animName))
            {
                anim = animPlayer.GetAnimation(animName);
                Debug.Log($"[Bridge] Using requested animation '{animName}'.");
            }
            else
            {
                string bestName = null;
                float bestLength = 0f;
                Debug.Log($"[Bridge] Fallback: Could not find '{animName}'. Searching for longest anim...");
                foreach (var name in animNames)
                {
                    var a = animPlayer.GetAnimation(name);
                    if (a.Length > bestLength) { bestName = name; bestLength = a.Length; }
                }

                if (bestName != null)
                {
                    animName = bestName;
                    anim = animPlayer.GetAnimation(animName);
                    Debug.Log($"[Bridge] Using longest animation '{animName}' ({anim.Length:F2}s).");
                }
                else
                {
                    Debug.LogError("[Bridge] No valid animation found!");
                    return new AnimationClip();
                }
            }

            // 3. Find the Skeleton3D obj *in the animation scene*
            Skeleton3D animSkeleton = animRoot.GetobjOrNull<Skeleton3D>("Armature/Skeleton3D") ??
                                      animRoot.GetobjOrNull<Skeleton3D>("Skeleton3D");
            
            if (animSkeleton == null)
            {
                Debug.LogError("[Bridge] No Skeleton3D found in animation scene to sample from!");
                return new AnimationClip();
            }
            
            // 4. Create a map of (CleanName -> Godot Bone Index) for the animation skeleton
            var animBoneNameMap = new Dictionary<string, int>();
            for (int i = 0; i < animSkeleton.bones.Length; i++)
            {
                animBoneNameMap[CleanName(animSkeleton.bones[i].name)] = i;
            }

            // 5. Compute duration
            float duration = anim.Length;
            if (duration <= 0.001f)
            {
                for (int i = 0; i < anim.GetTrackCount(); i++)
                {
                    int keyCount = anim.TrackGetKeyCount(i);
                    if (keyCount > 0)
                    {
                        float lastTime = (float)anim.TrackGetKeyTime(i, keyCount - 1);
                        if (lastTime > duration) duration = lastTime;
                    }
                }
                if (duration < 1e-3f) duration = 1f;
            }

            // 6. Bake frames
            var clip = new AnimationClip();
            float time = 0f;

            animPlayer.Play(animName);
            animPlayer.Pause();

            while (time <= duration + (1f / frameRate))
            {
                animPlayer.Seek(time, true);
                
                // This pose must match target skeleton 
                var absolutePose = new SpatialPose(targetSkeleton.BoneCount); 

                // Loop over target skeleton
                for (int i = 0; i < targetSkeleton.BoneCount; i++)
                {
                    var bone = targetSkeleton.GetBone(i);
                    
                    // Find the matching bone index in the animation's skeleton
                    if (animBoneNameMap.TryGetValue(bone.Name, out int animBoneIndex))
                    {
                        Transform bonePose = animSkeleton.GetBonePose(animBoneIndex);

                        // Convert Godot Transform to our BoneTransform
                        var gQuat = bonePose.Basis.GetRotationQuaternion();
                        var gScale = bonePose.Basis.Scale;

                        var t = new BoneTransform
                        {
                            Translation = new Vector3(bonePose.Origin.X, bonePose.Origin.Y, bonePose.Origin.Z),
                            Rotation = Quaternion.Normalize(new Quaternion(gQuat.X, gQuat.Y, gQuat.Z, gQuat.W)),
                            Scale = new Vector3(
                                Math.Abs(gScale.X) < 1e-4f ? 1 : gScale.X,
                                Math.Abs(gScale.Y) < 1e-4f ? 1 : gScale.Y,
                                Math.Abs(gScale.Z) < 1e-4f ? 1 : gScale.Z
                            )
                        };
                        absolutePose.LocalTransforms[i] = t;
                    }
                    else
                    {
                        if (bone.Name != "Root")
                            Debug.Log($"[Bridge] No bone named '{bone.Name}' found in anim skeleton.");
                        
                        absolutePose.LocalTransforms[i] = BoneTransform.Identity;
                    }
                }
                
                var deltaPose = SpatialPose.Deconcatenate(absolutePose, targetSkeleton.BasePose);
                clip.Keyframes.Add(new Keyframe { Time = time, Pose = deltaPose });

                time += 1f / frameRate;
            }

            clip.SortKeyframes();
            Debug.Log($"[Bridge] Cross-baked animation '{animName}' as delta poses ({clip.Keyframes.Count} frames, {clip.Duration:F2}s)");
            return clip;
        }

        // ─────────────────────────────────────────────
        // Pose & Skinning Application
        // ─────────────────────────────────────────────
        public static void ApplyBoneOverridesFromSkeleton(SkinnedMeshRenderer unitySkeleton, Nyteshade.Modules.Anim.Skeleton skeleton)
        {
            if (unitySkeleton == null || skeleton == null) return;

            // Map Godot's cleaned names to its original indices
            var nameToIndex = new Dictionary<string, int>(unitySkeleton.bones.Length);
            for (int gi = 0; gi < unitySkeleton.bones.Length; gi++)
            {
                nameToIndex[CleanName(unitySkeleton.GetBoneName(gi))] = gi;
            }

            // Loop the skeleton
            for (int i = 0; i < skeleton.BoneCount; i++)
            {
                var bone = skeleton.GetBone(i);
                
                if (!nameToIndex.TryGetValue(bone.Name, out int gidx))
                    continue; // Skips "Root"

                var world = bone.GetLocalToWorldMatrix();
                if (!Matrix4x4.Decompose(world, out var s, out var r, out var t))
                    continue;

                r = Quaternion.Normalize(r);
                s = new Vector3(
                    MathF.Max(MathF.Abs(s.X), 1e-4f),
                    MathF.Max(MathF.Abs(s.Y), 1e-4f),
                    MathF.Max(MathF.Abs(s.Z), 1e-4f)
                );

                var basis = new Godot.Basis(new Godot.Quaternion(r.X, r.Y, r.Z, r.W))
                    .Scaled(new Godot.Vector3(s.X, s.Y, s.Z));

                var xform = new Godot.Transform(basis, new Godot.Vector3(t.X, t.Y, t.Z));

                if (float.IsNaN(basis.X.X) || MathF.Abs(basis.Determinant()) < 1e-6f)
                {
                    Debug.LogError($"[WARN] Degenerate matrix for bone {bone.Name}, resetting to identity.");
                    xform = Godot.Transform.Identity;
                }

                unitySkeleton.SetBoneGlobalPoseOverride(gidx, xform, 1.0f, false);
            }
        }
        
    }
}