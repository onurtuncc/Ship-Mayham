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
        private TextMesh[] portLabels;
        private int placedIsland0 = -1, placedIsland1 = -1;

        /// <summary>The island the departure town stands at and the one the destination town stands at, from the replicated voyage.</summary>
        private void PlaceTowns(in VoyageState voyage)
        {
            if (PortRoots == null || PortRoots.Length < 2) return;
            if (portLabels == null)
            {
                portLabels = new TextMesh[2];
                for (int i = 0; i < 2; i++)
                {
                    var label = PortRoots[i] != null ? PortRoots[i].Find("PortLabel") : null;
                    portLabels[i] = label != null ? label.GetComponent<TextMesh>() : null;
                }
            }
            if (placedIsland0 != voyage.Island)
            {
                placedIsland0 = voyage.Island;
                PortRoots[0].position = VoyageSimulation.IslandHarborOrigin(voyage.Island);
                if (portLabels[0] != null) portLabels[0].text = Campaign.Name(voyage.Island).ToUpperInvariant();
            }
            if (placedIsland1 != voyage.Target)
            {
                placedIsland1 = voyage.Target;
                PortRoots[1].position = VoyageSimulation.IslandHarborOrigin(voyage.Target);
                if (portLabels[1] != null) portLabels[1].text = Campaign.Name(voyage.Target).ToUpperInvariant();
            }
            if (View != null && View.DestinationMarker != null)
            {
                Vector2 target = Campaign.World(voyage.Target);
                View.DestinationMarker.position = new Vector3(target.x, View.DestinationMarker.position.y, target.y);
            }
        }

        private void LateUpdate()
        {
            if (Session.IsSpawned) PlaceTowns(Session.Voyage.Value);
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
