namespace Nyteshade.Modules.Anim
{
    public interface IAnimNode
    {
        void Update(float deltaTime);
        SpatialPose Evaluate(int boneCount);
        
        void Reset();
    }
}