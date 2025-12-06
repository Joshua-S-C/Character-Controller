namespace Nyteshade.Modules.Anim
{
    public class BasePoseNode : IAnimNode
    {
        private readonly Skeleton _skeleton;

        public BasePoseNode(Skeleton skeleton)
        {
            _skeleton = skeleton;
        }

      
        public void ScriptUpdate(float deltaTime)
        {
            // Do nothing
        }
        
        public SpatialPose Evaluate(int boneCount)
        {
            // Just return the T-Pose
            return _skeleton.BasePose;
        }
        
        public void Reset()
        {
            // Do nothing, it's a static pose
        }
    }
}