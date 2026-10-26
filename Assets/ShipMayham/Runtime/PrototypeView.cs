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
        public float LobbyOrbitDegreesPerSecond = 6f;
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
        /// <summary>What the local crosshair is on; refreshed every frame the local crew member is active.</summary>
        public InteractionTarget Target { get; private set; }
        public InteractionPrompt Prompt => Target.Prompt;
        /// <summary>True while the emote wheel is open (the signal key is held); looking is frozen so the pointer can choose.</summary>
        public bool WheelOpen { get; private set; }
        /// <summary>The emote currently chosen on the wheel, if any.</summary>
        public SignalKind? WheelSelection { get; private set; }
        private float signalPressedAt = -1, nextSignalAt;
        private Vector2 wheelVector;

        private void CancelSignal()
        {
            WheelOpen = false;
            WheelSelection = null;
            signalPressedAt = -1;
        }

        // Tap: ping what the crosshair is on. Hold: open the emote wheel, choose with the pointer or right stick, release to send.
        private void UpdateSignals(Keyboard keyboard, Gamepad pad, int local)
        {
            bool pressed = keyboard?.qKey.wasPressedThisFrame == true || pad?.dpad.up.wasPressedThisFrame == true;
            bool released = keyboard?.qKey.wasReleasedThisFrame == true || pad?.dpad.up.wasReleasedThisFrame == true;
            bool held = keyboard?.qKey.isPressed == true || pad?.dpad.up.isPressed == true;
            if (pressed) { signalPressedAt = Time.unscaledTime; wheelVector = Vector2.zero; }
            if (signalPressedAt < 0) return;
            if (!WheelOpen && held && Time.unscaledTime - signalPressedAt >= Signals.HoldSeconds) WheelOpen = true;
            if (WheelOpen)
            {
                if (Mouse.current != null) wheelVector = Vector2.ClampMagnitude(wheelVector + Mouse.current.delta.ReadValue() * .6f, 80f);
                if (pad != null && pad.rightStick.ReadValue().sqrMagnitude > .25f) wheelVector = pad.rightStick.ReadValue() * 60f;
                WheelSelection = Signals.WheelChoice(wheelVector);
            }
            if (!released) return;
            var chosen = WheelOpen ? WheelSelection : null;
            bool wasWheel = WheelOpen;
            CancelSignal();
            if (Time.unscaledTime < nextSignalAt) return;
            if (wasWheel) { if (chosen.HasValue) SendEmote(chosen.Value); }
            else SendPing(local);
        }

        private void SendEmote(SignalKind kind)
        {
            nextSignalAt = Time.unscaledTime + Signals.CooldownSeconds;
            Session.SignalRpc(kind, Vector3.zero, false, default);
        }

        private void SendPing(int local)
        {
            nextSignalAt = Time.unscaledTime + Signals.CooldownSeconds;
            bool aboard = !Session.Crew[local].OnShore;
            Vector3 world;
            string label = string.Empty;
            if (!Target.IsNone && Target.WorldPoint != Vector3.zero)
            {
                world = Target.WorldPoint;
                label = Target.Prompt.Title ?? string.Empty;
            }
            else
            {
                // Nothing targeted: mark where the look direction meets the floor, or a point ahead when it never does.
                float floor = aboard ? Session.ShipRoot.TransformPoint(new Vector3(0, .5f, 0)).y : VoyageSimulation.ShoreHeight;
                var ray = new Ray(Camera.transform.position, Camera.transform.forward);
                world = new Plane(Vector3.up, new Vector3(0, floor, 0)).Raycast(ray, out float distance) && distance < 40f
                    ? ray.GetPoint(distance) : ray.GetPoint(14f);
            }
            Vector3 sent = aboard ? Session.ShipRoot.InverseTransformPoint(world) : world;
            Session.SignalRpc(SignalKind.Ping, sent, aboard, new Unity.Collections.FixedString32Bytes(LobbyRules.SanitizeName(label) ?? string.Empty));
        }

        /// <summary>True while the in-game menu is open: the cursor is free and, in a solo voyage, the game is paused.</summary>
        public bool MenuOpen { get; private set; }
        private float menuOpenedAt;
        private bool lockedLastFrame;

        public void OpenMenu()
        {
            if (MenuOpen) return;
            MenuOpen = true;
            CancelSignal();
            menuOpenedAt = Time.unscaledTime;
            CaptureCursor(false);
            if (Session != null && Session.IsSoloVoyage) Time.timeScale = 0;
        }

        /// <summary>Closes the menu. When asked, the cursor is captured again (browsers only allow that from a click).</summary>
        public void CloseMenu(bool recapture)
        {
            if (!MenuOpen) return;
            MenuOpen = false;
            Time.timeScale = 1;
            if (recapture && !Application.isBatchMode) CaptureCursor(Application.isFocused);
        }

        /// <summary>True while the purser's desk is open; the cursor is free and the crew member stands still.</summary>
        public bool PurserOpen { get; private set; }
        /// <summary>True while a skill game is on screen: the crew member stands still, the look is frozen and the game reads the keys.</summary>
        public bool MiniGameOpen { get; set; }

        public void OpenPurser()
        {
            if (PurserOpen) return;
            PurserOpen = true;
            CancelSignal();
            CaptureCursor(false);
        }

        public void ClosePurser()
        {
            if (!PurserOpen) return;
            PurserOpen = false;
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            // Browsers need a fresh click to lock the pointer again; elsewhere resume looking at once.
            CaptureCursor(Application.isFocused && !Application.isBatchMode);
#endif
        }
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
        private Vector3[] crewBlend, crewLastRender, crewNetVelocity, crewWorldLast, crewGlide;
        private bool[] crewWorldKnown;
        /// <summary>How fast the glide through the gangway gate dies away (per second) and the longest hop it smooths.</summary>
        private const float GateGlideRate = 4.5f, GateGlideMaxDistance = 8f;
        /// <summary>How far the body root hangs below the water line while swimming (the water reaches the chest).</summary>
        public float SwimBodyOffset = -.75f;
        public float SwimEyeAboveWater = .4f;
        private bool[] displayedSwim;
        private float[] crewDepth;
        /// <summary>Metres the local player's eyes are below the sea surface (0 when above it); drives the underwater look.</summary>
        public float LocalEyeDepth { get; private set; }
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
        private Quaternion[] passengerVisualRotations, passengerFacing;
        private Transform[] passengerBuoys, passengerFloaties;
        // Passengers are drawn through the same snapshot smoother as the crew, in the deck frame: chasing each 25 Hz update with
        // a plain lerp made every walker stutter.
        private MotionSmoother[] passengerMotion;
        private bool[] passengerShown, passengerHeldShown;
        /// <summary>Speed (m/s) above which a passenger body turns to face where it walks.</summary>
        private const float PassengerFaceWalkSpeed = .25f;
        private bool[] displayedShore, crewActiveLastFrame, crewSmootherShore;
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
            if (GetComponent<PrototypeNavigationView>() == null) gameObject.AddComponent<PrototypeNavigationView>().View = this;
            if (GetComponent<PrototypeDeckLifeView>() == null) gameObject.AddComponent<PrototypeDeckLifeView>().View = this;
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
            displayedShore = new bool[CrewRoots.Length]; crewSmootherShore = new bool[CrewRoots.Length];
            displayedSwim = new bool[CrewRoots.Length];
            crewDepth = new float[CrewRoots.Length];
            crewActiveLastFrame = new bool[CrewRoots.Length];
            crewBlend = new Vector3[CrewRoots.Length]; crewLastRender = new Vector3[CrewRoots.Length];
            crewWorldLast = new Vector3[CrewRoots.Length]; crewGlide = new Vector3[CrewRoots.Length]; crewWorldKnown = new bool[CrewRoots.Length];
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
            passengerFacing = new Quaternion[PassengerVisuals.Length];
            passengerBuoys = new Transform[PassengerVisuals.Length];
            passengerFloaties = new Transform[PassengerVisuals.Length];
            passengerMotion = new MotionSmoother[PassengerVisuals.Length];
            passengerShown = new bool[PassengerVisuals.Length];
            passengerHeldShown = new bool[PassengerVisuals.Length];
            for (int i = 0; i < PassengerVisuals.Length; i++)
            {
                passengerMotion[i] = new MotionSmoother { CorrectionRate = 14f, SnapDistance = 3f, MaxSpeed = 20f };
                passengerVisualOffsets[i] = PassengerVisuals[i].localPosition;
                passengerVisualRotations[i] = passengerFacing[i] = PassengerVisuals[i].localRotation;
                passengerLabelOffsets[i] = PassengerLabels[i].transform.localPosition;
                passengerBuoys[i] = PassengerVisuals[i].Find("RescueFloat_Visual");
                passengerFloaties[i] = PassengerVisuals[i].Find("Floaties");
            }
            // Ship upgrades are sold by the purser, so the upgrade fittings stay out of the world.
            foreach (var station in Session.Stations)
                if (VoyageSimulation.IsUpgrade(station.Kind) && station.Visuals != null) station.Visuals.gameObject.SetActive(false);
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
            MenuOpen = false;
            Time.timeScale = 1;
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

        /// <summary>A leak with no patch kit and no valve to hand is nailed over with the hammer game.</summary>
        private bool HammerWouldHelp()
        {
            var voyage = Session.Voyage.Value;
            return voyage.Leak != 0 && voyage.ReadyRepairKits == 0 && !Session.Emergency.Value.ValveAdjusted &&
                Session.Crew[Session.LocalCrewIndex].Carrying == ServiceItem.None;
        }

        /// <summary>Yaw is relative to the ship; looking up/down never changes walking speed.</summary>
        public static Vector2 ToDeckMovement(Vector2 input, float yaw)
        {
            Vector3 direction = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y);
            return Vector2.ClampMagnitude(new Vector2(direction.x, direction.z), 1);
        }

        private void Update()
        {
            if (Session == null || !Session.IsSpawned || Session.InLobby) return;
            int local = Session.LocalCrewIndex;
            if (local < 0) return;
            Vector2 input = Vector2.zero;
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            bool escape = Application.isFocused && (keyboard?.escapeKey.wasPressedThisFrame == true || pad?.startButton.wasPressedThisFrame == true);
            if (MenuOpen)
            {
                // A browser reports the lock loss and Escape in separate frames; ignore the late key so it cannot close the menu it opened.
                if (escape && Time.unscaledTime - menuOpenedAt > .3f)
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    CloseMenu(false);
#else
                    CloseMenu(true);
#endif
                }
                if (clientMotion != null) clientMotion.SetInput(Vector2.zero, false);
                return;
            }
            if (PurserOpen)
            {
                bool leave = Session.Voyage.Value.Phase == VoyagePhase.Sailing || Session.Crew[local].OnShore;
                if (leave || keyboard?.escapeKey.wasPressedThisFrame == true || keyboard?.eKey.wasPressedThisFrame == true || pad?.buttonEast.wasPressedThisFrame == true)
                    ClosePurser();
                if (clientMotion != null) clientMotion.SetInput(Vector2.zero, false);
                return;
            }
            if (MiniGameOpen)
            {
                if (clientMotion != null) clientMotion.SetInput(Vector2.zero, false);
                return;
            }
            bool capturedThisFrame = false;
            // Losing the pointer lock (Escape in a browser or the editor) is how the menu is reached without the key reaching the game.
            bool lockLost = lockedLastFrame && Cursor.lockState != CursorLockMode.Locked && Application.isFocused;
            lockedLastFrame = Cursor.lockState == CursorLockMode.Locked;
            if (escape || lockLost)
            {
                OpenMenu();
                if (clientMotion != null) clientMotion.SetInput(Vector2.zero, false);
                return;
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
                UpdateSignals(keyboard, pad, local);
                Vector2 look = Mouse.current != null ? Mouse.current.delta.ReadValue() * (MouseSensitivity * GameSettings.LookSpeed) : Vector2.zero;
                if (pad != null) look += pad.rightStick.ReadValue() * (StickLookSpeed * GameSettings.LookSpeed * Time.unscaledDeltaTime);
                if (WheelOpen) look = Vector2.zero;
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
                    int passenger = Target.Kind == InteractionKind.Passenger ? Target.Index : -1;
                    if (passenger >= 0 && (keyboard?.rKey.wasPressedThisFrame == true || pad?.dpad.down.wasPressedThisFrame == true))
                        Session.DeckCommandRpc(Session.Passengers[passenger].TicketChecked && !Session.Passengers[passenger].FarePaid ? DeckAction.WorkFare : DeckAction.CalmPassenger, passenger);
                    if (Target.Kind == InteractionKind.Station && (keyboard?.rKey.wasPressedThisFrame == true || pad?.dpad.down.wasPressedThisFrame == true))
                    {
                        var kind = Session.Stations[Target.Index].Kind;
                        if (kind == StationKind.RepairSupply) Session.DeckCommandRpc(DeckAction.TakeExtinguisher);
                        else if (kind == StationKind.Power) Session.DeckCommandRpc(DeckAction.IsolateFire);
                        else if (kind == StationKind.Repair) Session.DeckCommandRpc(DeckAction.SelectValve);
                    }
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
                    else if (Target.Kind == InteractionKind.DeckTask) Session.DeckCommandRpc((DeckAction)Target.Index);
                    else if (Target.Kind == InteractionKind.Salvage)
                        Session.PickUpSalvageRpc(Target.Index);
                    else if (Target.Kind == InteractionKind.Ladder)
                        Session.LadderRpc(Target.Index);
                    else if (Target.Kind == InteractionKind.Activity)
                        Session.ActivityRpc(Target.ActivityKind, Target.ActivitySpot);
                    else if (Target.Kind == InteractionKind.Station && Session.Stations[Target.Index].Kind == StationKind.Repair && HammerWouldHelp())
                        Session.ActivityRpc(MiniGameKind.Hammer, 0);
                    else if (Target.Kind == InteractionKind.Station && Session.Stations[Target.Index].Kind == StationKind.Manifest)
                    {
                        if (Session.Voyage.Value.Phase != VoyagePhase.Sailing) OpenPurser();
                    }
                    else if (Target.Kind == InteractionKind.Harbor)
                    {
                        var action = Target.HarborAction;
                        if (Session.Voyage.Value.DockJobActive && (action == HarborAction.BuyFood || VoyageSimulation.IsPortCargo(Session.Crew[local].Carrying)))
                            Session.DockItemCommandRpc(action, Session.Voyage.Value.DockJobType);
                        else if (action != HarborAction.GoAshore && action != HarborAction.BoardShip) Session.HarborCommandRpc(action);   // the gate opens by walking into it
                    }
                    else Session.InteractRpc(Target.InteractionId(Session.Stations.Length), false);
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
            bool diveHeld = Application.isFocused && Cursor.lockState == CursorLockMode.Locked && !WheelOpen && Session.Crew[local].Swimming &&
                (keyboard?.spaceKey.isPressed == true || pad?.buttonWest.isPressed == true);
            if (clientMotion != null) clientMotion.SetInput(input, jumpRequested, diveHeld);
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

        private void FindTarget(CrewState crew) =>
            Target = InteractionTargeting.Find(Session, crew, Camera.transform.position, Camera.transform.forward);

        /// <summary>
        /// Which way a passenger body faces: toward where they walk, unchanged while they are being handled or lying down and
        /// back to the seat's orientation once seated. Smoothed so the turn on arrival is not a snap.
        /// </summary>
        /// <summary>Standing height in a walking area: the deck, the quay, or the slope of the gangway plank between them.</summary>
        private static float WalkHeight(Vector2 position, bool onShore)
        {
            Vector2 shipLocal = onShore ? GangwayPassage.ShoreToShip(position) : position;
            if (GangwayPassage.InLane(shipLocal) && shipLocal.x < GangwayPassage.DeckEdgeX)
                return GangwayPassage.PlankHeight(shipLocal, .5f, VoyageSimulation.ShoreHeight);
            return onShore ? VoyageSimulation.ShoreHeight : .5f;
        }

        private Quaternion PassengerFacing(int index, in PassengerState p, Quaternion current, Vector3 walkVelocity)
        {
            Quaternion desired = current;
            var flat = new Vector3(walkVelocity.x, 0, walkVelocity.z);
            bool walking = flat.sqrMagnitude > PassengerFaceWalkSpeed * PassengerFaceWalkSpeed;
            if (p.Motion == PassengerMotion.Returning || p.Motion == PassengerMotion.Boarding || p.Motion == PassengerMotion.Leaving)
            {
                if (walking) desired = Quaternion.LookRotation(flat, Vector3.up);
            }
            else if (p.Motion == PassengerMotion.OnDeck && p.Routine != PassengerRoutine.Seated)
            {
                // Up and about on deck: face the way they walk; standing to watch keeps the last heading.
                if (walking) desired = Quaternion.LookRotation(flat, Vector3.up);
                else if (p.Routine == PassengerRoutine.Working)
                {
                    var toWork = new Vector3(p.RoutineTarget.x - p.Position.x, 0, p.RoutineTarget.y - p.Position.z);
                    if (toWork.sqrMagnitude > .01f) desired = Quaternion.LookRotation(toWork, Vector3.up);
                }
            }
            else if (p.Motion == PassengerMotion.OnDeck)
            {
                int seat = p.SeatClaim == 0 ? index : p.SeatClaim - 1;
                desired = passengerVisualRotations[Mathf.Clamp(seat, 0, passengerVisualRotations.Length - 1)];
            }
            else if (p.Motion == PassengerMotion.Rescuing && p.CanSwim && new Vector2(p.Velocity.x, p.Velocity.z).sqrMagnitude > .04f)
                desired = Quaternion.LookRotation(new Vector3(p.Velocity.x, 0, p.Velocity.z), Vector3.up);
            else if (p.Motion == PassengerMotion.Carried || p.Motion == PassengerMotion.Airborne || p.Motion == PassengerMotion.Rescuing)
                return current;
            return Quaternion.Slerp(current, desired, 1 - Mathf.Exp(-12f * Time.deltaTime));
        }

        private void LateUpdate()
        {
            if (Session == null || Camera == null) return;
            bool active = Session != null && Session.IsSpawned;
            int local = active ? Session.LocalCrewIndex : -1;
            // The lobby is an interface over a calm view of the ship: no look, no movement, a free cursor.
            bool firstPerson = local >= 0 && !Session.InLobby;
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
                if (!show) { crewWorldKnown[i] = false; continue; }
                var crew = Session.Crew[i];
                Vector3 netPosition = default;
                bool netShore = false;
                bool fromNet = clientMotion != null && clientMotion.TryGetCrew(i, out netPosition, out crewNetVelocity[i], out netShore);
                // The predicted or interpolated position can be in the other area a moment before the crew list says so (walking
                // the gangway): place the body in the area its coordinates belong to.
                bool onShore = fromNet ? netShore : crew.OnShore;
                crewDepth[i] = Mathf.Lerp(crewDepth[i], crew.Swimming ? crew.Depth : 0, 1f - Mathf.Exp(-14f * dt));
                float swimHeight = VoyageSimulation.SeaLevel + SwimBodyOffset - crewDepth[i];
                var target = new Vector3(crew.Position.x, (crew.Swimming ? swimHeight : WalkHeight(crew.Position, crew.OnShore)) + crew.JumpHeight, crew.Position.y);
                // Smooth in the frame the crew member walks in (deck or quay), then place that frame; smoothing in world space made
                // crew slide across the deck whenever the ship moved. The area change at the gangway is exact, so a snap is invisible.
                var motion = crewMotion[i];
                if (crewSmootherShore[i] != crew.OnShore) motion.Snap(target, now);
                crewSmootherShore[i] = crew.OnShore;
                Vector3 local3 = motion.Step(target, now, dt);
                if (fromNet)
                {
                    var flat = new Vector2(netPosition.x, netPosition.z);
                    local3 = new Vector3(netPosition.x, (crew.Swimming ? swimHeight : WalkHeight(flat, onShore)) + netPosition.y, netPosition.z);
                    if (!crewFromNet[i]) crewBlend[i] = BlendFrom(crewLastRender[i], local3);
                }
                else if (crewFromNet[i]) crewBlend[i] = BlendFrom(crewLastRender[i], local3);
                if (displayedShore[i] != onShore) crewBlend[i] = Vector3.zero;
                crewFromNet[i] = fromNet;
                crewBlend[i] *= Mathf.Exp(-9f * dt);
                local3 += crewBlend[i];
                crewLastRender[i] = local3;
                Vector3 world = onShore ? VoyageSimulation.HarborOrigin(state) + local3 : ship.TransformPoint(local3);
                // A change of area by ladder (or any leftover difference from the gangway) glides instead of jumping.
                if (crewWorldKnown[i] && (displayedShore[i] != onShore || displayedSwim[i] != crew.Swimming))
                {
                    Vector3 jump = crewWorldLast[i] - world;
                    crewGlide[i] = jump.magnitude <= GateGlideMaxDistance ? jump : Vector3.zero;
                }
                crewGlide[i] *= Mathf.Exp(-GateGlideRate * dt);
                CrewRoots[i].position = world + crewGlide[i];
                crewWorldLast[i] = CrewRoots[i].position;
                crewWorldKnown[i] = true;
                CrewRoots[i].rotation = onShore ? Quaternion.identity : ship.rotation;
                displayedShore[i] = onShore;
                displayedSwim[i] = crew.Swimming;
                bool me = local == i;
                // Hide only the local body, never the root or another client's representation.
                // The local body is hidden in first person, but visible when the helm uses the chase camera.
                if (crewVisuals[i] != null) crewVisuals[i].gameObject.SetActive(!me || ChaseCameraActive || Session.InLobby);
                CrewLabels[i].gameObject.SetActive(!me && !Session.InLobby);
                string crewName = LobbyRules.DisplayName(Session.Profiles.Count > i ? Session.Profiles[i].Name.ToString() : null, i);
                CrewLabels[i].text = $"{crewName}{(me ? " (YOU)" : "")}{(crew.AtHelm ? " / HELM" : "")}";
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
            LocalEyeDepth = 0;
            Camera.fieldOfView = firstPerson ? FirstPersonFieldOfView : 55;
            Camera.nearClipPlane = .05f;
            if (ChaseCameraActive) PlaceChaseCamera(ship, state, dt);
            else if (firstPerson)
            {
                bool atCrane = Session.Crew[local].AtCrane;
                if (atCrane && !wasAtCrane) { lookYaw = 236; lookPitch = 60; }
                if (!atCrane && wasAtCrane) lookPitch = 12;
                wasAtCrane = atCrane;
                Vector3 eye = CrewRoots[local].position + (atCrane ? CraneCameraOffset : ship.up * EyeHeight);
                // A swimmer's eyes sit just above the waves, wherever the body root is hanging.
                if (Session.Crew[local].Swimming) eye = new Vector3(eye.x, VoyageSimulation.SeaLevel + SwimEyeAboveWater - crewDepth[local], eye.z);
                Camera.transform.SetPositionAndRotation(eye,
                    (Session.Crew[local].OnShore ? Quaternion.identity : ship.rotation) * Quaternion.Euler(lookPitch, lookYaw, 0));
                CarriedItems[local].SetPositionAndRotation(Camera.transform.TransformPoint(HeldItemOffset), Camera.transform.rotation);
                FindTarget(Session.Crew[local]);
                LocalEyeDepth = Mathf.Max(0, VoyageSimulation.SeaLevel - eye.y);
            }
            else
            {
                // In the lobby the overview camera drifts slowly around the ship.
                Quaternion orbit = Session.InLobby ? Quaternion.Euler(0, Time.unscaledTime * LobbyOrbitDegreesPerSecond, 0) : Quaternion.identity;
                Camera.transform.position = ship.TransformPoint(orbit * new Vector3(0, 24, -19));
                Camera.transform.LookAt(ship.position);
                Target = InteractionTarget.None;
            }
            foreach (var label in CrewLabels) WorldLabelPresentation.UpdateLabel(label, Camera);
            for (int i = 0; i < PassengerVisuals.Length; i++)
            {
                bool show = active && i < Session.Passengers.Count && Session.Passengers[i].Active;
                PassengerVisuals[i].gameObject.SetActive(show);
                PassengerLabels[i].gameObject.SetActive(show);
                if (!show) { passengerShown[i] = false; continue; }
                var p = Session.Passengers[i];
                // Move presentation children, never the seat/interaction roots used by gameplay.
                bool heldByMe = p.Motion == PassengerMotion.Carried && p.CarrierSlot == local;
                Vector3 localTarget = p.Position + passengerVisualOffsets[i];
                // Appearing, or leaving the local player's arms (drawn in camera space), starts from where the body really is.
                if (!passengerShown[i] || passengerHeldShown[i] != heldByMe) passengerMotion[i].Snap(localTarget, now);
                passengerShown[i] = true;
                passengerHeldShown[i] = heldByMe;
                Vector3 smoothedLocal = passengerMotion[i].Step(localTarget, now, dt);
                PassengerVisuals[i].position = heldByMe ? Camera.transform.TransformPoint(HeldPassengerOffset) : ship.TransformPoint(smoothedLocal);
                passengerFacing[i] = PassengerFacing(i, p, passengerFacing[i], passengerMotion[i].Velocity);
                PassengerVisuals[i].rotation = ship.rotation * passengerFacing[i] *
                    (p.Motion == PassengerMotion.Airborne ? Quaternion.Euler(0, 0, Time.time * 360) : Quaternion.identity);
                if (passengerBuoys[i] != null) passengerBuoys[i].gameObject.SetActive(p.Motion == PassengerMotion.Rescuing && !p.CanSwim);
                if (passengerFloaties[i] != null) passengerFloaties[i].gameObject.SetActive(!p.CanSwim);
                bool focused = Target.IsPassenger(i);
                bool rescuing = p.Motion == PassengerMotion.Rescuing;
                PassengerLabels[i].gameObject.SetActive(!heldByMe && (rescuing || Vector3.Distance(Camera.transform.position, PassengerVisuals[i].position) < 7));
                PassengerLabels[i].transform.position = PassengerVisuals[i].position + ship.rotation * passengerLabelOffsets[i];
                PassengerLabels[i].text = $"{i + 1}: {(p.Request == ServiceItem.None ? "Happy" : p.Request.ToString())} / {p.Satisfaction:0}%{(p.CanSwim ? string.Empty : " (can't swim)")}\n{PassengerPresentation.TicketText(p)}";
                if (focused || rescuing) PassengerLabels[i].text += "\n" + PassengerPresentation.ReactionText(p);
                if (focused) PassengerLabels[i].text += $"\n{p.Personality} / {p.Routine}";
                if (focused && p.Motion == PassengerMotion.OnDeck)
                    PassengerLabels[i].text += "\n" + PassengerPresentation.CueText(PassengerPresentation.CueFor(p, Session.ActiveEvent.Value, state, i));
                PassengerLabels[i].color = p.Motion == PassengerMotion.Rescuing || (p.TicketChecked && !p.FarePaid) ? new Color(1, .65f, .2f) : Color.white;
                WorldLabelPresentation.UpdateLabel(PassengerLabels[i], Camera);
            }
            LeakVisual.gameObject.SetActive(active && state.Leak != 0);
            foreach (var station in Session.Stations)
            {
                WorldLabelPresentation.UpdateLabel(station.Label, Camera);
                station.Label.gameObject.SetActive((local < 0 || !Session.Crew[local].OnShore) && !VoyageSimulation.IsUpgrade(station.Kind) && Vector3.Distance(station.Label.transform.position, Camera.transform.position) < 10);
                station.Label.color = Target.Kind == InteractionKind.Station && Session.Stations[Target.Index] == station ? Color.yellow : Color.white;
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
            Target = InteractionTarget.None;
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
            if (serviceItemVisuals[slot] != null) serviceItemVisuals[slot].gameObject.SetActive(item != ServiceItem.Ammo && item != ServiceItem.Extinguisher && item != ServiceItem.TemporaryPlug && !luggage && !repairKit && !salvage && !craneCrate);
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
