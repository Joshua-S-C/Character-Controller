using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace NyteshadeGodot.Modules.Maths
{
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

        public static Quaternion FromToRotation(Vector3 fromDirection, Vector3 toDirection)
        {
            fromDirection = Vector3.Normalize(fromDirection);
            toDirection = Vector3.Normalize(toDirection);

            float dot = Vector3.Dot(fromDirection, toDirection);

            if (dot > 0.999999f) // Vectors are already aligned
            {
                return Quaternion.Identity;
            }

            if (dot < -0.999999f) // Vectors are perfectly opposed
            {
                // Need to find an arbitrary axis perpendicular to 'fromDirection'
                Vector3 axis = Vector3.UnitX;
                if (Math.Abs(Vector3.Dot(axis, fromDirection)) > 0.99f)
                {
                    axis = Vector3.UnitY; // Use Y axis if X is too parallel
                }
                Vector3 orthoAxis = Vector3.Normalize(Vector3.Cross(fromDirection, axis));
                return Quaternion.CreateFromAxisAngle(orthoAxis, MathF.PI);
            }

            // Standard case
            Vector3 cross = Vector3.Normalize(Vector3.Cross(fromDirection, toDirection));
            float angle = MathF.Acos(dot);
            return Quaternion.CreateFromAxisAngle(cross, angle);
        }

        public static float SafeDivide(float a, float b)
        {
            // Prevent division by zero, return 1 if scales are identical (even if 0)
            if (Math.Abs(a - b) < 1e-6f) return 1f;
            if (Math.Abs(b) < 1e-6f) return a; // Or handle as error, but this is safer
            return a / b;
        }
    }
}