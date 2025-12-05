using System.Collections.Generic;
using System; // [NEW] Added for Func

namespace Nyteshade.Modules.Anim
{
    /// <summary>
    /// Represents a single state in an Animation State Machine (e.g., "Locomotion" or "Jump").
    /// It holds the animation graph to play, and a list of transitions to other states.
    /// </summary>
    public class AnimState
    {
        public readonly IAnimNode Animation;
        public readonly List<AnimTransition> Transitions = new List<AnimTransition>();

        public AnimState(IAnimNode animation)
        {
            Animation = animation;
        }

        /// <summary>
        /// A clean "builder" method to add a new transition.
        /// </summary>
        public void AddTransition(string targetStateName, Func<bool> condition, float blendDuration = 0.2f)
        {
            // Default to 0.2s if not specified
            Transitions.Add(new AnimTransition(targetStateName, condition, blendDuration));
        }
    }
}