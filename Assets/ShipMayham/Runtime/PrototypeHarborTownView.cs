using UnityEngine;

namespace ShipMayham
{
    public sealed class PrototypeHarborTownView : MonoBehaviour
    {
        public PrototypeHarbor Harbor;
        private void LateUpdate()
        {
            if (Harbor == null || Harbor.SalvageMarkers == null) return;
            var session = Harbor.Session;
            for (int port = 0; port < 2; port++)
                for (int index = 0; index < HarborTownLayout.SalvageCount; index++)
                {
                    bool visible = Harbor.IsOpen && Harbor.PortIndex == port && session.Voyage.Value.DockJobActive &&
                        session.Voyage.Value.DockJobType == DockJobKind.SalvageRun && (session.Voyage.Value.DockJobDeliveredMask & (1 << index)) == 0;
                    if (visible)
                        foreach (var crew in session.Crew)
                            if (crew.Active && crew.Carrying == (ServiceItem)((int)ServiceItem.SalvageA + index)) visible = false;
                    var marker = Harbor.SalvageMarkers[port * HarborTownLayout.SalvageCount + index];
                    if (marker != null) marker.gameObject.SetActive(visible);
                }
        }
    }
}
