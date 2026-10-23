using UnityEngine;

namespace ShipMayham
{
    /// <summary>Authored port anchors and presentation. Rules live in VoyageHarbor.</summary>
    public sealed class PrototypeHarbor : MonoBehaviour
    {
        public PrototypeSession Session;
        public PrototypeView View;
        public Transform ShipGate;
        public Transform[] PortRoots;
        // HarborAction 1..StationsPerPort, departure first then destination.
        public Transform[] ShoreMarkers;
        public TextMesh[] Labels;
        public Transform[] Walkways;
        public Transform[] SalvageMarkers;
        public bool IsOpen => Session.IsSpawned && (Session.Voyage.Value.Phase == VoyagePhase.Boarding || Session.Voyage.Value.Phase == VoyagePhase.Arrived);
        public int PortIndex => Session.Voyage.Value.Phase == VoyagePhase.Arrived ? 1 : 0;
        public Vector3 Point(HarborAction action) => action == HarborAction.GoAshore ? ShipGate.position :
            ShoreMarkers[PortIndex * HarborTownLayout.StationsPerPort + (int)action - 1].position;
        private void LateUpdate()
        {
            bool open = IsOpen;
            ShipGate.gameObject.SetActive(open);
            for (int i = 0; i < Walkways.Length; i++) Walkways[i].gameObject.SetActive(open && i == PortIndex);
            foreach (var label in Labels)
            {
                WorldLabelPresentation.UpdateLabel(label, View.Camera);
                label.gameObject.SetActive(open && Vector3.Distance(label.transform.position, View.Camera.transform.position) < 15);
            }
        }
    }
}
