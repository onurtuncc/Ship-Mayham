using UnityEngine;
using UnityEngine.InputSystem;

namespace ShipMayham
{
    /// <summary>Presentation only. Replace Visuals children; keep gameplay anchors and references.</summary>
    public sealed class PrototypeView : MonoBehaviour
    {
        public PrototypeSession Session;
        public Camera Camera;
        public Transform[] CrewRoots;
        public TextMesh[] CrewLabels;
        public Transform[] CarriedItems;
        public Transform[] PassengerVisuals;
        public TextMesh[] PassengerLabels;
        public Transform LeakVisual;
        public Transform DestinationMarker;
        [Header("First person")]
        public float EyeHeight = 1.6f;
        public float MouseSensitivity = .12f;
        public float StickLookSpeed = 140f;
        public float FirstPersonFieldOfView = 75f;
        public Vector3 HeldItemOffset = new Vector3(.32f, -.28f, .65f);
        public Vector3 HeldPassengerOffset = new Vector3(.9f, -1.25f, 1.4f);
        [Header("Service colors")]
        public Color FoodColor = new Color(1f, .48f, .12f);
        public Color DrinkColor = new Color(.15f, .6f, 1f);
        [Tooltip("Optional hull meshes to recolor. Classic restores their authored property blocks.")]
        public Renderer[] LiveryRenderers;
        public int FocusedTarget { get; private set; } = -1;
        public string FocusText { get; private set; } = "";
        public bool IsFirstPerson { get; private set; }
        private float sendTimer;
        private bool wasSpawned;
        private bool wasAtCannon;
        private bool captureRequested;
        private float lookYaw, lookPitch = 12;
        private Transform[] crewVisuals;
        private Renderer[][] itemRenderers;
        private Vector3[] carryPositions;
        private Quaternion[] carryRotations;
        private ServiceItem[] displayedItems;
        private MaterialPropertyBlock itemProperties;
        private Vector3[] passengerVisualOffsets, passengerLabelOffsets;
        private Quaternion[] passengerVisualRotations;
        private Transform[] passengerBuoys;
        private bool[] displayedShore;
        private MaterialPropertyBlock[] originalLiveryBlocks;
        private int displayedLivery = -1;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            crewVisuals = new Transform[CrewRoots.Length];
            itemRenderers = new Renderer[CrewRoots.Length][];
            carryPositions = new Vector3[CrewRoots.Length];
            carryRotations = new Quaternion[CrewRoots.Length];
            displayedItems = new ServiceItem[CrewRoots.Length];
            displayedShore = new bool[CrewRoots.Length];
            itemProperties = new MaterialPropertyBlock();
            if (LiveryRenderers == null || LiveryRenderers.Length == 0)
            {
                var hulls = new System.Collections.Generic.List<Renderer>();
                foreach (var renderer in Session.ShipRoot.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name == "Hull_Primitive") hulls.Add(renderer);
                LiveryRenderers = hulls.ToArray();
            }
            originalLiveryBlocks = new MaterialPropertyBlock[LiveryRenderers.Length];
            for (int i = 0; i < LiveryRenderers.Length; i++)
            {
                originalLiveryBlocks[i] = new MaterialPropertyBlock();
                if (LiveryRenderers[i] != null) LiveryRenderers[i].GetPropertyBlock(originalLiveryBlocks[i]);
            }
            for (int i = 0; i < CrewRoots.Length; i++)
            {
                crewVisuals[i] = CrewRoots[i].Find("Visuals");
                itemRenderers[i] = CarriedItems[i].GetComponentsInChildren<Renderer>(true);
                carryPositions[i] = CarriedItems[i].localPosition;
                carryRotations[i] = CarriedItems[i].localRotation;
            }
            passengerVisualOffsets = new Vector3[PassengerVisuals.Length];
            passengerLabelOffsets = new Vector3[PassengerVisuals.Length];
            passengerVisualRotations = new Quaternion[PassengerVisuals.Length];
            passengerBuoys = new Transform[PassengerVisuals.Length];
            for (int i = 0; i < PassengerVisuals.Length; i++)
            {
                passengerVisualOffsets[i] = PassengerVisuals[i].localPosition;
                passengerVisualRotations[i] = PassengerVisuals[i].localRotation;
                passengerLabelOffsets[i] = PassengerLabels[i].transform.localPosition;
                passengerBuoys[i] = PassengerVisuals[i].Find("RescueFloat_Visual");
            }
        }

