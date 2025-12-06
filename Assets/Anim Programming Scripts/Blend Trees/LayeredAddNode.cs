using System.Numerics;

namespace Nyteshade.Modules.Anim
{
    /// <summary>
    /// A "branch" node that additively blends a second pose on top of
    /// a base pose, using a per-bone weight mask.
    /// </summary>
    public class LayeredAddNode : IAnimNode
    {
        private readonly IAnimNode _baseNode;
        private readonly IAnimNode _additiveNode;
        
        public float[] BoneWeights { get; private set; }
        
      
        public float Weight { get; set; } = 1.0f;

        public LayeredAddNode(IAnimNode baseNode, IAnimNode additiveNode, int boneCount)
        {
            _baseNode = baseNode;
            _additiveNode = additiveNode;
            
            // Initialize all weights to 0 (no effect)
            BoneWeights = new float[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                BoneWeights[i] = 0.0f;
            }
        }
        
        public void ScriptUpdate(float deltaTime)
        {
            _baseNode.ScriptUpdate(deltaTime);
            _additiveNode.ScriptUpdate(deltaTime);
        }
        
        public SpatialPose Evaluate(int boneCount)
        {
            // 1. "Pull" the poses from the children
            var basePose = _baseNode.Evaluate(boneCount);
            var additivePose = _additiveNode.Evaluate(boneCount);

            // 2. Start with a copy of the base pose
            var resultPose = new SpatialPose(boneCount);
            
            // 3. Loop over every bone and apply the layered blend
            for (int i = 0; i < boneCount; i++)
            {
                float finalWeight = BoneWeights[i] * Weight;

                if (finalWeight < 0.001f)
                {
                    // No effect, just use the base pose
                    resultPose.LocalTransforms[i] = basePose.LocalTransforms[i];
                }
                else
                {
                    var addedTransform = SpatialPose.ConcatenateTransforms
                    (
                        basePose.LocalTransforms[i], 
                        additivePose.LocalTransforms[i]
                    );

                    // Blend from the base to the full additive pose
                    var baseT = basePose.LocalTransforms[i];
                    
                    resultPose.LocalTransforms[i].Rotation = Quaternion.Slerp(baseT.Rotation, addedTransform.Rotation, finalWeight);
                    resultPose.LocalTransforms[i].Translation = Vector3.Lerp(baseT.Translation, addedTransform.Translation, finalWeight);
                    resultPose.LocalTransforms[i].Scale = Vector3.Lerp(baseT.Scale, addedTransform.Scale, finalWeight);
                }
            }

            return resultPose;
        }
        
        public void Reset()
        {
            _baseNode.Reset();
            _additiveNode.Reset();
        }
    }
}