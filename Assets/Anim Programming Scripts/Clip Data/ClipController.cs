using System;

namespace Nyteshade.Modules.Anim;

public enum PlaybackBehavior
{
    Stop,
    Loop,
    PingPong
}

public class ClipController
{
    private float _KeyFrameTime;
    private float _ClipTime;
    private bool _IsPlaying = false;
    
    public PlaybackBehavior Behavior { get; set; } = PlaybackBehavior.Loop;
    public float PlaybackSpeed { get; set; } = 1.0f;
    public float KeyframeDuration { get; set; } = 1.0f;
    public float ClipDuration { get; set; } = 1.0f;

    public float NormalizedKeyframeTime { get; private set; }
    public float NormalizedClipTime { get; private set; }
    
    public float KeyframeTime => _KeyFrameTime;
    public float ClipTime => _ClipTime;
    public bool IsPlaying => _IsPlaying;

    public void Update(float deltaTime)
    {
        if (!_IsPlaying) return;
        
        float EffectiveDelta = deltaTime * PlaybackSpeed;
        
        _KeyFrameTime += EffectiveDelta;
        _ClipTime += EffectiveDelta;
        
        _KeyFrameTime = ResolveTime(_KeyFrameTime, KeyframeDuration, PlaybackSpeed, Behavior);
        _ClipTime = ResolveTime(_ClipTime, ClipDuration, PlaybackSpeed, Behavior);
        
        NormalizedKeyframeTime = KeyframeDuration == 0 ? 0 : _KeyFrameTime / KeyframeDuration;
        NormalizedClipTime = ClipDuration == 0 ? 0 : _ClipTime / ClipDuration;

    }

    private float ResolveTime(float time, float duration, float speed, PlaybackBehavior behavior)
    {
        bool unresolved = true;

        while (unresolved)
        {
            unresolved = false; 
 
            if (speed > 0 && time > duration)
            {
                switch (behavior)
                {
                    case PlaybackBehavior.Stop:
                        time = duration;
                        _IsPlaying = false; //Stop
                        break;
                    case PlaybackBehavior.Loop:
                        time -= duration;
                        unresolved = true; //Wrap
                        break;
                    case PlaybackBehavior.PingPong:
                        time = duration - (time - duration);
                        speed *= -1; // Reverse direction
                        unresolved = true;
                        break;
                }
            }
            
            else if (speed < 0 && time < 0)
            {
                switch (behavior)
                {
                    case PlaybackBehavior.Stop:
                        time = 0;
                        _IsPlaying = false;
                        break;
                    case PlaybackBehavior.Loop:
                        time += duration;
                        unresolved = true;
                        break;
                    case PlaybackBehavior.PingPong:
                        time = -time;
                        speed *= -1;
                        unresolved = true;
                        break;
                }
            }
        }
        
        return time;
    }
    
    public void Reset()
    {
        _KeyFrameTime = 0;
        _ClipTime = 0;
        NormalizedKeyframeTime = 0;
        NormalizedClipTime = 0;
    }
    
    public void Play()
    {
        _IsPlaying = true;
    }
    
    public void Pause()
    {
        _IsPlaying = false;
    }
    
    public void Stop()
    {
        _IsPlaying = false;
        Reset();
    }
}
