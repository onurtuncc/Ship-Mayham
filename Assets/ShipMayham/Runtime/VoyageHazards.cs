using UnityEngine;

namespace ShipMayham
{
    /// <summary>Captain's hazards: shoals hurt a ship that runs into them and rogue waves punish a ship that is not facing them.</summary>
    public sealed partial class VoyageSimulation
    {
        public HazardState Hazards;
        private readonly Shoal[] shoals = new Shoal[HazardLogic.MaxShoals];
        private int shoalCount;
        private float shoalCooldown;
        private bool hazardsReady;
        private uint hazardSessionSeed;

        public int ShoalTotal => shoalCount;

        public Shoal ShoalAt(int index) => shoals[index];

        private void ResetHazards()
        {
            Hazards = new HazardState { Seed = HazardLogic.VoyageSeed(hazardSessionSeed, State.Voyage), NextWaveAt = -1f };
            shoalCount = 0;
            shoalCooldown = 0;
            hazardsReady = false;
        }

        /// <summary>Lays out the shoals and schedules the first wave the first time the voyage is sailing.</summary>
        private void BeginHazards()
        {
            shoalCount = HazardLogic.Generate(Hazards.Seed, State.Route, shoals);
            float delay = HazardLogic.FirstWaveDelay(State.Route);
            Hazards.NextWaveAt = delay < 0 ? -1f : State.Elapsed + delay;
            Hazards.Wave = WavePhase.Idle;
            Hazards.WaveRemaining = 0;
            shoalCooldown = 0;
            hazardsReady = true;
        }

        /// <summary>After a migration the layout is rebuilt from the seed; the wave timer travels in the snapshot.</summary>
        private void RebuildHazardsAfterImport()
        {
            shoalCooldown = 0;
            if (State.Phase == VoyagePhase.Sailing)
            {
                shoalCount = HazardLogic.Generate(Hazards.Seed, State.Route, shoals);
                hazardsReady = true;
            }
            else
            {
                shoalCount = 0;
                hazardsReady = false;
            }
        }

        private void EndHazards()
        {
            Hazards.Wave = WavePhase.Idle;
            Hazards.WaveRemaining = 0;
            shoalCount = 0;
            hazardsReady = false;
        }

        private void TickHazards(float dt)
        {
            if (!hazardsReady) BeginHazards();
            if (shoalCooldown > 0) shoalCooldown -= dt;
            for (int i = 0; i < shoalCount; i++) CollideWithShoal(shoals[i]);
            TickWave(dt);
        }

        private void CollideWithShoal(Shoal shoal)
        {
            Vector2 offset = State.Position - shoal.Center;
            float limit = shoal.Radius + HazardLogic.ShipRadius, distance = offset.magnitude;
            if (distance >= limit) return;
            Vector2 normal;
            if (distance > .001f) normal = offset / distance;
            else
            {
                float radians = State.Heading * Mathf.Deg2Rad;
                normal = -new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            }
            State.Position = shoal.Center + normal * limit;
            if (shoalCooldown <= 0)
            {
                DamageHull(HazardLogic.ShoalDamage(State.Speed));
                Hazards.ShoalHits++;
                shoalCooldown = HazardLogic.HitCooldown;
            }
            State.Speed *= HazardLogic.ShoalSpeedFactor;
        }

        private void TickWave(float dt)
        {
            if (Hazards.Wave == WavePhase.Warning)
            {
                Hazards.WaveRemaining -= dt;
                if (Hazards.WaveRemaining <= 0) ResolveWave();
                return;
            }
            if (Hazards.NextWaveAt < 0 || State.Elapsed < Hazards.NextWaveAt) return;
            // The harbor entrance stays calm: a wave that is due close to the port waits.
            if (Vector2.Distance(State.Position, Destination) < HazardLogic.SafeFromPort)
            {
                Hazards.NextWaveAt = State.Elapsed + 5f;
                return;
            }
            Hazards.Wave = WavePhase.Warning;
            Hazards.WaveRemaining = HazardLogic.WaveWarningSeconds;
            Hazards.WaveBearing = HazardLogic.WaveBearing(Hazards.Seed, Hazards.WaveIndex);
        }

        private void ResolveWave()
        {
            if (HazardLogic.WaveSafe(State.Heading, Hazards.WaveBearing)) Hazards.WavesDodged++;
            else
            {
                Hazards.WavesHit++;
                DamageHull(HazardLogic.WaveDamage);
                State.Speed *= HazardLogic.WaveSpeedFactor;
            }
            Hazards.Wave = WavePhase.Idle;
            Hazards.WaveRemaining = 0;
            Hazards.WaveIndex++;
            Hazards.NextWaveAt = State.Elapsed + HazardLogic.WaveInterval(State.Route, Hazards.Seed, Hazards.WaveIndex);
        }
    }
}
