namespace Nyteshade.Modules.Anim
{
    
    public class LerpNode : IAnimNode
    {
        private readonly IAnimNode _nodeA;
        private readonly IAnimNode _nodeB;
      
        public float BlendWeight { get; set; } = 0.0f;

        public LerpNode(IAnimNode nodeA, IAnimNode nodeB)
        {
            _nodeA = nodeA;
            _nodeB = nodeB;
        }
        
        public void Update(float deltaTime)
        {
            _nodeA.Update(deltaTime);
            _nodeB.Update(deltaTime);
        }
        
        public SpatialPose Evaluate(int boneCount)
        {
            // 1. "Pull" the poses from the children
            var poseA = _nodeA.Evaluate(boneCount);
            var poseB = _nodeB.Evaluate(boneCount);

            // 2. Blend the results
            return SpatialPose.Lerp(poseA, poseB, BlendWeight);
        }
        
        public void Reset()
        {
            _nodeA.Reset();
            _nodeB.Reset();
        }
    }
}