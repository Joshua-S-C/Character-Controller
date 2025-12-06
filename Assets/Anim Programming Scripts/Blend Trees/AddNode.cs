namespace Nyteshade.Modules.Anim
{
    
    public class AddNode : IAnimNode
    {
        private readonly IAnimNode _baseNode;
        private readonly IAnimNode _additiveNode;

        public AddNode(IAnimNode baseNode, IAnimNode additiveNode)
        {
            _baseNode = baseNode;
            _additiveNode = additiveNode;
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

            // 2. Add the additive pose on top of the base pose
            return SpatialPose.Concatenate(basePose, additivePose);
        }
        
        public void Reset()
        {
            _baseNode.Reset();
            _additiveNode.Reset();
        }
    }
}