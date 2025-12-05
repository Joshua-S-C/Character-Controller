namespace Nyteshade.Modules.Anim
{
    /// <summary>
    /// A "leaf" node in the animation graph that plays a single AnimationClip.
    /// </summary>
    public class ClipNode : IAnimNode
    {
        private readonly AnimationClip _clip;
        private readonly ClipController _controller;

        public bool IsPlaying => _controller.IsPlaying;
        
        public bool Loops
        {
            get => _controller.Behavior == PlaybackBehavior.Loop;
            set => _controller.Behavior = value ? PlaybackBehavior.Loop : PlaybackBehavior.Stop;
        }

        public ClipNode(AnimationClip clip)
        {
            _clip = clip;
            _controller = new ClipController
            {
                ClipDuration = clip.Duration,
                KeyframeDuration = clip.Duration,
                Behavior = PlaybackBehavior.Loop 
            };
        }

        public void Update(float deltaTime)
        {
            _controller.Update(deltaTime);
        }

        public SpatialPose Evaluate(int boneCount)
        {
            return _clip.Sample(_controller.KeyframeTime, boneCount);
        }
        
        public void Play() => _controller.Play();
        public void Stop() => _controller.Stop();
        
        public void Reset()
        {
            _controller.Reset();
            _controller.Play();
        }
    }
}