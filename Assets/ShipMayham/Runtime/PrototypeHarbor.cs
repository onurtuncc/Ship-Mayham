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
        // Board / food / drink / maintenance, departure first then destination.
        public Transform[] ShoreMarkers;
        public TextMesh[] Labels;
        public Transform[] Walkways;
        public bool IsOpen => Session.IsSpawned && (Session.Voyage.Value.Phase == VoyagePhase.Boarding || Session.Voyage.Value.Phase == VoyagePhase.Arrived);
        public int PortIndex => Session.Voyage.Value.Phase == VoyagePhase.Arrived ? 1 : 0;
        public Vector3 Point(HarborAction action) => action == HarborAction.GoAshore ? ShipGate.position :
            ShoreMarkers[PortIndex * 4 + (int)action - 1].position;
        public static int Target(HarborAction action) => -100 - (int)action;
        public static string Prompt(HarborAction action)
        {
            switch (action)
            {
                case HarborAction.GoAshore: return "E / A: go ashore (empty hands)";
                case HarborAction.BoardShip: return "E / A: board ship";
                case HarborAction.BuyFood: return "E / A: buy 8 food for 12 coins | storage cap 32";
                case HarborAction.BuyDrink: return "E / A: buy 8 drinks for 12 coins | storage cap 32";
                default: return "E / A: hull protection — 30 coins, absorbs 20 damage";
            }
        }

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
