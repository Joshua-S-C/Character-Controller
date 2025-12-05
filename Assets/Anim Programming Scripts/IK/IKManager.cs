using System.Collections.Generic;

namespace Nyteshade.Modules.Anim
{
    public class IKManager
    {
        private readonly Skeleton _skeleton;
        private readonly List<IKSolver> _solvers = new List<IKSolver>();

        public IKManager(Skeleton skeleton)
        {
            _skeleton = skeleton;
        }

        public void AddSolver(IKSolver solver)
        {
            _solvers.Add(solver);
        }

        public void RemoveSolver(IKSolver solver)
        {
            _solvers.Remove(solver);
        }
        
        public void ResolveSolvers()
        {
            _skeleton.UpdateFKDirect(_skeleton.CurrentLocalSpacePose);
            
            foreach (var solver in _solvers)
            {
                if (solver.Weight > 0.001f)
                {
                    solver.Resolve(_skeleton);
                }
            }
            _skeleton.UpdateFKDirect(_skeleton.CurrentLocalSpacePose);
        }
    }
}