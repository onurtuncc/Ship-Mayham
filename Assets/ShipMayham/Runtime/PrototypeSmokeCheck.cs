#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>Opt-in, development-build network smoke test; inactive during ordinary play.</summary>
    public sealed class PrototypeSmokeCheck : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--ship-smoke-host") < 0 && Array.IndexOf(args, "--ship-smoke-client") < 0 && Array.IndexOf(args, "--ship-preview") < 0) return;
            new GameObject("Development_NetworkSmokeCheck").AddComponent<PrototypeSmokeCheck>();
        }

        private PrototypeSession session;
        private PrototypeView view;
        private float deadline;
        private bool sawSailing, sawFour, sawPowerOut, sawPiratesDefeated, sawCarriedPassenger, sawRescue, sawShore;
        private readonly bool[] sawJump = new bool[VoyageSimulation.MaxCrew];

        private IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartup + 130;
            var hud = FindAnyObjectByType<PrototypeHud>();
            session = FindAnyObjectByType<PrototypeSession>();
            view = FindAnyObjectByType<PrototypeView>();
            if (hud == null || session == null) { Fail("Missing scene references"); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--ship-preview") >= 0)
            {
                hud.Connect(true);
                while (!session.IsSpawned || session.LocalCrewIndex < 0) yield return null;
                session.Simulation.Crew[0].Position = new Vector2(-3, -4);
                session.InteractRpc(0, false);
                yield return new WaitForSeconds(2);
                bool harborPreview = Array.IndexOf(Environment.GetCommandLineArgs(), "--ship-preview-harbor") >= 0;
                if (harborPreview)
                {
                    session.Simulation.Crew[0].Position = VoyageSimulation.ShipGangway;
                    session.HarborCommandRpc(HarborAction.GoAshore);
                    session.Simulation.Crew[0].Position = new Vector2(1.5f, -9);
                    yield return new WaitForSeconds(1);
                }
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../ShipPrototype-Gameplay.png"));
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForSeconds(2);
                Debug.Log("SHIP_PREVIEW " + path);
                // Off-screen rendering is usable even when a hidden window has no presented backbuffer.
                string scenePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                    harborPreview ? "../ShipPrototype-Harbor-Camera.png" : "../ShipPrototype-Deck-Camera.png"));
                CaptureCamera(view.Camera, scenePath);
                Application.Quit(0);
                yield break;
            }
            view.enabled = false; // Test inputs are the sole input source.
            bool host = Array.IndexOf(Environment.GetCommandLineArgs(), "--ship-smoke-host") >= 0;
            hud.Connect(host, "127.0.0.1");
            while (!session.IsSpawned || session.LocalCrewIndex < 0) yield return null;
            Debug.Log("SHIP_SMOKE_CONNECTED " + (host ? "HOST" : "CLIENT") + " " + session.LocalCrewIndex);
            if (!host)
            {
                // Exercise input RPCs from each remote client. Spawn corridor is clear.
                float until = Time.realtimeSinceStartup + .5f;
                while (Time.realtimeSinceStartup < until)
                {
                    session.MoveRpc(Vector2.up);
                    yield return new WaitForSeconds(.05f);
                }
                session.MoveRpc(Vector2.zero);
                float movedZ = session.Crew[session.LocalCrewIndex].Position.y;
                if (movedZ <= -5.5f) { Fail("Remote input did not move crew"); yield break; }
                int slot = session.LocalCrewIndex;
                session.JumpRpc();
                float jumpDeadline = Time.realtimeSinceStartup + 2;
                while (!sawJump[slot] && Time.realtimeSinceStartup < jumpDeadline) yield return null;
                if (!sawJump[slot]) { Fail("Remote jump was not replicated"); yield break; }
                while (session.Crew[slot].JumpHeight > 0 || session.Crew[slot].VerticalSpeed > 0) yield return null;
                Debug.Log("SHIP_SMOKE_JUMP CLIENT " + slot);
                if (slot == 1 || slot == 2)
                {
                    yield return WalkTo(new Vector2(3.1f, -4.8f));
                    yield return WalkTo(new Vector2(3.1f, slot == 1 ? 4.7f : -1.2f));
                }
                if (slot == 1)
                {
                    session.InteractRpc(FindStation(StationKind.Cannon), false);
                    yield return new WaitForSeconds(.5f);
                    if (!session.Crew[slot].AtCannon) { Fail("Remote gunner could not claim cannon"); yield break; }
                }
                if (slot == 2)
                {
                    for (int round = 0; round < 2; round++)
                    {
                        yield return WalkTo(new Vector2(3.1f, 1.8f));
                        session.InteractRpc(FindStation(StationKind.Ammo), false);
                        yield return new WaitForSeconds(.5f);
                        if (session.Crew[slot].Carrying != ServiceItem.Ammo) { Fail("Remote magazine pickup failed"); yield break; }
                        yield return WalkTo(new Vector2(3.1f, 4.7f));
                        session.InteractRpc(FindStation(StationKind.Cannon), false);
                        yield return new WaitForSeconds(.5f);
                        if (session.Crew[slot].Carrying != ServiceItem.None || session.Crew[slot].AtCannon)
                        { Fail("Remote cannon delivery failed or stole gunner ownership"); yield break; }
                    }
                    Debug.Log("SHIP_SMOKE_AMMO_SUPPLY CLIENT delivered=2");
                    yield return WalkTo(new Vector2(3.1f, -1.2f));
                }
                if (slot == 3)
                {
                    while (session.Voyage.Value.Boarded == 0) yield return null;
                    yield return WalkTo(new Vector2(0, -4.8f));
                    yield return WalkTo(new Vector2(-3, -4.8f));
                    yield return WalkTo(new Vector2(-3, -1.5f));
                    session.PassengerCommandRpc(0, PassengerAction.CheckTicket, 0);
                    yield return new WaitForSeconds(.5f);
                    session.PassengerCommandRpc(0, PassengerAction.CheckTicket, 0);
                    yield return new WaitForSeconds(.5f);
                    while (session.Voyage.Value.Served < 1) yield return null;
                    session.PassengerCommandRpc(0, PassengerAction.CarryOrDrop, 0);
                    yield return new WaitForSeconds(.5f);
                    if (session.LocalCarriedPassengerIndex != 0) { Fail("Remote passenger pickup failed"); yield break; }
                    session.PassengerCommandRpc(-1, PassengerAction.CarryOrDrop, 0);
                    yield return new WaitForSeconds(.5f);
                    if (session.Passengers[0].Motion != PassengerMotion.OnDeck) { Fail("Remote passenger drop failed"); yield break; }
                    session.PassengerCommandRpc(0, PassengerAction.CarryOrDrop, 0);
                    yield return new WaitForSeconds(.5f);
                    yield return WalkTo(new Vector2(-5, -1.5f));
                    session.PassengerCommandRpc(-1, PassengerAction.Throw, -90);
                    yield return new WaitForSeconds(.5f);
                    if (session.Passengers[0].Motion != PassengerMotion.Rescuing) { Fail("Remote passenger throw failed"); yield break; }
                    while (session.Passengers[0].Motion != PassengerMotion.OnDeck) yield return null;
                    Debug.Log("SHIP_SMOKE_PASSENGER_FLOW CLIENT " + slot);
                }
                while (session.Voyage.Value.Phase != VoyagePhase.Arrived)
                {
                    var encounter = session.Encounter.Value;
                    if (slot == 1 && encounter.Pirate == PiratePhase.Attacking)
                    {
                        Vector2 aim = PirateAim();
                        session.AimCannonRpc(aim);
                        if (encounter.CannonLoaded) session.CannonActionRpc(aim, false);
                        else if (encounter.ReloadRemaining <= 0) session.CannonActionRpc(aim, true);
                    }
                    if (slot == 2 && encounter.PowerOut) session.InteractRpc(FindStation(StationKind.Power), false);
                    yield return new WaitForSeconds(.4f);
                }
                if (!sawFour || !sawSailing || !sawPowerOut || !sawPiratesDefeated || session.Voyage.Value.LastBounty != 50 || session.Voyage.Value.Wallet <= 0 || !PassengerSettlementValid())
                { Fail("Client snapshot missing crew, power, combat or settlement"); yield break; }
                int settledWallet = session.Voyage.Value.Wallet;
                Debug.Log("SHIP_SMOKE_VOYAGE CLIENT wallet=" + settledWallet + " bounty=" + session.Voyage.Value.LastBounty);
                if (slot == 3)
                {
                    yield return WalkTo(VoyageSimulation.ShipGangway);
                    session.HarborCommandRpc(HarborAction.GoAshore);
                    yield return new WaitForSeconds(.5f);
                    if (!session.Crew[slot].OnShore) { Fail("Remote gangway disembark failed"); yield break; }
                    yield return WalkTo(new Vector2(2, -6));
                    yield return WalkTo(new Vector2(2, -7));
                    session.HarborCommandRpc(HarborAction.BuyFood);
                    yield return new WaitForSeconds(.5f);
                    yield return WalkTo(new Vector2(2, 0));
                    session.HarborCommandRpc(HarborAction.BuyDrink);
                    yield return new WaitForSeconds(.5f);
                    yield return WalkTo(new Vector2(2, 7));
                    session.HarborCommandRpc(HarborAction.MaintainHull);
                    yield return new WaitForSeconds(.5f);
                    yield return WalkTo(new Vector2(2, -6));
                    yield return WalkTo(VoyageSimulation.HarborPoint(HarborAction.BoardShip));
                    session.HarborCommandRpc(HarborAction.BoardShip);
                    yield return new WaitForSeconds(.5f);
                    Debug.Log("SHIP_SMOKE_HARBOR CLIENT " + slot);
                }
                while (session.Voyage.Value.Voyage < 2) yield return null;
                if (!HarborPreparationValid(settledWallet)) { Fail("Client harbor/reset snapshot mismatch"); yield break; }
                Debug.Log("SHIP_SMOKE_PASS CLIENT wallet=" + session.Voyage.Value.Wallet + " food=" + session.Voyage.Value.FoodStock + " drink=" + session.Voyage.Value.DrinkStock + " shield=" + session.Voyage.Value.HullShield);
                yield return new WaitForSeconds(3);
                Application.Quit(0);
                yield break;
            }
            while (!sawFour) yield return null;
            session.JumpRpc();
            yield return new WaitForSeconds(.8f);
            yield return WalkTo(new Vector2(-3,-4));
            session.InteractRpc(0, false);
            yield return new WaitForSeconds(.5f);
            if (session.Voyage.Value.Boarded != 4) { Fail("Boarding failed"); yield break; }
            yield return WalkTo(new Vector2(-3,0));
            session.InteractRpc(2, false);
            yield return new WaitForSeconds(.5f);
            yield return WalkTo(new Vector2(-2.9f,-1.5f));
            session.InteractRpc(session.Stations.Length, false);
            yield return new WaitForSeconds(.5f);
            if (session.Voyage.Value.Served != 1) { Fail("Service interaction failed"); yield break; }
            yield return WalkTo(new Vector2(-3,5.7f));
            yield return WalkTo(new Vector2(0,5.7f));
            session.InteractRpc(1, false);
            yield return new WaitForSeconds(.5f);
            session.InteractRpc(-1, true);
            while (session.Voyage.Value.Position.y < 211)
            {
                session.MoveRpc(Vector2.up);
                yield return new WaitForSeconds(.05f);
            }
            while (session.Voyage.Value.Speed > .05f)
            {
                session.MoveRpc(Vector2.down);
                yield return new WaitForSeconds(.05f);
            }
            session.MoveRpc(Vector2.zero);
            session.InteractRpc(-1, true);
            yield return new WaitForSeconds(.5f);
            if (session.Voyage.Value.Phase != VoyagePhase.Arrived || session.Voyage.Value.Wallet <= 0 ||
                !sawPowerOut || !sawPiratesDefeated || session.Encounter.Value.PowerRepairSteps != 3 || session.Voyage.Value.LastBounty != 50 ||
                Array.Exists(sawJump, seen => !seen) || !PassengerSettlementValid())
            { Fail("Arrival/settlement failed"); yield break; }
            int arrivalWallet = session.Voyage.Value.Wallet;
            Debug.Log("SHIP_SMOKE_VOYAGE HOST wallet=" + arrivalWallet + " bounty=" + session.Voyage.Value.LastBounty);
            while (!sawShore || session.Crew[3].OnShore || session.Voyage.Value.HullShield < 20) yield return null;
            yield return WalkTo(new Vector2(-3, 5.7f));
            yield return WalkTo(new Vector2(-3, -4));
            session.InteractRpc(0, false);
            yield return new WaitForSeconds(.5f);
            if (!HarborPreparationValid(arrivalWallet)) { Fail("Host harbor/reset snapshot mismatch"); yield break; }
            Debug.Log("SHIP_SMOKE_PASS HOST wallet=" + session.Voyage.Value.Wallet + " food=" + session.Voyage.Value.FoodStock + " drink=" + session.Voyage.Value.DrinkStock + " shield=" + session.Voyage.Value.HullShield);
            yield return new WaitForSeconds(6);
            Application.Quit(0);
        }

        private IEnumerator WalkTo(Vector2 point)
        {
            while (Vector2.Distance(session.Crew[session.LocalCrewIndex].Position, point) > .15f)
            {
                Vector2 direction = point - session.Crew[session.LocalCrewIndex].Position;
                session.MoveRpc(Vector2.ClampMagnitude(direction * 3, 1));
                yield return new WaitForSeconds(.05f);
            }
            session.MoveRpc(Vector2.zero);
        }

        private static void CaptureCamera(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 800, 24);
            var texture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                texture.Apply();
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log("SHIP_CAMERA_PREVIEW " + path);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Destroy(texture); Destroy(target);
            }
        }

        private int FindStation(StationKind kind) => Array.FindIndex(session.Stations, s => s.Kind == kind);

        private bool PassengerSettlementValid()
        {
            var state = session.Voyage.Value;
            return sawCarriedPassenger && sawRescue && state.CheckedTickets == 1 && state.CollectedFares == 1 &&
                state.RescueCount == 1 && state.LastRescueCosts == 15 && state.LastTickets == 160 && session.Passengers[0].FarePaid;
        }

        private bool HarborPreparationValid(int settledWallet)
        {
            var s = session.Voyage.Value;
            return sawShore && s.Voyage == 2 && s.Phase == VoyagePhase.Boarding && s.Wallet == settledWallet - 54 &&
                s.FoodStock == 23 && s.DrinkStock == 24 && s.HullShield == 20 && !session.Crew[3].OnShore;
        }

        private Vector2 PirateAim()
        {
            var state = session.Voyage.Value;
            Vector2 station = session.Stations[FindStation(StationKind.Cannon)].DeckPosition(session.ShipRoot);
            Vector3 origin = VoyageSimulation.DeckToWorld(state, new Vector3(station.x, 1.8f, station.y));
            Vector2 pirate = session.Encounter.Value.PiratePosition;
            Vector3 delta = new Vector3(pirate.x, 1.5f, pirate.y) - origin;
            return new Vector2(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg - state.Heading,
                -Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg);
        }

        private void Update()
        {
            if (deadline > 0 && Time.realtimeSinceStartup > deadline) Fail("Timed out");
            if (session == null || !session.IsSpawned) return;
            sawSailing |= session.Voyage.Value.Phase == VoyagePhase.Sailing;
            sawPowerOut |= session.Encounter.Value.PowerOut;
            sawPiratesDefeated |= session.Encounter.Value.Pirate == PiratePhase.Defeated;
            if (session.Passengers.Count > 0)
            {
                sawCarriedPassenger |= session.Passengers[0].Motion == PassengerMotion.Carried;
                sawRescue |= session.Passengers[0].Motion == PassengerMotion.Rescuing;
            }
            int active = 0;
            foreach (var crew in session.Crew) if (crew.Active) active++;
            foreach (var crew in session.Crew) sawShore |= crew.Active && crew.OnShore;
            for (int i = 0; i < session.Crew.Count; i++) sawJump[i] |= session.Crew[i].Active && session.Crew[i].JumpHeight > .1f;
            sawFour |= active == 4;
        }

        private void Fail(string reason)
        {
            Debug.LogError("SHIP_SMOKE_FAIL " + reason);
            Application.Quit(2);
            enabled = false;
        }
    }
}
#endif
