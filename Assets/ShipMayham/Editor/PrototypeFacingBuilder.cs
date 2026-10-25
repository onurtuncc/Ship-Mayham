using System;
using UnityEngine;

namespace ShipMayham.Editor
{
    /// <summary>
    /// Which way the generated models face. The rigged humans face +Z (toes forward), while the static deck bench has its
    /// backrest on +Z and so faces -Z. The town residents' roots were authored for the old static characters that faced -Z.
    /// These helpers turn the model wrappers, never the gameplay roots, so positions and anchors stay as they are. Idempotent.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        /// <summary>Yaw of a deck bench wrapper so passengers sit facing the bow, the way the human body faces.</summary>
        public const float BenchModelYaw = 180f;
        /// <summary>Yaw of a resident's rigged human inside the authored root, so they face their counter instead of the wall.</summary>
        public const float ResidentModelYaw = 180f;

        private static void ApplyFacing(PrototypeSession session)
        {
            if (session == null || session.PassengerAnchors == null || session.Harbor == null)
                throw new InvalidOperationException("A complete prototype scene is required to fix model facing.");
            foreach (var seat in session.PassengerAnchors)
            {
                var bench = seat != null ? seat.Find("SeatVisuals/Model_DeckBench") : null;
                if (bench == null) throw new InvalidOperationException($"Seat {seat?.name} has no Model_DeckBench.");
                bench.localRotation = Quaternion.Euler(0, BenchModelYaw, 0);
            }
            foreach (var port in session.Harbor.PortRoots)
                foreach (var (resident, model) in Residents)
                {
                    Transform root = FindDescendant(port, resident);
                    var body = root != null ? root.Find("Model_" + model) : null;
                    if (body == null) throw new InvalidOperationException($"Resident '{resident}' under {port.name} has no rigged body.");
                    body.localRotation = Quaternion.Euler(0, ResidentModelYaw, 0);
                }
        }
    }
}
