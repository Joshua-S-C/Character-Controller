namespace Nyteshade.Modules.Anim
{
    public interface IAnimNode
    {
        void ScriptUpdate(float deltaTime);
        SpatialPose Evaluate(int boneCount);
        
        void Reset();
    }
}