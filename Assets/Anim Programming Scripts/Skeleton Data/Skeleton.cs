using System;
using System.Collections.Generic;
using System.Numerics;
using NyteshadeGodot.Modules.Maths;

namespace Nyteshade.Modules.Anim
{
    public class Skeleton
    {
        // The root of our agnostic Transform hierarchy
        public Transform RootBone { get; private set; }

        // Flat list for easy bone access by index
        private readonly List<Transform> _boneList = new List<Transform>();

        private readonly Dictionary<string, int> _boneNameMap = new Dictionary<string, int>();

        // The T-Pose
        public SpatialPose BasePose { get; set; }

        // Inverse bind matrices.
        public Matrix4x4[] InverseBindMatrices { get; set; }

        // Stores the result of the last animation sample
        public SpatialPose CurrentLocalSpacePose { get; set; }

        public int BoneCount => _boneList.Count;

        public Skeleton(Transform root)
        {
            RootBone = root;
            BuildBoneList(root);

            // Initialize arrays
            BasePose = new SpatialPose(BoneCount);
            InverseBindMatrices = new Matrix4x4[BoneCount];
            CurrentLocalSpacePose = new SpatialPose(BoneCount); // Initialize

            for (int i = 0; i < BoneCount; i++)
            {
                InverseBindMatrices[i] = Matrix4x4.Identity;
            }
        }

        private void BuildBoneList(Transform current)
        {
            _boneNameMap[current.Name] = _boneList.Count;
            _boneList.Add(current);

            foreach (var child in current.Children)
            {
                BuildBoneList(child);
            }
        }

        public Transform GetBone(int index) => _boneList[index];

        public int GetBoneIndex(string name)
        {
            if (_boneNameMap.TryGetValue(name, out int index))
            {
                return index;
            }
            return -1;
        }

        public void UpdateFK(AnimationClip clip, ClipController controller)
        {
            // 1. Sample the current keyframe pose
            SpatialPose sampledPose = clip.Sample(controller.KeyframeTime, BoneCount);

            // 2. Combine it with the BasePose
            SpatialPose localPose = SpatialPose.Concatenate(BasePose, sampledPose);

            // 3. Store result
            CurrentLocalSpacePose = localPose;

            // 4. Apply local transforms to the hierarchy
            for (int i = 0; i < BoneCount; i++)
            {
                var lt = localPose.LocalTransforms[i];
                _boneList[i].Position = lt.Translation;
                _boneList[i].Rotation = lt.Rotation;
                _boneList[i].Scale = lt.Scale;
            }
        }

        // Direct FK apply (no sampling)
        public void UpdateFKDirect(SpatialPose pose)
        {
            CurrentLocalSpacePose = pose;

            for (int i = 0; i < BoneCount; i++)
            {
                var lt = pose.LocalTransforms[i];
                _boneList[i].Position = lt.Translation;
                _boneList[i].Rotation = lt.Rotation;
                _boneList[i].Scale = lt.Scale;
            }
        }


        /// <summary>
        /// Extracts an animation delta relative to the base pose (for additive clip baking)
        /// </summary>
        public SpatialPose ExtractDeltaFromCurrentPose()
        {
            return SpatialPose.Deconcatenate(CurrentLocalSpacePose, BasePose);
        }
    }
}