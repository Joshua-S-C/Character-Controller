using System.Collections.Generic;

namespace Nyteshade.Modules.Anim
{
    public class StateMachineNode : IAnimNode
    {
        private readonly Dictionary<string, AnimState> _states = new Dictionary<string, AnimState>();
        private AnimState _currentState;
        
        private AnimState _previousState;
        private float _blendTimer = 0.0f;
        private float _blendDuration = 0.0f;

        public StateMachineNode(string entryStateName, AnimState entryState)
        {
            AddState(entryStateName, entryState);
            _currentState = entryState;
            _previousState = null;
            
            _currentState.Animation.Reset();
        }

        public void AddState(string name, AnimState state)
        {
            _states[name] = state;
        }

        public void Update(float deltaTime)
        {
            // --- 1. Are we currently blending between states? ---
            if (_blendTimer > 0.0f)
            {
                _blendTimer -= deltaTime;
                if (_blendTimer <= 0.0f)
                {
                    // Blending finished — now stop the previous state's clip cleanly
                    if (_previousState?.Animation is ClipNode clipNodePrev)
                        clipNodePrev.Stop();

                    _previousState = null;
                }

                _previousState?.Animation.Update(deltaTime);
                _currentState.Animation.Update(deltaTime);
                return;
            }
            
            if (_blendDuration <= 0.0f)
            {
                if (_previousState?.Animation is ClipNode clipNodePrev)
                    clipNodePrev.Stop();

                _previousState = null;
            }


            // --- 2. If not blending, check for a transition ---
            foreach (var transition in _currentState.Transitions)
            {
                if (transition.Condition())
                {
                    // --- TRANSITION HAPPENED ---
                    if (_states.TryGetValue(transition.TargetStateName, out var newState))
                    {
                        _previousState = _currentState;
                        _currentState = newState;
                        
                        _blendDuration = transition.BlendDuration;
                        _blendTimer = _blendDuration;
                        
                        _currentState.Animation.Reset();
                        
                        if (_blendDuration <= 0.0f)
                        {
                            _previousState?.Animation.Reset();
                            _previousState = null;
                        }
                        
                        break;
                    }
                }
            }
            
            // --- 3. If no transition, just update the current state ---
            _currentState.Animation.Update(deltaTime);
        }

        public SpatialPose Evaluate(int boneCount)
        {
            if (_previousState != null && _blendTimer > 0.0f)
            {
                var poseA = _previousState.Animation.Evaluate(boneCount);
                var poseB = _currentState.Animation.Evaluate(boneCount);
                float blendAlpha = 1.0f - (_blendTimer / _blendDuration);
                return SpatialPose.Lerp(poseA, poseB, blendAlpha);
            }
            
            return _currentState.Animation.Evaluate(boneCount);
        }

        public void Reset()
        {
            _previousState?.Animation.Reset(); 
            _previousState = null;
            _blendTimer = 0.0f;
            _currentState.Animation.Reset();
        }
    }
}