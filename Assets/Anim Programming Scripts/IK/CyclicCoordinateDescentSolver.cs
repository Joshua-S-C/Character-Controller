using System;
using System.Linq;
using System.Numerics;
using Nyteshade.Modules.Maths;

namespace Nyteshade.Modules.Anim
{
    /// <summary>
    /// Solves an IK chain using Cyclic Coordinate Descent (CCD).
    /// Now includes a Pole Vector (Plane Constraint) for 3-bone chains.
    /// </summary>
    public class CCDIK_Solver : IKSolver
    {
        public float Weight { get; set; } = 1.0f;

        private readonly int[] _boneIndices;
        private readonly int _endEffectorIndex;
        
        public int Iterations { get; set; } = 10;
        public float Tolerance { get; set; } = 0.01f; // 1cm in anim-space

        private Vector3 _worldSpaceTarget;
        
        // Fields for the pole vector
        private Vector3 _worldSpacePoleTarget;
        private bool _hasPoleTarget = false;

        public CCDIK_Solver(int[] boneChainIndices)
        {
            if (boneChainIndices == null || boneChainIndices.Length < 2)
            {
                throw new ArgumentException("CCD chain must have at least 2 bones.", nameof(boneChainIndices));
            }
            _boneIndices = boneChainIndices;
            _endEffectorIndex = boneChainIndices.Last();
        }

        public void SetTarget(Vector3 worldSpaceTarget)
        {
            _worldSpaceTarget = worldSpaceTarget;
        }

        // Method to set the pole target
        public void SetPoleTarget(Vector3 worldSpacePoleTarget)
        {
            _worldSpacePoleTarget = worldSpacePoleTarget;
            _hasPoleTarget = true; // Flag that we should use it this frame
        }

        public void Resolve(Skeleton skeleton)
        {
            // --- 1. Store Original Pose for Blending ---
            var originalPose = new SpatialPose(_boneIndices.Length);
            for(int i = 0; i < _boneIndices.Length; i++)
            {
                originalPose.LocalTransforms[i] = skeleton.CurrentLocalSpacePose.LocalTransforms[_boneIndices[i]];
            }

            // --- 2. Apply Pole Vector Constraint ---
            if (_hasPoleTarget && _boneIndices.Length >= 3)
            {
                ApplyPoleVector(skeleton);
                skeleton.UpdateFKDirect(skeleton.CurrentLocalSpacePose);
            }
            _hasPoleTarget = false; // Reset the flag

            // --- 3. Run CCD Iterations ---
            for (int iter = 0; iter < Iterations; iter++)
            {
                var effectorPos = skeleton.GetBone(_endEffectorIndex).GetLocalToWorldMatrix().Translation;
                if (Vector3.Distance(effectorPos, _worldSpaceTarget) < Tolerance)
                {
                    break;
                }

                // Loop *backwards* from the parent of the end-effector
                for (int i = _boneIndices.Length - 2; i >= 0; i--)
                {
                    int boneIndex = _boneIndices[i];
                    var bone = skeleton.GetBone(boneIndex);
                    
                    var boneWorldMatrix = bone.GetLocalToWorldMatrix();
                    var boneWorldPos = boneWorldMatrix.Translation;
                    
                    var effectorWorldPos = skeleton.GetBone(_endEffectorIndex).GetLocalToWorldMatrix().Translation;

                    var toEnd = Vector3.Normalize(effectorWorldPos - boneWorldPos);
                    var toTarget = Vector3.Normalize(_worldSpaceTarget - boneWorldPos);
                    
                    var ikRotation = CoreMaths.FromToRotation(toEnd, toTarget);
                    
                    Matrix4x4.Decompose(boneWorldMatrix, out _, out var boneWorldRot, out _);
                    var newWorldRotation = ikRotation * boneWorldRot;

                    Matrix4x4.Decompose(bone.Parent.GetLocalToWorldMatrix(), out _, out var parentWorldRot, out _);
                    var newLocalRotation = Quaternion.Inverse(parentWorldRot) * newWorldRotation;
                    
                    skeleton.CurrentLocalSpacePose.LocalTransforms[boneIndex].Rotation = newLocalRotation;
                    
                    skeleton.UpdateFKDirect(skeleton.CurrentLocalSpacePose);
                }
            }
            
            // --- 4. Blend back to Original Pose ---
            for (int i = 0; i < _boneIndices.Length; i++)
            {
                int boneIndex = _boneIndices[i];
                var originalRot = originalPose.LocalTransforms[i].Rotation;
                var ikRot = skeleton.CurrentLocalSpacePose.LocalTransforms[boneIndex].Rotation;
                
                skeleton.CurrentLocalSpacePose.LocalTransforms[boneIndex].Rotation = 
                    Quaternion.Slerp(originalRot, ikRot, Weight);
            }
        }
        
        private void ApplyPoleVector(Skeleton skeleton)
        {
            // We assume a 3-bone chain for this logic: Root, Mid, End
            int rootIndex = _boneIndices[0];
            int midIndex = _boneIndices[1];
            int endIndex = _endEffectorIndex;

            var rootBone = skeleton.GetBone(rootIndex);
            
            // 1. Get world positions
            var rootPos = rootBone.GetLocalToWorldMatrix().Translation;
            var midPos = skeleton.GetBone(midIndex).GetLocalToWorldMatrix().Translation;
            var endPos = skeleton.GetBone(endIndex).GetLocalToWorldMatrix().Translation;

            // 2. Calculate the current plane normal
            var currentArmVec = Vector3.Normalize(endPos - rootPos);
            var currentElbowVec = Vector3.Normalize(midPos - rootPos);
            var currentPlaneNormal = Vector3.Normalize(Vector3.Cross(currentArmVec, currentElbowVec));
            
            // 3. Calculate the target plane normal
            var targetElbowVec = Vector3.Normalize(_worldSpacePoleTarget - rootPos);
            var targetPlaneNormal = Vector3.Normalize(Vector3.Cross(currentArmVec, targetElbowVec));

            // 4. Find the rotation to get from current to target
            var poleRotation = CoreMaths.FromToRotation(currentPlaneNormal, targetPlaneNormal);
            
            // 5. Apply this rotation to the root bone
            Matrix4x4.Decompose(rootBone.Parent.GetLocalToWorldMatrix(), out _, out var parentWorldRot, out _);
            var rootLocalRot = skeleton.CurrentLocalSpacePose.LocalTransforms[rootIndex].Rotation;
            var rootWorldRot = parentWorldRot * rootLocalRot;
            
            var newWorldRot = poleRotation * rootWorldRot;
            
            var newLocalRot = Quaternion.Inverse(parentWorldRot) * newWorldRot;

            skeleton.CurrentLocalSpacePose.LocalTransforms[rootIndex].Rotation = newLocalRot;
        }

      
    }
}