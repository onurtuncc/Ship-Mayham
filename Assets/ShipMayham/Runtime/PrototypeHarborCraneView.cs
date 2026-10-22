using UnityEngine;

namespace ShipMayham
{
    public sealed class PrototypeHarborCraneView : MonoBehaviour
    {
        public PrototypeHarbor Harbor;
        public Transform[] Hooks, Booms, Cables, AimMarkers, HangingCrates;
        public Transform[] StockCrates, PadCrates, DeliveredCrates;

        private void LateUpdate()
        {
            if (Harbor == null || !Harbor.Session.IsSpawned) return;
            Present(Harbor.Session.Voyage.Value, Harbor.PortIndex, Harbor.IsOpen);
        }

        public void Present(VoyageState state, int activePort, bool open)
        {
            for (int port = 0; port < 2; port++)
            {
                bool show = open && activePort == port && state.DockJobType == DockJobKind.CraneLoading;
                Vector2 landing = VoyageSimulation.CraneLandingPoint(state);
                Vector3 hook = new Vector3(landing.x, state.CraneHolding ? 4.2f : 1.25f, landing.y);
                Vector3 trolley = new Vector3(state.CraneHook.x, 8, state.CraneHook.y);
                Hooks[port].gameObject.SetActive(show); Booms[port].gameObject.SetActive(show);
                Cables[port].gameObject.SetActive(show); AimMarkers[port].gameObject.SetActive(show);
                Hooks[port].localPosition = hook;
                HangingCrates[port].gameObject.SetActive(state.CraneHolding);
                Span(Booms[port], new Vector3(-28, 8, 8), trolley, .16f);
                Span(Cables[port], trolley, hook, .035f);
                AimMarkers[port].localPosition = new Vector3(landing.x, .38f, landing.y);
                for (int i = 0; i < VoyageSimulation.DockJobGoal; i++)
                {
                    StockCrates[port * 3 + i].gameObject.SetActive(show && i < state.CraneStock);
                    DeliveredCrates[port * 3 + i].gameObject.SetActive(show && i < state.DockJobProgress);
                }
                for (int i = 0; i < VoyageSimulation.CranePadCapacity; i++) PadCrates[port * 2 + i].gameObject.SetActive(show && i < state.CraneReady);
            }
        }

        private static void Span(Transform visual, Vector3 from, Vector3 to, float width)
        {
            Vector3 delta = to - from;
            visual.localPosition = (from + to) * .5f;
            visual.localRotation = delta.sqrMagnitude > .0001f ? Quaternion.LookRotation(delta) : Quaternion.identity;
            visual.localScale = new Vector3(width, width, Mathf.Max(.01f, delta.magnitude));
        }
    }
}
