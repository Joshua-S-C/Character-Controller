using System;

namespace Nyteshade.Modules.Anim
{
    /// <summary>
    /// Represents a "link" between two states in an Animation State Machine.
    /// It holds a condition (a function) and the name of the state to go to.
    /// </summary>
    public class AnimTransition
    {
        public readonly string TargetStateName;
        public readonly Func<bool> Condition;
        
        // How long (in seconds) to blend when this transition is taken.
        public readonly float BlendDuration; 

        public AnimTransition(string targetStateName, Func<bool> condition, float blendDuration)
        {
            TargetStateName = targetStateName;
            Condition = condition;
            BlendDuration = blendDuration;
        }
    }
}