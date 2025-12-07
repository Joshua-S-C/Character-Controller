using System;
using System.Numerics;
using Nyteshade.Modules.Maths;

namespace Nyteshade.Modules.Anim
{
    public struct BoneTransform
    {
        public Vector3 Translation;
        public Quaternion Rotation;
        public Vector3 Scale;

        public static BoneTransform Identity =>
            new()
            {
                Translation = Vector3.Zero,
                Rotation = Quaternion.Identity,
                Scale = Vector3.One
            };
    }


    public class SpatialPose
    {
        public BoneTransform[] LocalTransforms;
        
        public SpatialPose(int boneCount)
        {
            LocalTransforms = new BoneTransform[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                LocalTransforms[i] = BoneTransform.Identity;
            }
        }

      
        public static SpatialPose Identity(int boneCount)
        {
            return new SpatialPose(boneCount);
        }
        
        public static SpatialPose Invert(SpatialPose pose)
        {
            int boneCount = pose.LocalTransforms.Length;
            SpatialPose result = new SpatialPose(boneCount);
            var identity = BoneTransform.Identity;

            for (int i = 0; i < boneCount; i++)
            {
                var p = pose.LocalTransforms[i];
                var invRot = Quaternion.Inverse(p.Rotation);
                
                // Scale: 1 / p.Scale
                result.LocalTransforms[i].Scale = new Vector3(
                    CoreMaths.SafeDivide(identity.Scale.X, p.Scale.X),
                    CoreMaths.SafeDivide(identity.Scale.Y, p.Scale.Y),
                    CoreMaths.SafeDivide(identity.Scale.Z, p.Scale.Z)
                );
                
                // Rotation: Identity.Rotation * Inverse(p.Rotation)
                result.LocalTransforms[i].Rotation = invRot; // Since identity.Rotation is (0,0,0,1)
                
                // Translation: Inverse(p.Rotation) * (Identity.Translation - p.Translation)
                result.LocalTransforms[i].Translation = Vector3.Transform(-p.Translation, invRot);
            }
            return result;
        }

        public static SpatialPose Scale(SpatialPose pose, float factor)
        {
            int boneCount = pose.LocalTransforms.Length;
            // Create a matching identity pose
            var identity = SpatialPose.Identity(boneCount); 
            
            // Lerp(Identity, pose, factor)
            return SpatialPose.Lerp(identity, pose, factor);
        }
        
        public static SpatialPose Lerp(SpatialPose a, SpatialPose b, float t)
        {
            int boneCount = a.LocalTransforms.Length;
            SpatialPose result = new SpatialPose(boneCount);

            for (int i = 0; i < boneCount; i++)
            {
                result.LocalTransforms[i].Translation = Vector3.Lerp(a.LocalTransforms[i].Translation, b.LocalTransforms[i].Translation, t);
                result.LocalTransforms[i].Rotation = Quaternion.Slerp(a.LocalTransforms[i].Rotation, b.LocalTransforms[i].Rotation, t);
                result.LocalTransforms[i].Scale = Vector3.Lerp(a.LocalTransforms[i].Scale, b.LocalTransforms[i].Scale, t);
            }
            return result;
        }
        
        public static SpatialPose Concatenate(SpatialPose basePose, SpatialPose deltaPose)
        {
            int n = basePose.LocalTransforms.Length;
            var result = new SpatialPose(n);

            for (int i = 0; i < n; i++)
            {
                var a = basePose.LocalTransforms[i];   // REST (Base)
                var b = deltaPose.LocalTransforms[i];  // DELTA (Relative)

                // Scale
                result.LocalTransforms[i].Scale = a.Scale * b.Scale;
                
                // Rotation
                result.LocalTransforms[i].Rotation = a.Rotation * b.Rotation;
                
                // Translation
                result.LocalTransforms[i].Translation = a.Translation + Vector3.Transform(b.Translation, a.Rotation);
            }
            return result;
        }
        public static SpatialPose Deconcatenate(SpatialPose combinedPose, SpatialPose basePose)
        {
            int boneCount = basePose.LocalTransforms.Length;
            SpatialPose result = new SpatialPose(boneCount);

            for (int i = 0; i < boneCount; i++)
            {
                var combined = combinedPose.LocalTransforms[i];
                var baseT = basePose.LocalTransforms[i];

                var invRot = Quaternion.Inverse(baseT.Rotation);
                
                result.LocalTransforms[i].Scale =
                    new Vector3(
                        CoreMaths.SafeDivide(combined.Scale.X, baseT.Scale.X),
                        CoreMaths.SafeDivide(combined.Scale.Y, baseT.Scale.Y),
                        CoreMaths.SafeDivide(combined.Scale.Z, baseT.Scale.Z)
                    );
                
                result.LocalTransforms[i].Rotation = invRot * combined.Rotation;
                
                result.LocalTransforms[i].Translation =
                    Vector3.Transform(combined.Translation - baseT.Translation, invRot);
            }

            return result;
        }
        
        public static BoneTransform ConcatenateTransforms(BoneTransform a, BoneTransform b)
        {
            return new BoneTransform
            {
                Scale = a.Scale * b.Scale,
                Rotation = a.Rotation * b.Rotation,
                Translation = a.Translation + Vector3.Transform(b.Translation, a.Rotation)
            };
        }
    }
}