        private void CaptureCursor(bool capture)
        {
            captureRequested = capture;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CaptureCursor(false);
        }

        private void OnDisable()
        {
            CaptureCursor(false);
            IsFirstPerson = false;
            if (crewVisuals == null) return;
            for (int i = 0; i < crewVisuals.Length; i++)
            {
                if (crewVisuals[i] != null) crewVisuals[i].gameObject.SetActive(true);
                if (CrewLabels[i] != null) CrewLabels[i].gameObject.SetActive(true);
                if (CarriedItems[i] == null) continue;
                CarriedItems[i].localPosition = carryPositions[i];
                CarriedItems[i].localRotation = carryRotations[i];
            }
            for (int i = 0; i < PassengerVisuals.Length; i++)
            {
                if (PassengerVisuals[i] == null) continue;
                PassengerVisuals[i].localPosition = passengerVisualOffsets[i];
                PassengerVisuals[i].localRotation = passengerVisualRotations[i];
                if (PassengerLabels[i] != null) PassengerLabels[i].transform.localPosition = passengerLabelOffsets[i];
            }
        }

        /// <summary>Yaw is relative to the ship; looking up/down never changes walking speed.</summary>
        public static Vector2 ToDeckMovement(Vector2 input, float yaw)
        {
            Vector3 direction = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y);
            return Vector2.ClampMagnitude(new Vector2(direction.x, direction.z), 1);
        }

        private void Update()
        {
            if (Session == null || !Session.IsSpawned) return;
            int local = Session.LocalCrewIndex;
            if (local < 0) return;
            Vector2 input = Vector2.zero;
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            bool capturedThisFrame = false;
            if (Application.isFocused && (keyboard?.escapeKey.wasPressedThisFrame == true || pad?.startButton.wasPressedThisFrame == true))
            {
                // The Editor can unlock on Escape before Update; track our own intent so
                // that the same Escape press does not immediately relock the cursor.
                capturedThisFrame = !captureRequested;
                CaptureCursor(capturedThisFrame);
            }
            // Click only the unobstructed center of the game view to resume; HUD clicks stay UI clicks.
            if (Application.isFocused && Cursor.lockState != CursorLockMode.Locked && Mouse.current?.leftButton.wasPressedThisFrame == true)
            {
                Vector2 pointer = Mouse.current.position.ReadValue();
                if (pointer.x > Screen.width * .32f && pointer.x < Screen.width * .8f && pointer.y > Screen.height * .2f && pointer.y < Screen.height * .8f)
                { CaptureCursor(true); capturedThisFrame = true; }
            }
            if (Application.isFocused && Cursor.lockState == CursorLockMode.Locked && !capturedThisFrame)
            {
                Vector2 look = Mouse.current != null ? Mouse.current.delta.ReadValue() * MouseSensitivity : Vector2.zero;
                if (pad != null) look += pad.rightStick.ReadValue() * (StickLookSpeed * Time.unscaledDeltaTime);
                lookYaw = Mathf.Repeat(lookYaw + look.x, 360);
                lookPitch = Mathf.Clamp(lookPitch - look.y, -80, 80);
                if (Session.Crew[local].AtCannon) lookPitch = Mathf.Clamp(lookPitch, -25, 45);
                if (keyboard != null) input = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                if (pad != null && pad.leftStick.ReadValue().sqrMagnitude > .04f) input = pad.leftStick.ReadValue();
                if (!Session.Crew[local].AtHelm) input = ToDeckMovement(input, lookYaw);
                if (Session.Crew[local].AtCannon)
                {
                    input = Vector2.zero;
                    if (Mouse.current?.leftButton.wasPressedThisFrame == true || pad?.rightTrigger.wasPressedThisFrame == true)
                        Session.CannonActionRpc(new Vector2(lookYaw, lookPitch), false);
                    if (keyboard?.rKey.wasPressedThisFrame == true || pad?.buttonWest.wasPressedThisFrame == true)
                        Session.CannonActionRpc(new Vector2(lookYaw, lookPitch), true);
                }
                // Use the current look, not last frame's reticle, for interaction selection.
                Camera.transform.rotation = (Session.Crew[local].OnShore ? Quaternion.identity : Session.ShipRoot.rotation) * Quaternion.Euler(lookPitch, lookYaw, 0);
                FindTarget(Session.Crew[local]);
                if (!Session.Crew[local].AtHelm && !Session.Crew[local].AtCannon && !Session.Crew[local].OnShore)
                {
                    int passenger = FocusedTarget - Session.Stations.Length;
                    if (keyboard?.fKey.wasPressedThisFrame == true || pad?.buttonNorth.wasPressedThisFrame == true)
                        Session.PassengerCommandRpc(passenger, PassengerAction.CheckTicket, lookYaw);
                    if (keyboard?.gKey.wasPressedThisFrame == true || pad?.buttonEast.wasPressedThisFrame == true)
                        Session.PassengerCommandRpc(passenger, PassengerAction.CarryOrDrop, lookYaw);
                    if (Session.LocalCarriedPassengerIndex >= 0 && (Mouse.current?.leftButton.wasPressedThisFrame == true || pad?.rightTrigger.wasPressedThisFrame == true))
                        Session.PassengerCommandRpc(-1, PassengerAction.Throw, lookYaw);
                }
                if (keyboard?.eKey.wasPressedThisFrame == true || pad?.buttonSouth.wasPressedThisFrame == true)
                {
                    if (FocusedTarget <= -100) Session.HarborCommandRpc((HarborAction)(-100 - FocusedTarget));
                    else Session.InteractRpc(FocusedTarget, false);
                }
                if (!Session.Crew[local].AtCannon && (keyboard?.spaceKey.wasPressedThisFrame == true || pad?.buttonWest.wasPressedThisFrame == true))
                {
                    if (Session.Crew[local].AtHelm) Session.InteractRpc(-1, true);
                    else Session.JumpRpc();
                }
            }
            sendTimer -= Time.deltaTime;
            if (sendTimer <= 0)
            {
                sendTimer = .05f;
                Session.MoveRpc(input);
                if (Session.Crew[local].AtCannon && Cursor.lockState == CursorLockMode.Locked)
                    Session.AimCannonRpc(new Vector2(lookYaw, lookPitch));
                if (Session.LocalCarriedPassengerIndex >= 0 && Cursor.lockState == CursorLockMode.Locked)
                    Session.AimPassengerRpc(lookYaw);
            }
        }

        private void FindTarget(CrewState crew)
        {
            FocusedTarget = -1;
            if (crew.AtCannon) { FocusText = "LMB / RT: fire | R / X: reload | E / A: release cannon"; return; }
            if (crew.AtHelm) { FocusText = "E / A: release helm   |   Space / X: depart or dock"; return; }
            if (Session.LocalCarriedPassengerIndex >= 0) { FocusText = "G / B: put down | LMB / RT: throw (-5 happiness)\nOverboard: another -20 happiness + 15 coins. Rescue: 8s."; return; }
            if (crew.JumpHeight > 0 || crew.VerticalSpeed > 0) { FocusText = "Land before interacting"; return; }
            float bestAngle = 24f;
            FocusText = "Look at a nearby station or passenger";
            if (crew.OnShore)
            {
                FocusText = "Harbor: food / drink packs 12 coins, hull shield 30. Return at the green gangway marker.";
                FindHarborTarget(crew, ref bestAngle);
                return;
            }
            for (int i = 0; i < Session.Stations.Length; i++)
            {
                var station = Session.Stations[i];
                float distance = Vector2.Distance(crew.Position, station.DeckPosition(Session.ShipRoot));
                if (distance > VoyageSimulation.InteractionRange) continue;
                float angle = Vector3.Angle(Camera.transform.forward, station.InteractionAnchor.position + Vector3.up * .65f - Camera.transform.position);
                if (angle >= bestAngle) continue;
                bestAngle = angle;
                FocusedTarget = i;
                FocusText = "E / A: " + station.DisplayName;
                if (VoyageSimulation.IsUpgrade(station.Kind))
                    FocusText += $" ({VoyageSimulation.UpgradeCost(station.Kind, Session.Voyage.Value)} coins; at port)";
            }
            for (int i = 0; i < Session.Passengers.Count; i++)
            {
                if (!Session.Passengers[i].Active) continue;
                if (Session.Passengers[i].Motion != PassengerMotion.OnDeck) continue;
                Vector3 p = Session.Passengers[i].Position;
                float distance = Vector2.Distance(crew.Position, new Vector2(p.x, p.z));
                if (distance > VoyageSimulation.InteractionRange) continue;
                float angle = Vector3.Angle(Camera.transform.forward, Session.ShipRoot.TransformPoint(p + Vector3.up) - Camera.transform.position);
                if (angle >= bestAngle) continue;
                bestAngle = angle;
                FocusedTarget = Session.Stations.Length + i;
                var passenger = Session.Passengers[i];
                string ticket = !passenger.TicketChecked ? "F / Y: check ticket" : !passenger.FarePaid ? "F / Y: collect 40 coins" : "Ticket valid";
                string service = passenger.Request == ServiceItem.None ? "No order" : "E / A: serve " + passenger.Request;
                FocusText = $"Passenger {i + 1} -> North Port | {PassengerPresentation.TicketText(passenger)}\n{service} | {ticket} | G / B: lift";
            }
            FindHarborTarget(crew, ref bestAngle);
        }

        private void FindHarborTarget(CrewState crew, ref float bestAngle)
        {
            var harbor = Session.Harbor;
            if (harbor == null || !harbor.IsOpen) return;
            int first = crew.OnShore ? 1 : 0, last = crew.OnShore ? 4 : 0;
            for (int i = first; i <= last; i++)
            {
                var action = (HarborAction)i;
                if (Vector2.Distance(crew.Position, VoyageSimulation.HarborPoint(action)) > VoyageSimulation.InteractionRange) continue;
                Vector3 point = harbor.Point(action) + Vector3.up * .7f;
                float angle = Vector3.Angle(Camera.transform.forward, point - Camera.transform.position);
                if (angle >= bestAngle) continue;
                bestAngle = angle;
                FocusedTarget = PrototypeHarbor.Target(action);
                FocusText = PrototypeHarbor.Prompt(action);
                if (Session.Voyage.Value.DockJobActive)
                {
                    if (action == HarborAction.BuyFood)
                        FocusText = crew.Carrying == ServiceItem.Cargo ? "E / A: return cargo crate" : "E / A: pick up cargo crate — deliver at HULL";
                    else if (action == HarborAction.MaintainHull && crew.Carrying == ServiceItem.Cargo)
                        FocusText = "E / A: deliver cargo crate";
                }
            }
        }

        private void LateUpdate()
        {
            if (Session == null || Camera == null) return;
            bool active = Session != null && Session.IsSpawned;
            int local = active ? Session.LocalCrewIndex : -1;
            bool firstPerson = local >= 0;
            if (firstPerson != IsFirstPerson)
            {
                IsFirstPerson = firstPerson;
                lookYaw = 0;
                lookPitch = 12;
                CaptureCursor(firstPerson && Application.isFocused && !Application.isBatchMode);
            }
            bool atCannon = firstPerson && Session.Crew[local].AtCannon;
            if (atCannon && !wasAtCannon)
            {
                lookYaw = Session.Encounter.Value.CannonYaw;
                lookPitch = Session.Encounter.Value.CannonPitch;
            }
            wasAtCannon = atCannon;
            var state = active ? Session.Voyage.Value : new VoyageState { Health = 100 };
            ApplyLivery(state.Livery);
            Transform ship = Session.ShipRoot;
            float blend = wasSpawned ? 1 - Mathf.Exp(-18 * Time.deltaTime) : 1;
            ship.position = Vector3.Lerp(ship.position, new Vector3(state.Position.x, 0, state.Position.y), blend);
            ship.rotation = Quaternion.Slerp(ship.rotation, Quaternion.Euler(0, state.Heading, 0), blend);
            wasSpawned = active;
            for (int i = 0; i < CrewRoots.Length; i++)
            {
                bool show = active && i < Session.Crew.Count && Session.Crew[i].Active;
                CrewRoots[i].gameObject.SetActive(show);
                if (!show) continue;
                var crew = Session.Crew[i];
                var target = new Vector3(crew.Position.x, (crew.OnShore ? VoyageSimulation.ShoreHeight : .5f) + crew.JumpHeight, crew.Position.y);
                Vector3 worldTarget = crew.OnShore ? VoyageSimulation.HarborOrigin(state) + target : ship.TransformPoint(target);
                CrewRoots[i].position = Vector3.Lerp(CrewRoots[i].position, worldTarget, displayedShore[i] != crew.OnShore ? 1 : blend);
                CrewRoots[i].rotation = crew.OnShore ? Quaternion.identity : ship.rotation;
                displayedShore[i] = crew.OnShore;
                bool me = local == i;
                // Hide only the local body, never the root or another client's representation.
                if (crewVisuals[i] != null) crewVisuals[i].gameObject.SetActive(!me);
                CrewLabels[i].gameObject.SetActive(!me);
                CrewLabels[i].text = $"P{i + 1}{(me ? " (YOU)" : "")}{(crew.AtHelm ? " / HELM" : "")}";
                if (crew.AtCannon) CrewLabels[i].text += " / GUNNER";
                if (crew.OnShore) CrewLabels[i].text += " / ASHORE";
                CarriedItems[i].gameObject.SetActive(crew.Carrying != ServiceItem.None);
                SetItemColor(i, crew.Carrying);
                if (!me)
                {
                    CarriedItems[i].localPosition = carryPositions[i];
                    CarriedItems[i].localRotation = carryRotations[i];
                }
                if (crew.Carrying != ServiceItem.None) CrewLabels[i].text += "\n" + crew.Carrying;
            }
            Camera.fieldOfView = firstPerson ? FirstPersonFieldOfView : 55;
            Camera.nearClipPlane = .05f;
            if (firstPerson)
            {
                Camera.transform.SetPositionAndRotation(CrewRoots[local].position + ship.up * EyeHeight,
                    (Session.Crew[local].OnShore ? Quaternion.identity : ship.rotation) * Quaternion.Euler(lookPitch, lookYaw, 0));
                CarriedItems[local].SetPositionAndRotation(Camera.transform.TransformPoint(HeldItemOffset), Camera.transform.rotation);
                FindTarget(Session.Crew[local]);
            }
            else
            {
                Camera.transform.position = ship.TransformPoint(new Vector3(0, 24, -19));
                Camera.transform.LookAt(ship.position);
                FocusedTarget = -1;
            }
            foreach (var label in CrewLabels) WorldLabelPresentation.UpdateLabel(label, Camera);
            for (int i = 0; i < PassengerVisuals.Length; i++)
            {
                bool show = active && i < Session.Passengers.Count && Session.Passengers[i].Active;
                PassengerVisuals[i].gameObject.SetActive(show);
                PassengerLabels[i].gameObject.SetActive(show);
                if (!show) continue;
                var p = Session.Passengers[i];
                // Move presentation children, never the seat/interaction roots used by gameplay.
                bool heldByMe = p.Motion == PassengerMotion.Carried && p.CarrierSlot == local;
                Vector3 destination = ship.TransformPoint(p.Position + passengerVisualOffsets[i]);
                PassengerVisuals[i].position = heldByMe ? Camera.transform.TransformPoint(HeldPassengerOffset) :
                    Vector3.Lerp(PassengerVisuals[i].position, destination, blend);
                PassengerVisuals[i].rotation = ship.rotation * passengerVisualRotations[i] *
                    (p.Motion == PassengerMotion.Airborne ? Quaternion.Euler(0, 0, Time.time * 360) : Quaternion.identity);
                if (passengerBuoys[i] != null) passengerBuoys[i].gameObject.SetActive(p.Motion == PassengerMotion.Rescuing);
                bool focused = FocusedTarget == Session.Stations.Length + i;
                bool rescuing = p.Motion == PassengerMotion.Rescuing;
                PassengerLabels[i].gameObject.SetActive(!heldByMe && (rescuing || Vector3.Distance(Camera.transform.position, PassengerVisuals[i].position) < 7));
                PassengerLabels[i].transform.position = PassengerVisuals[i].position + ship.rotation * passengerLabelOffsets[i];
                PassengerLabels[i].text = $"{i + 1}: {(p.Request == ServiceItem.None ? "Happy" : p.Request.ToString())} / {p.Satisfaction:0}%\n{PassengerPresentation.TicketText(p)}";
                if (focused || rescuing) PassengerLabels[i].text += "\n" + PassengerPresentation.ReactionText(p);
                PassengerLabels[i].color = p.Motion == PassengerMotion.Rescuing || (p.TicketChecked && !p.FarePaid) ? new Color(1, .65f, .2f) : Color.white;
                WorldLabelPresentation.UpdateLabel(PassengerLabels[i], Camera);
            }
            LeakVisual.gameObject.SetActive(active && state.Leak != 0);
            foreach (var station in Session.Stations)
            {
                WorldLabelPresentation.UpdateLabel(station.Label, Camera);
                station.Label.gameObject.SetActive((local < 0 || !Session.Crew[local].OnShore) && Vector3.Distance(station.Label.transform.position, Camera.transform.position) < 10);
                station.Label.color = FocusedTarget >= 0 && FocusedTarget < Session.Stations.Length && Session.Stations[FocusedTarget] == station ? Color.yellow : Color.white;
            }
        }

        private void ApplyLivery(ShipLivery livery)
        {
            if (displayedLivery == (int)livery) return;
            displayedLivery = (int)livery;
            Color color = livery == ShipLivery.Seafoam ? new Color(.15f, .72f, .55f) : new Color(1f, .36f, .14f);
            for (int i = 0; i < LiveryRenderers.Length; i++)
            {
                var renderer = LiveryRenderers[i];
                if (renderer == null) continue;
                renderer.SetPropertyBlock(originalLiveryBlocks[i]);
                if (livery == ShipLivery.Classic) continue;
                renderer.GetPropertyBlock(itemProperties);
                itemProperties.SetColor(BaseColorId, color);
                itemProperties.SetColor(ColorId, color);
                renderer.SetPropertyBlock(itemProperties);
            }
        }

        private void SetItemColor(int slot, ServiceItem item)
        {
            if (displayedItems[slot] == item) return;
            displayedItems[slot] = item;
            if (item == ServiceItem.None) return;
            Color color = item == ServiceItem.Drink ? DrinkColor : item == ServiceItem.Cargo ? new Color(.5f, .28f, .1f) : FoodColor;
            foreach (var renderer in itemRenderers[slot])
            {
                renderer.GetPropertyBlock(itemProperties);
                itemProperties.SetColor(BaseColorId, color);
                itemProperties.SetColor(ColorId, color);
                renderer.SetPropertyBlock(itemProperties);
            }
        }
    }
}
