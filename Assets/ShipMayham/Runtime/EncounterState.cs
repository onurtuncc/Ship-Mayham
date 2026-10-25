using System;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    [Serializable]
    public struct EncounterState : INetworkSerializable, IEquatable<EncounterState>
    {
        public PiratePhase Pirate;
        public Vector2 PiratePosition;
        public float PirateHealth, WarningRemaining, AttackRemaining, EnemyShotRemaining;
        public bool PowerOut, CannonLoaded;
        public int PowerRepairSteps, Ammo, Bounty, ShotsFired, PirateShotsFired, Hits;
        public int MagazineAmmo, ReadyAmmo;
        public float CannonYaw, CannonPitch, ReloadRemaining;
        public Vector3 ShotFrom, ShotTo, EnemyShotFrom, EnemyShotTo;
        public CannonballState CrewBall, PirateBall;
        /// <summary>The extra barrels' rounds of a volley (see <see cref="CannonProgression"/>).</summary>
        public CannonballState CrewBallB, CrewBallC;
        public int PirateHits;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Pirate); s.SerializeValue(ref PiratePosition); s.SerializeValue(ref PirateHealth);
            s.SerializeValue(ref WarningRemaining); s.SerializeValue(ref AttackRemaining); s.SerializeValue(ref EnemyShotRemaining);
            s.SerializeValue(ref PowerOut); s.SerializeValue(ref PowerRepairSteps);
            s.SerializeValue(ref CannonLoaded); s.SerializeValue(ref Ammo); s.SerializeValue(ref Bounty);
            s.SerializeValue(ref MagazineAmmo); s.SerializeValue(ref ReadyAmmo);
            s.SerializeValue(ref ShotsFired); s.SerializeValue(ref PirateShotsFired); s.SerializeValue(ref Hits);
            s.SerializeValue(ref CannonYaw); s.SerializeValue(ref CannonPitch); s.SerializeValue(ref ReloadRemaining);
            s.SerializeValue(ref ShotFrom); s.SerializeValue(ref ShotTo); s.SerializeValue(ref EnemyShotFrom); s.SerializeValue(ref EnemyShotTo);
            s.SerializeValue(ref CrewBall); s.SerializeValue(ref PirateBall); s.SerializeValue(ref PirateHits);
            s.SerializeValue(ref CrewBallB); s.SerializeValue(ref CrewBallC);
        }
        public bool Equals(EncounterState other) => ValueType.Equals(this, other);
    }
}
