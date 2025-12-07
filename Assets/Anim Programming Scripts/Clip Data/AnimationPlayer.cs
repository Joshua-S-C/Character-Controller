using System;

namespace Nyteshade.Modules.Anim
{
    public class AnimationPlayer
    {
        private readonly Skeleton _Skeleton;
        
        private IAnimNode _rootNode;

        public AnimationPlayer(Skeleton skeleton)
        {
            _Skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
        }
        
        public void SetRoot(IAnimNode rootNode)
        {
            _rootNode = rootNode;
        }
        
        public void Update(float deltaTime)
        {
            if (_rootNode == null)
                return;
            
            _rootNode.Update(deltaTime);
            SpatialPose finalPose = _rootNode.Evaluate(_Skeleton.BoneCount);
            _Skeleton.CurrentLocalSpacePose = finalPose;
        }
    }
}