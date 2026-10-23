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
        [Header("Helm chase camera")]
        public float ChaseDistance = 26f;
        public float ChasePitch = 40f;
        public float ChaseFieldOfView = 58f;
        [Header("Service colors")]
        public Color FoodColor = new Color(1f, .48f, .12f);
        public Color DrinkColor = new Color(.15f, .6f, 1f);
        [Tooltip("Optional hull meshes to recolor. Classic restores their authored property blocks.")]
        public Renderer[] LiveryRenderers;
        public int FocusedTarget { get; private set; } = -1;
        public string FocusText { get; private set; } = "";
        public bool IsFirstPerson { get; private set; }
        /// <summary>True while the local helmsman uses the elevated follow camera instead of first person.</summary>
        public bool ChaseCameraActive { get; private set; }
        /// <summary>Estimated ship turn rate in degrees per second (clockwise positive), for banking effects.</summary>
        public float ShipTurnRate => shipFromNet ? shipNetTurnRate : shipHeading.Rate;
        /// <summary>Estimated ship velocity in the world plane, for wake and lean effects.</summary>
        public Vector3 ShipVelocity => shipFromNet ? shipNetVelocity : shipPosition.Velocity;

        /// <summary>Smoothed velocity of a crew member in the frame they walk in (deck or quay), metres per second.</summary>
        public Vector3 CrewVelocity(int index) =>
            crewMotion == null || index < 0 || index >= crewMotion.Length ? Vector3.zero : crewFromNet[index] ? crewNetVelocity[index] : crewMotion[index].Velocity;
        /// <summary>Seconds between movement input messages; the server holds each one for 0.35 s so this only bounds latency.</summary>
        public const float InputSendInterval = .033f;
        private const string ChaseKey = "ship-mayham.camera.helm-chase.v1";
        private float sendTimer;
        private PrototypeClientMotion clientMotion;
        private bool jumpRequested;
        private Vector3[] crewBlend, crewLastRender, crewNetVelocity;
        private bool[] crewFromNet;
        private Vector3 shipNetVelocity; private float shipNetTurnRate;
        private bool shipFromNet;
        private readonly MotionSmoother shipPosition = new MotionSmoother { CorrectionRate = 12f, SnapDistance = 30f, MaxSpeed = 25f };
        private readonly AngleSmoother shipHeading = new AngleSmoother { CorrectionRate = 10f };
        private MotionSmoother[] crewMotion;
        private bool chasePreferred;
        private float chaseHeading, chaseHeadingVelocity, lastLookInput;
        private bool wasSpawned;
        private bool wasAtCannon;
        private bool captureRequested;
        private float lookYaw, lookPitch = 12;
        private Transform[] crewVisuals;
        private Renderer[][] itemRenderers;
        private Transform[] ammoItemVisuals, serviceItemVisuals;
        private Transform[] luggageItemVisuals;
        private Transform[] repairKitVisuals;
        private Transform[] salvageVisuals;
        private Transform[] craneCrateVisuals;
        private bool wasAtCrane;
        public static readonly Vector3 CraneCameraOffset = new Vector3(3.5f, 18, 8.8f);
        private Renderer[] luggageTagRenderers;
        private Transform ammoPile;
        private TextMesh ammoLabel;
        private int displayedMagazine = -1;
        private Vector3[] carryPositions;
        private Quaternion[] carryRotations;
        private ServiceItem[] displayedItems;
        private MaterialPropertyBlock itemProperties;
        private Vector3[] passengerVisualOffsets, passengerLabelOffsets;
        private Quaternion[] passengerVisualRotations;
        private Transform[] passengerBuoys;
        private bool[] displayedShore, crewActiveLastFrame;
        private MaterialPropertyBlock[] originalLiveryBlocks;
        private int displayedLivery = -1;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void ShipCursor_Install();
