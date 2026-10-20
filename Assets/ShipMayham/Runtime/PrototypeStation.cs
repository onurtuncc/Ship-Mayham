using UnityEngine;

namespace ShipMayham
{
    public sealed class PrototypeStation : MonoBehaviour
    {
        public StationKind Kind;
        public string DisplayName;
        public Transform InteractionAnchor;
        public Transform Visuals;
        public TextMesh Label;

        public Vector2 DeckPosition(Transform ship)
        {
            Vector3 p = ship.InverseTransformPoint(InteractionAnchor.position);
            return new Vector2(p.x, p.z);
        }
    }
}
