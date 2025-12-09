using System;
using System.Numerics;
using Nyteshade.Modules.Maths;

namespace Nyteshade.Modules.Anim
{
    public class LookAtIKSolver : IKSolver
    {
        public float Weight { get; set; } = 1.0f;

        private readonly int _boneIndex;
        private readonly Vector3 _localForwardAxis; 
        private Vector3 _worldSpaceTarget;

        /// <summary>
        /// Creates a new Look-At solver.
        /// </summary>
        /// <param name="boneIndex">The index of the bone to rotate (e.g., the neck).</param>
        /// <param name="localForwardAxis">The bone's *local* axis that represents "forward" (e.g., -Vector3.UnitZ for Mixamo).</param>
        public LookAtIKSolver(int boneIndex, Vector3 localForwardAxis)
        {
            _boneIndex = boneIndex;
            _localForwardAxis = Vector3.Normalize(localForwardAxis);
        }

        public void SetTarget(Vector3 worldSpaceTarget)
        {
            _worldSpaceTarget = worldSpaceTarget;
        }

        public void Resolve(Skeleton skeleton)
        {
            var bone = skeleton.GetBone(_boneIndex);
            if (bone == null || bone.Parent == null) return;

            // 1. Get the parent's world matrix
            Matrix4x4 parentWorldMatrix = bone.Parent.GetLocalToWorldMatrix();
            Matrix4x4.Decompose(parentWorldMatrix, out _, out var parentWorldRotation, out _);

            // 2. Get the bone's world position (the "eye")
            Vector3 boneWorldPos = bone.GetLocalToWorldMatrix().Translation;

            // 3. Get the desired "forward" vector in WORLD space
            Vector3 worldForward = Vector3.Normalize(_worldSpaceTarget - boneWorldPos);

            // 4. Transform this world vector into the PARENT'S LOCAL space
            Vector3 localTargetVector = Vector3.Transform(worldForward, Quaternion.Inverse(parentWorldRotation));

            // 5. Calculate the rotation required to aim the bone's local "forward" axis
            Quaternion desiredLocalRotation = CoreMaths.FromToRotation(_localForwardAxis, localTargetVector);

            // 6. Get current animated rotation for blending
            var currentLocalRotation = skeleton.CurrentLocalSpacePose.LocalTransforms[_boneIndex].Rotation;
            
            // 7. Slerp between the original animation and our new IK rotation
            var finalRotation = Quaternion.Slerp(currentLocalRotation, desiredLocalRotation, Weight);

            // 8. Apply the final result back to the pose buffer
            skeleton.CurrentLocalSpacePose.LocalTransforms[_boneIndex].Rotation = finalRotation;
        }
    }
}