#endif

        private void Awake()
        {
            chasePreferred = LoadChasePreference();
            clientMotion = GetComponent<PrototypeClientMotion>();
            if (clientMotion == null) clientMotion = gameObject.AddComponent<PrototypeClientMotion>();
            clientMotion.View = this;
#if UNITY_WEBGL && !UNITY_EDITOR
            ShipCursor_Install();
#endif
            crewVisuals = new Transform[CrewRoots.Length];
            itemRenderers = new Renderer[CrewRoots.Length][];
            ammoItemVisuals = new Transform[CrewRoots.Length];
            serviceItemVisuals = new Transform[CrewRoots.Length];
            luggageItemVisuals = new Transform[CrewRoots.Length];
            repairKitVisuals = new Transform[CrewRoots.Length];
            salvageVisuals = new Transform[CrewRoots.Length];
            craneCrateVisuals = new Transform[CrewRoots.Length];
            luggageTagRenderers = new Renderer[CrewRoots.Length];
            carryPositions = new Vector3[CrewRoots.Length];
            carryRotations = new Quaternion[CrewRoots.Length];
            displayedItems = new ServiceItem[CrewRoots.Length];
            displayedShore = new bool[CrewRoots.Length];
            crewActiveLastFrame = new bool[CrewRoots.Length];
            crewBlend = new Vector3[CrewRoots.Length]; crewLastRender = new Vector3[CrewRoots.Length];
            crewNetVelocity = new Vector3[CrewRoots.Length]; crewFromNet = new bool[CrewRoots.Length];
            crewMotion = new MotionSmoother[CrewRoots.Length];
            for (int i = 0; i < crewMotion.Length; i++) crewMotion[i] = new MotionSmoother { CorrectionRate = 16f, SnapDistance = 3f, MaxSpeed = 12f };
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
                ammoItemVisuals[i] = CarriedItems[i].Find("Ammo_Visual");
                serviceItemVisuals[i] = CarriedItems[i].Find("Visuals");
                luggageItemVisuals[i] = CarriedItems[i].Find("Model_Suitcase");
                repairKitVisuals[i] = CarriedItems[i].Find("Model_Toolbox");
                salvageVisuals[i] = CarriedItems[i].Find("Model_RopeCoil");
                craneCrateVisuals[i] = CarriedItems[i].Find("Model_CrateStack");
                if (luggageItemVisuals[i] != null)
                {
                    var tag = luggageItemVisuals[i].Find("Tag_Visual");
                    if (tag != null) luggageTagRenderers[i] = tag.GetComponent<Renderer>();
                }
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
            foreach (var station in Session.Stations)
                if (station.Kind == StationKind.Ammo)
                {
                    ammoPile = station.Visuals.Find("Model_CannonballPile");
                    ammoLabel = station.Label;
                    break;
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
#if UNITY_WEBGL && !UNITY_EDITOR
                // Browsers reserve Escape for releasing pointer lock. Recapture
                // requires a fresh canvas click, not an asynchronous/toggled request.
                capturedThisFrame = true;
                CaptureCursor(false);
#else
                // The Editor can unlock on Escape before Update; track our own intent so
                // that the same Escape press does not immediately relock the cursor.
                capturedThisFrame = !captureRequested;
                CaptureCursor(capturedThisFrame);
#endif
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
                if (look.sqrMagnitude > .0001f) lastLookInput = Time.unscaledTime;
                lookYaw = Mathf.Repeat(lookYaw + look.x, 360);
                lookPitch = Mathf.Clamp(lookPitch - look.y, -80, 80);
                if (Session.Crew[local].AtCannon) lookPitch = Mathf.Clamp(lookPitch, -25, 45);
                if (keyboard != null) input = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                if (pad != null && pad.leftStick.ReadValue().sqrMagnitude > .04f) input = pad.leftStick.ReadValue();
                if (!Session.Crew[local].AtHelm && !Session.Crew[local].AtCrane) input = ToDeckMovement(input, lookYaw);
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
                    if (Session.Crew[local].AtCrane) Session.HarborCraneActionRpc(false);
                    else if (FocusedTarget <= -200 && FocusedTarget > -200 - HarborTownLayout.SalvageCount)
                        Session.PickUpSalvageRpc(-200 - FocusedTarget);
                    else if (FocusedTarget <= -100)
                    {
                        var action = (HarborAction)(-100 - FocusedTarget);
                        if (Session.Voyage.Value.DockJobActive && (action == HarborAction.BuyFood || VoyageSimulation.IsPortCargo(Session.Crew[local].Carrying)))
                            Session.DockItemCommandRpc(action, Session.Voyage.Value.DockJobType);
                        else Session.HarborCommandRpc(action);
                    }
                    else Session.InteractRpc(FocusedTarget, false);
                }
                if (Session.Crew[local].AtHelm && (keyboard?.cKey.wasPressedThisFrame == true || pad?.buttonNorth.wasPressedThisFrame == true)) ToggleChaseCamera();
                if (!Session.Crew[local].AtCannon && (keyboard?.spaceKey.wasPressedThisFrame == true || pad?.buttonWest.wasPressedThisFrame == true))
                {
                    if (Session.Crew[local].AtCrane) Session.HarborCraneActionRpc(true);
                    else if (Session.Crew[local].AtHelm) Session.InteractRpc(-1, true);
                    else if (clientMotion != null) jumpRequested = true;
                    else Session.JumpRpc();
                }
            }
            if (clientMotion != null) clientMotion.SetInput(input, jumpRequested);
            jumpRequested = false;
            sendTimer -= Time.deltaTime;
            if (sendTimer <= 0)
            {
                sendTimer = InputSendInterval;
                if (clientMotion == null) Session.MoveRpc(input);
                if (Session.Crew[local].AtCannon && Cursor.lockState == CursorLockMode.Locked)
                    Session.AimCannonRpc(new Vector2(lookYaw, lookPitch));
                if (Session.LocalCarriedPassengerIndex >= 0 && Cursor.lockState == CursorLockMode.Locked)
                    Session.AimPassengerRpc(lookYaw);
            }
        }

        private void FindTarget(CrewState crew)
        {
            FocusedTarget = -1;
            if (crew.AtCannon) { FocusText = "LMB / RT: fire | R / X: reload | E / A: release cannon\nCrew: carry rounds from AMMO to CANNON."; return; }
            if (crew.AtHelm) { FocusText = "E / A: release helm   |   Space / X: depart or dock   |   C / Y: change camera"; return; }
            if (crew.AtCrane) { FocusText = "CRANE: WASD / stick moves hook (north up) | Space / X: lift / land | E / A: release"; return; }
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
                if (station.Kind == StationKind.RepairSupply)
                    FocusText = crew.Carrying == ServiceItem.RepairKit ? "E / A: return quick patch kit" : $"E / A: take kit ({Session.Voyage.Value.RepairKitStock} in store) -> REPAIR";
                else if (station.Kind == StationKind.Repair)
                    FocusText = crew.Carrying == ServiceItem.RepairKit
                        ? $"E / A: deliver kit ({Session.Voyage.Value.ReadyRepairKits}/{VoyageSimulation.RepairKitBufferCapacity} bench slots used)"
                        : Session.Voyage.Value.Leak == 0 ? "Hull watertight | carry KITS here to prepare quick patches"
                        : Session.Voyage.Value.ReadyRepairKits > 0 ? "E / A: use one quick kit -> seal leak + up to 8 hull"
                        : $"E / A: manual patch {Session.Voyage.Value.RepairSteps}/4 | or bring a kit from KITS";
                if (station.Kind == StationKind.Ammo)
                    FocusText = crew.Carrying == ServiceItem.Ammo ? "E / A: return cannonball to magazine" : $"E / A: take cannonball ({Session.Encounter.Value.MagazineAmmo} in magazine)";
                else if (station.Kind == StationKind.Cannon && crew.Carrying == ServiceItem.Ammo)
                {
                    var e = Session.Encounter.Value;
                    int occupied = e.ReadyAmmo + (e.ReloadRemaining > 0 ? 1 : 0);
                    FocusText = $"E / A: deliver cannonball ({occupied}/{VoyageSimulation.CannonBufferCapacity} rack slots used)";
                }
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
            int first = crew.OnShore ? 1 : 0, last = crew.OnShore ? HarborTownLayout.StationsPerPort : 0;
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
                    if (Session.Voyage.Value.DockJobType == DockJobKind.CraneLoading)
                    {
                        if (crew.Carrying == ServiceItem.CraneCrate) FocusText = "E / A: deliver crate at LOAD PAD | FISH MARKET / FOOD accepts returns";
                        continue;
                    }
                    if (Session.Voyage.Value.DockJobType == DockJobKind.SalvageRun)
                    {
                        if (VoyageSimulation.IsSalvage(crew.Carrying)) FocusText = "E / A: deliver salvage at PORT OFFICE | FOOD accepts returns";
                        continue;
                    }
                    if (Session.Voyage.Value.DockJobType == DockJobKind.LuggageSort)
                    {
                        FocusText = action == HarborAction.BuyFood
                            ? VoyageSimulation.IsLuggage(crew.Carrying) ? "E / A: return tagged bag" : "E / A: take next tagged bag (A / B / C)"
                            : VoyageSimulation.IsLuggage(crew.Carrying)
                                ? $"Bag {VoyageSimulation.LuggageTag(crew.Carrying)} -> {VoyageSimulation.LuggageDesk(Session.Voyage.Value, crew.Carrying)} | E / A: deliver at matching desk"
                                : PrototypeHarbor.Prompt(action);
                        continue;
                    }
                    if (action == HarborAction.BuyFood)
                        FocusText = crew.Carrying == ServiceItem.Cargo ? "E / A: return cargo crate" : "E / A: pick up cargo crate — deliver at HULL";
                    else if (action == HarborAction.MaintainHull && crew.Carrying == ServiceItem.Cargo)
                        FocusText = "E / A: deliver cargo crate";
                }
            }
            if (!crew.OnShore || !Session.Voyage.Value.DockJobActive || Session.Voyage.Value.DockJobType != DockJobKind.SalvageRun) return;
            for (int index = 0; index < HarborTownLayout.SalvageCount; index++)
            {
                if ((Session.Voyage.Value.DockJobDeliveredMask & (1 << index)) != 0) continue;
                ServiceItem item = (ServiceItem)((int)ServiceItem.SalvageA + index);
                bool held = false;
                foreach (var member in Session.Crew) if (member.Active && member.Carrying == item) held = true;
                if (held || Vector2.Distance(crew.Position, HarborTownLayout.SalvagePoint(index)) > VoyageSimulation.InteractionRange) continue;
                Vector3 point = VoyageSimulation.HarborOrigin(Session.Voyage.Value) + new Vector3(HarborTownLayout.SalvagePoint(index).x, 1, HarborTownLayout.SalvagePoint(index).y);
                float angle = Vector3.Angle(Camera.transform.forward, point - Camera.transform.position);
                if (angle >= bestAngle) continue;
                bestAngle = angle; FocusedTarget = -200 - index;
                FocusText = $"E / A: take salvage bundle {index + 1}/3 -> PORT OFFICE";
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
#if UNITY_WEBGL && !UNITY_EDITOR
                // Relay completion is asynchronous, not a browser user gesture.
                // Let the player explicitly click the center to request pointer lock.
                CaptureCursor(false);
#else
                CaptureCursor(firstPerson && Application.isFocused && !Application.isBatchMode);
#endif
            }
            bool chase = firstPerson && Session.Crew[local].AtHelm && chasePreferred;
            if (chase != ChaseCameraActive)
            {
                ChaseCameraActive = chase;
                lookYaw = 0;
                lookPitch = chase ? ChasePitch : 12;
                chaseHeadingVelocity = 0;
                chaseHeading = shipHeading.Angle;
            }
            bool atCannon = firstPerson && Session.Crew[local].AtCannon;
            if (atCannon && !wasAtCannon)
            {
                lookYaw = Session.Encounter.Value.CannonYaw;
                lookPitch = Session.Encounter.Value.CannonPitch;
            }
            wasAtCannon = atCannon;
            var state = active ? Session.Voyage.Value : new VoyageState { Health = 100 };
            int magazine = active ? Session.Encounter.Value.MagazineAmmo : 0;
            if (displayedMagazine != magazine)
            {
                displayedMagazine = magazine;
                if (ammoPile != null) ammoPile.gameObject.SetActive(magazine > 0);
                if (ammoLabel != null) ammoLabel.text = $"AMMO {magazine}\nE / A: carry round";
            }
            ApplyLivery(state.Livery);
            Transform ship = Session.ShipRoot;
            float blend = wasSpawned ? 1 - Mathf.Exp(-18 * Time.deltaTime) : 1;
            float now = Time.unscaledTime, dt = Time.deltaTime;
            Vector3 shipTarget = new Vector3(state.Position.x, 0, state.Position.y);
            if (!wasSpawned) { shipPosition.Snap(shipTarget, now); shipHeading.Snap(state.Heading, now); }
            Vector3 smoothedShip = shipPosition.Step(shipTarget, now, dt);
            float smoothedHeading = shipHeading.Step(state.Heading, now, dt);
            Vector3 netShip = default; float netHeading = 0;
            shipFromNet = clientMotion != null && clientMotion.TryGetShip(out netShip, out netHeading, out shipNetVelocity, out shipNetTurnRate);
            if (shipFromNet) { smoothedShip = netShip; smoothedHeading = netHeading; }
            ship.position = smoothedShip;
            ship.rotation = Quaternion.Euler(0, smoothedHeading, 0);
            wasSpawned = active;
            for (int i = 0; i < CrewRoots.Length; i++)
            {
                bool show = active && i < Session.Crew.Count && Session.Crew[i].Active && Session.Crew[i].ClientId != ulong.MaxValue;
                CrewRoots[i].gameObject.SetActive(show);
                crewActiveLastFrame[i] = show;
                if (!show) continue;
                var crew = Session.Crew[i];
                var target = new Vector3(crew.Position.x, (crew.OnShore ? VoyageSimulation.ShoreHeight : .5f) + crew.JumpHeight, crew.Position.y);
                // Smooth in the frame the crew member walks in (deck or quay), then place that frame; smoothing in world space made
                // crew slide across the deck whenever the ship moved.
                var motion = crewMotion[i];
                if (displayedShore[i] != crew.OnShore || !crewActiveLastFrame[i]) motion.Snap(target, now);
                Vector3 local3 = motion.Step(target, now, dt);
                float groundHeight = crew.OnShore ? VoyageSimulation.ShoreHeight : .5f;
                Vector3 netPosition = default;
                bool fromNet = clientMotion != null && clientMotion.TryGetCrew(i, out netPosition, out crewNetVelocity[i]);
                if (fromNet)
                {
                    local3 = new Vector3(netPosition.x, groundHeight + netPosition.y, netPosition.z);
                    if (!crewFromNet[i]) crewBlend[i] = BlendFrom(crewLastRender[i], local3);
                }
                else if (crewFromNet[i]) crewBlend[i] = BlendFrom(crewLastRender[i], local3);
                if (displayedShore[i] != crew.OnShore) crewBlend[i] = Vector3.zero;
                crewFromNet[i] = fromNet;
                crewBlend[i] *= Mathf.Exp(-9f * dt);
                local3 += crewBlend[i];
                crewLastRender[i] = local3;
                CrewRoots[i].position = crew.OnShore ? VoyageSimulation.HarborOrigin(state) + local3 : ship.TransformPoint(local3);
                CrewRoots[i].rotation = crew.OnShore ? Quaternion.identity : ship.rotation;
                displayedShore[i] = crew.OnShore;
                bool me = local == i;
                // Hide only the local body, never the root or another client's representation.
                // The local body is hidden in first person, but visible when the helm uses the chase camera.
                if (crewVisuals[i] != null) crewVisuals[i].gameObject.SetActive(!me || ChaseCameraActive);
                CrewLabels[i].gameObject.SetActive(!me);
                CrewLabels[i].text = $"P{i + 1}{(me ? " (YOU)" : "")}{(crew.AtHelm ? " / HELM" : "")}";
                if (crew.AtCannon) CrewLabels[i].text += " / GUNNER";
                if (crew.AtCrane) CrewLabels[i].text += " / CRANE";
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
            if (ChaseCameraActive) PlaceChaseCamera(ship, state, dt);
            else if (firstPerson)
            {
                bool atCrane = Session.Crew[local].AtCrane;
                if (atCrane && !wasAtCrane) { lookYaw = 236; lookPitch = 60; }
                if (!atCrane && wasAtCrane) lookPitch = 12;
                wasAtCrane = atCrane;
                Camera.transform.SetPositionAndRotation(CrewRoots[local].position + (atCrane ? CraneCameraOffset : ship.up * EyeHeight),
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
                if (focused && p.Motion == PassengerMotion.OnDeck)
                    PassengerLabels[i].text += "\n" + PassengerPresentation.CueText(PassengerPresentation.CueFor(p, Session.ActiveEvent.Value, state, i));
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

        /// <summary>Switches the helmsman between first person and the chase camera; the choice is remembered.</summary>
        // When a crew member switches between data sources (predicted, interpolated, smoothed) the new position can differ a little
        // from where they were drawn; carry the difference as an offset that fades out instead of popping.
        private static Vector3 BlendFrom(Vector3 previousRender, Vector3 newPosition)
        {
            Vector3 difference = previousRender - newPosition;
            return difference.sqrMagnitude > 4f ? Vector3.zero : difference;
        }

        public void ToggleChaseCamera()
        {
            chasePreferred = !chasePreferred;
            try { PlayerPrefs.SetInt(ChaseKey, chasePreferred ? 1 : 0); PlayerPrefs.Save(); }
            catch (System.Exception exception) { Debug.LogWarning("Could not persist the camera choice: " + exception.Message); }
        }

        private static bool LoadChasePreference()
        {
            try { return PlayerPrefs.GetInt(ChaseKey, 0) == 1; }
            catch (System.Exception exception) { Debug.LogWarning("Could not read the camera choice: " + exception.Message); return false; }
        }

        /// <summary>
        /// Elevated follow camera for the helmsman: trails the heading of the ship with a soft lag, leads the ship by its speed,
        /// pulls back as it accelerates, and lets the mouse orbit before easing back behind the ship.
        /// </summary>
        private void PlaceChaseCamera(Transform ship, VoyageState state, float dt)
        {
            float speed01 = Mathf.Clamp01(state.Speed / 8f);
            chaseHeading = Mathf.SmoothDampAngle(chaseHeading, ship.eulerAngles.y, ref chaseHeadingVelocity, .55f, 220f, dt);
            if (Time.unscaledTime - lastLookInput > 2.5f) lookYaw = Mathf.Repeat(Mathf.MoveTowardsAngle(lookYaw, 0, 140f * dt), 360);
            lookPitch = Mathf.Clamp(lookPitch, 14f, 76f);
            Quaternion rotation = Quaternion.Euler(lookPitch, chaseHeading + lookYaw, 0);
            Vector3 pivot = ship.position + Vector3.up * 1.5f + ship.forward * (2f + speed01 * 6f);
            Camera.transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * (ChaseDistance * (1f + .22f * speed01)), rotation);
            Camera.fieldOfView = ChaseFieldOfView + speed01 * 6f;
            Camera.nearClipPlane = .3f;
            FocusedTarget = -1;
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
            if (ammoItemVisuals[slot] != null) ammoItemVisuals[slot].gameObject.SetActive(item == ServiceItem.Ammo);
            bool luggage = VoyageSimulation.IsLuggage(item);
            bool repairKit = item == ServiceItem.RepairKit;
            bool salvage = VoyageSimulation.IsSalvage(item);
            bool craneCrate = item == ServiceItem.CraneCrate;
            if (craneCrateVisuals[slot] != null) craneCrateVisuals[slot].gameObject.SetActive(craneCrate);
            if (salvageVisuals[slot] != null) salvageVisuals[slot].gameObject.SetActive(salvage);
            if (repairKitVisuals[slot] != null) repairKitVisuals[slot].gameObject.SetActive(repairKit);
            if (luggageItemVisuals[slot] != null) luggageItemVisuals[slot].gameObject.SetActive(luggage);
            if (serviceItemVisuals[slot] != null) serviceItemVisuals[slot].gameObject.SetActive(item != ServiceItem.Ammo && !luggage && !repairKit && !salvage && !craneCrate);
            if (item == ServiceItem.None) return;
            Color color = item == ServiceItem.Ammo ? new Color(.12f, .14f, .18f) : item == ServiceItem.Drink ? DrinkColor : item == ServiceItem.Cargo ? new Color(.5f, .28f, .1f) : FoodColor;
            if (luggage) color = VoyageSimulation.LuggageColor(item);
            foreach (var renderer in itemRenderers[slot])
            {
                // Keep the imported leather palette; only the tag identifies a baggage item.
                if (luggageItemVisuals[slot] != null && renderer.transform.IsChildOf(luggageItemVisuals[slot]) && renderer != luggageTagRenderers[slot]) continue;
                if (repairKitVisuals[slot] != null && renderer.transform.IsChildOf(repairKitVisuals[slot])) continue;
                if (salvageVisuals[slot] != null && renderer.transform.IsChildOf(salvageVisuals[slot])) continue;
                if (craneCrateVisuals[slot] != null && renderer.transform.IsChildOf(craneCrateVisuals[slot])) continue;
                renderer.GetPropertyBlock(itemProperties);
                itemProperties.SetColor(BaseColorId, color);
                itemProperties.SetColor(ColorId, color);
                renderer.SetPropertyBlock(itemProperties);
            }
        }
    }
}
