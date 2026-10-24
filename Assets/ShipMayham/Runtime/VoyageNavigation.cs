using System;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    [Serializable]
    public struct NavigationState : INetworkSerializable, IEquatable<NavigationState>
    {
        public uint Seed;
        public float ImpactCooldown, WaveIn, Warning, WaveHeading;
        public int Impacts, WavesAvoided, WavesHit;
        public bool WaveActive;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Seed); s.SerializeValue(ref ImpactCooldown); s.SerializeValue(ref WaveIn);
            s.SerializeValue(ref Warning); s.SerializeValue(ref WaveHeading); s.SerializeValue(ref WaveActive);
            s.SerializeValue(ref Impacts); s.SerializeValue(ref WavesAvoided); s.SerializeValue(ref WavesHit);
        }
        public bool Equals(NavigationState other) => ValueType.Equals(this, other);
    }

    public sealed partial class VoyageSimulation
    {
        public NavigationState Navigation;
        public const int ShoalCount = 3;
        public const float ShoalRadius = 10, WaveWarningSeconds = 5;
        public float CrewPressure
        {
            get
            {
                int count = 0;
                foreach (var member in Crew) if (member.Active && member.ClientId != ulong.MaxValue) count++;
                return count >= 4 ? 1 : count == 3 ? .8f : count == 2 ? .6f : .45f;
            }
        }
        public static Vector2 ShoalPosition(uint seed, int index)
        {
            if (index < 0 || index >= ShoalCount) throw new ArgumentOutOfRangeException(nameof(index));
            uint n = unchecked(seed * 1664525u + (uint)(index + 1) * 1013904223u);
            return new Vector2((index % 2 == 0 ? -1 : 1) * (18 + n % 12), 50 + index * 55 + (n >> 8) % 12);
        }
        private void ResetNavigation()
        {
            Navigation = new NavigationState { Seed = eventRandomState ^ (uint)State.Voyage * 747796405u, WaveIn = 35 };
        }
        private void TickNavigation(float dt)
        {
            Navigation.ImpactCooldown = Mathf.Max(0, Navigation.ImpactCooldown - dt);
            if (Navigation.ImpactCooldown == 0 && State.Speed > 1)
                for (int i = 0; i < ShoalCount; i++)
                    if (Vector2.Distance(State.Position, ShoalPosition(Navigation.Seed, i)) < ShoalRadius)
                    {
                        DamageHull(Mathf.Min(15, State.Speed * 2));
                        State.Speed *= .6f;
                        Navigation.ImpactCooldown = 4;
                        Navigation.Impacts++;
                        break;
                    }
            if (State.Route != RouteProfile.StormPassage) return;
            if (!Navigation.WaveActive)
            {
                Navigation.WaveIn = Mathf.Max(0, Navigation.WaveIn - dt * CrewPressure);
                if (Navigation.WaveIn > 0) return;
                Navigation.WaveActive = true;
                Navigation.Warning = WaveWarningSeconds;
                Navigation.WaveHeading = Mathf.Repeat(State.Heading + ((Navigation.Seed & 1) == 0 ? 65 : -65), 360);
                return;
            }
            Navigation.Warning = Mathf.Max(0, Navigation.Warning - dt);
            if (Navigation.Warning > 0) return;
            if (Mathf.Abs(Mathf.DeltaAngle(State.Heading, Navigation.WaveHeading)) <= 30) Navigation.WavesAvoided++;
            else { DamageHull(12); State.Speed *= .7f; Navigation.WavesHit++; }
            Navigation.WaveActive = false;
            Navigation.WaveIn = 40;
        }
    }
}
