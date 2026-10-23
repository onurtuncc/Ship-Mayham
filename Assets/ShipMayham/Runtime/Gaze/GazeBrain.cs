using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    public enum GazeKind : byte { Camera, Character, Ahead, Wander }

    /// <summary>Something a character may look at. <see cref="Id"/> is stable while the thing exists (instance id for characters).</summary>
    public struct GazeCandidate
    {
        public int Id;
        public GazeKind Kind;
        public Vector3 Position;
    }

    /// <summary>
    /// Decides what one character looks at and for how long. Pure logic: callers hand in the character's eye position, facing and
    /// the things nearby, and get back the world point to aim at. The mix is what makes it read as alive: people notice the
    /// player now and then (never constantly), glance at neighbours, look where they are walking, and let their eyes wander,
    /// with small involuntary jitter while fixating.
    /// </summary>
    public sealed class GazeBrain
    {
        public const int CameraId = -1, AheadId = -2, WanderId = -3;
        public const float MaxAngleFromFacing = 85f;
        public const float CameraRange = 16f, CharacterRange = 8f, MinRange = .4f;

        private readonly System.Random random;
        private int targetId = int.MinValue;
        private GazeKind targetKind = GazeKind.Wander;
        private float holdUntil = float.NegativeInfinity, nextJitter;
        private Vector3 wanderDirection = Vector3.forward;          // relative to facing, so it follows the body turning
        private Vector2 jitter;                                     // degrees (yaw, pitch)

        public GazeKind CurrentKind => targetKind;
        public int CurrentId => targetId;

        public GazeBrain(int seed) { random = new System.Random(seed); }

        /// <summary>World-space point the eyes should aim at now.</summary>
        public Vector3 Update(float now, Vector3 eye, Vector3 facing, bool moving, IReadOnlyList<GazeCandidate> candidates)
        {
            facing.y = 0;
            facing = facing.sqrMagnitude > 1e-4f ? facing.normalized : Vector3.forward;
            if (now >= holdUntil || !TryResolve(eye, facing, moving, candidates, out Vector3 point)) Pick(now, eye, facing, moving, candidates);
            if (!TryResolve(eye, facing, moving, candidates, out point)) point = eye + facing * 6f;

            if (now >= nextJitter)
            {
                nextJitter = now + Range(.18f, .55f);
                jitter = new Vector2(Range(-1.2f, 1.2f), Range(-.8f, .8f));
            }
            Vector3 toTarget = point - eye;
            float distance = Mathf.Max(toTarget.magnitude, .01f);
            return eye + Quaternion.Euler(jitter.y, jitter.x, 0) * (toTarget / distance) * distance;
        }

        /// <summary>True when a point is somewhere a character could plausibly look without turning their head right round.</summary>
        public static bool InView(Vector3 eye, Vector3 facing, Vector3 point)
        {
            Vector3 to = point - eye;
            float distance = to.magnitude;
            if (distance < MinRange) return false;
            return Vector3.Angle(facing, to / distance) <= MaxAngleFromFacing;
        }

        private bool TryResolve(Vector3 eye, Vector3 facing, bool moving, IReadOnlyList<GazeCandidate> candidates, out Vector3 point)
        {
            point = default;
            switch (targetKind)
            {
                case GazeKind.Ahead:
                    if (!moving) return false;
                    point = eye + facing * 6f;
                    return true;
                case GazeKind.Wander:
                    point = eye + Quaternion.LookRotation(facing) * wanderDirection * 6f;
                    return true;
                default:
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        if (candidates[i].Id != targetId) continue;
                        point = candidates[i].Position;
                        float limit = targetKind == GazeKind.Camera ? CameraRange : CharacterRange;
                        return InView(eye, facing, point) && (point - eye).sqrMagnitude <= limit * limit;
                    }
                    return false;
            }
        }

        private void Pick(float now, Vector3 eye, Vector3 facing, bool moving, IReadOnlyList<GazeCandidate> candidates)
        {
            float total = 0;
            int characters = 0;
            for (int i = 0; i < candidates.Count; i++) total += Weight(candidates[i], eye, facing, ref characters);
            float wander = 1f, ahead = moving ? (targetKind == GazeKind.Ahead ? .2f : 2.6f) : 0f;      // staring straight ahead for ever looks dead
            total += wander + ahead;

            float roll = (float)random.NextDouble() * total;
            characters = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                float w = Weight(candidates[i], eye, facing, ref characters);
                if (w <= 0) continue;
                roll -= w;
                if (roll > 0) continue;
                Choose(now, candidates[i].Id, candidates[i].Kind);
                return;
            }
            if (roll - ahead < 0 && ahead > 0) { Choose(now, AheadId, GazeKind.Ahead); return; }
            Choose(now, WanderId, GazeKind.Wander);
        }

        private float Weight(GazeCandidate candidate, Vector3 eye, Vector3 facing, ref int characters)
        {
            float distance = (candidate.Position - eye).magnitude;
            float weight;
            switch (candidate.Kind)
            {
                case GazeKind.Camera:
                    if (distance > CameraRange) return 0;
                    weight = 3f;
                    break;
                case GazeKind.Character:
                    if (distance > CharacterRange || ++characters > 4) return 0;
                    weight = 1.6f;
                    break;
                default:
                    return 0;
            }
            if (!InView(eye, facing, candidate.Position)) return 0;
            return candidate.Id == targetId ? weight * .15f : weight;      // rarely re-pick what was just looked at
        }

        private void Choose(float now, int id, GazeKind kind)
        {
            targetId = id; targetKind = kind;
            switch (kind)
            {
                case GazeKind.Camera: holdUntil = now + Range(.9f, 2.4f); break;
                case GazeKind.Character: holdUntil = now + Range(1f, 2.6f); break;
                case GazeKind.Ahead: holdUntil = now + Range(1f, 2.2f); break;
                default:
                    holdUntil = now + Range(.6f, 1.8f);
                    wanderDirection = Quaternion.Euler(Range(-12f, 14f), (random.Next(2) == 0 ? -1 : 1) * Range(12f, 55f), 0) * Vector3.forward;       // a real glance, never dead ahead
                    break;
            }
        }

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
