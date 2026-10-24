using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// The gate at the end of the gangway: its two leaves swing open while a crew member or a passenger is about to pass and
    /// close again behind them. Presentation only; the crossing itself is the host's (<see cref="VoyageSimulation.GatePoint"/>).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class PrototypeGangwayView : MonoBehaviour
    {
        public const float OpenDistance = 3f;

        public PrototypeSession Session;
        public PrototypeView View;
        public Transform Gate, LeafLeft, LeafRight;
        public float OpenDegrees = 105f, DegreesPerSecond = 260f;

        public float Angle { get; private set; }

        private void LateUpdate()
        {
            bool open = Session != null && Session.IsSpawned && Session.Harbor != null && Session.Harbor.IsOpen && SomeoneIsComing();
            Angle = Mathf.MoveTowards(Angle, open ? OpenDegrees : 0f, DegreesPerSecond * Time.deltaTime);
            if (LeafLeft != null) LeafLeft.localRotation = Quaternion.Euler(0, Angle, 0);
            if (LeafRight != null) LeafRight.localRotation = Quaternion.Euler(0, -Angle, 0);
        }

        private bool SomeoneIsComing()
        {
            if (Gate == null || Session.ShipRoot == null) return false;
            Vector3 gate = Gate.position;
            if (View != null)
                for (int i = 0; i < View.CrewRoots.Length; i++)
                {
                    if (!View.CrewRoots[i].gameObject.activeSelf) continue;
                    Vector3 offset = View.CrewRoots[i].position - gate;
                    if (offset.x * offset.x + offset.z * offset.z < OpenDistance * OpenDistance) return true;
                }
            for (int i = 0; i < Session.Passengers.Count; i++)
            {
                var passenger = Session.Passengers[i];
                if (!passenger.Active || (passenger.Motion != PassengerMotion.Boarding && passenger.Motion != PassengerMotion.Leaving)) continue;
                Vector3 offset = Session.ShipRoot.TransformPoint(passenger.Position) - gate;
                if (offset.x * offset.x + offset.z * offset.z < OpenDistance * OpenDistance) return true;
            }
            return false;
        }
    }
}
