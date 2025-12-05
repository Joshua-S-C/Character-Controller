namespace Nyteshade.Modules.Anim
{
    /// <summary>
    /// An interface for all Inverse Kinematics solvers.
    /// A solver's job is to modify the Skeleton's CurrentLocalSpacePose
    /// to achieve a specific goal (e.g., look at a target, grab an object).
    /// </summary>
    public interface IKSolver
    {
        public float Weight { get; set; }
        void Resolve(Skeleton skeleton);
    }
}