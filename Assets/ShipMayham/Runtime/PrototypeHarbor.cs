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
        private bool residentsLabelled;

        // Who the town residents are and what each one is for.
        private static readonly (string resident, string name, string role)[] Residents =
        {
            ("Mara / Market", "MARA", "Market: sells food"),
            ("Finn / Tavern", "FINN", "Tavern: sells drinks"),
            ("Pip / Dockwright", "PIP", "Dockwright: mends the hull"),
            ("Nell / Paint", "NELL", "Painter: new ship colours"),
            ("Captain Ada / Harbor Master", "CAPTAIN ADA", "Harbor master: press E to plot a course"),
            ("Ollie / Fisher", "OLLIE", "Fisher: fish market and crane jobs"),
        };

        /// <summary>Puts a name and a job above every resident so that it is clear who is who.</summary>
        private void LabelResidents()
        {
            residentsLabelled = true;
            if (Labels == null || Labels.Length == 0 || Labels[0] == null || PortRoots == null) return;
            var added = new System.Collections.Generic.List<TextMesh>(Labels);
            foreach (var root in PortRoots)
                foreach (var (resident, name, role) in Residents)
                {
                    Transform npc = null;
                    foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == resident) { npc = t; break; }
                    if (npc == null) continue;
                    var label = Instantiate(Labels[0], npc);
                    label.name = "ResidentLabel";
                    label.transform.localPosition = new Vector3(0, 2.4f, 0);
                    label.text = name + " - " + role;
                    added.Add(label);
                }
            Labels = added.ToArray();
        }
        private PrototypeIslandView islands;

        /// <summary>The ground and scenery of the islands around the two towns (created on first use).</summary>
        public PrototypeIslandView Islands => islands != null ? islands : islands = gameObject.AddComponent<PrototypeIslandView>();
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
            Islands.Present(PortRoots, voyage);
            if (View != null && View.DestinationMarker != null)
            {
                Vector2 target = Campaign.World(voyage.Target);
                View.DestinationMarker.position = new Vector3(target.x, View.DestinationMarker.position.y, target.y);
            }
        }

        private void LateUpdate()
        {
            if (Session.IsSpawned) PlaceTowns(Session.Voyage.Value);
            if (!residentsLabelled) LabelResidents();
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
