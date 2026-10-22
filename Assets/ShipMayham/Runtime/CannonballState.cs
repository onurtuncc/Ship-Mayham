using System;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    [Serializable]
    public struct CannonballState : INetworkSerializable
    {
        public bool Active;
        public Vector3 Position, Velocity;
        public float Remaining;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Active); s.SerializeValue(ref Position);
            s.SerializeValue(ref Velocity); s.SerializeValue(ref Remaining);
        }
    }

    /// <summary>Finite segment tests; avoids tunnelling without a scene/Physics dependency.</summary>
    public static class BallisticCollision
    {
        public static bool Sphere(Vector3 from, Vector3 to, Vector3 center, float radius, out float fraction)
        {
            fraction = 0;
            if (!Valid(from) || !Valid(to) || !Valid(center) || !float.IsFinite(radius) || radius < 0) return false;
            Vector3 offset = from - center, delta = to - from;
            float c = offset.sqrMagnitude - radius * radius;
            if (c <= 0) return true;
            float a = delta.sqrMagnitude;
            if (a < .000001f) return false;
            float b = Vector3.Dot(offset, delta), discriminant = b * b - a * c;
            if (discriminant < 0) return false;
            fraction = (-b - Mathf.Sqrt(discriminant)) / a;
            return fraction >= 0 && fraction <= 1;
        }

        public static bool Box(Vector3 from, Vector3 to, Vector3 min, Vector3 max, out float fraction)
        {
            fraction = 0;
            if (!Valid(from) || !Valid(to) || !Valid(min) || !Valid(max)) return false;
            float exit = 1;
            for (int axis = 0; axis < 3; axis++)
            {
                if (min[axis] > max[axis]) return false;
                float delta = to[axis] - from[axis];
                if (Mathf.Abs(delta) < .000001f)
                {
                    if (from[axis] < min[axis] || from[axis] > max[axis]) return false;
                    continue;
                }
                float a = (min[axis] - from[axis]) / delta, b = (max[axis] - from[axis]) / delta;
                fraction = Mathf.Max(fraction, Mathf.Min(a, b));
                exit = Mathf.Min(exit, Mathf.Max(a, b));
                if (fraction > exit) return false;
            }
            return true;
        }

        private static bool Valid(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
    }
}
