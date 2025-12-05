using System;
using System.Collections.Generic;
using System.Linq;

namespace Nyteshade.Modules.Anim;

public class Keyframe
{
    public float Time;
    public SpatialPose Pose;
}

public class AnimationClip
{
    
    public float Duration { get; private set; }
    
    //todo : A sorted array would be faster
    public List<Keyframe> Keyframes = new List<Keyframe>();

    public void SortKeyframes()
    {
        Keyframes = Keyframes.OrderBy(k => k.Time).ToList();
        Duration = Keyframes.LastOrDefault()?.Time ?? 0f;
    }
    
    public SpatialPose Sample(float time, int boneCount)
    {
        if (Keyframes.Count == 0)
            return new SpatialPose(boneCount);
        if (Keyframes.Count == 1)
            return Keyframes[0].Pose;

        time = Math.Clamp(time, 0f, Duration);

        Keyframe prevFrame = Keyframes.First();
        Keyframe nextFrame = Keyframes.Last();

        for (int i = 1; i < Keyframes.Count; i++)
        {
            if (Keyframes[i].Time > time)
            {
                nextFrame = Keyframes[i];
                prevFrame = Keyframes[Math.Max(i - 1, 0)];
                break;
            }
        }

        // Past the end --> return last pose
        if (time >= Duration)
            return Keyframes.Last().Pose;
        
        float frameDuration = nextFrame.Time - prevFrame.Time;
        float t = frameDuration == 0 ? 0 : (time - prevFrame.Time) / frameDuration;
        return SpatialPose.Lerp(prevFrame.Pose, nextFrame.Pose, t);
    }
}
