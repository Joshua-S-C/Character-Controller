using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace NyteshadeGodot.Modules.Maths;

public class CoreMaths
{
    public static Vector3 Average(List<Vector3> points)
    {
        if (points.Count == 0) return Vector3.Zero;
        Vector3 sum = Vector3.Zero;
        foreach (var p in points)
            sum += p;
        return sum / points.Count;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float ls = v.LengthSquared();
        if (ls > 1e-12f) return v / MathF.Sqrt(ls);
        return fallback;
    }
}