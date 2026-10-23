using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShipMayham.Tests
{
    public sealed class HudPromptTests
    {
        private static VoyageState Voyage(VoyagePhase phase = VoyagePhase.Boarding) => new VoyageState { Phase = phase };

        [Test]
        public void StationPromptNamesTheObjectAndTheAction()
        {
            var voyage = Voyage();
            voyage.FoodStock = 14;
            var prompt = PromptCatalog.ForStation(StationKind.Food, voyage, default, default);
            Assert.IsTrue(prompt.Targeted);
            Assert.AreEqual(HudControl.Interact, prompt.Key);
            Assert.That(prompt.Title, Does.Contain("food"));
            Assert.AreEqual("Take food", prompt.Action);
            Assert.That(prompt.Detail, Does.Contain("14"));
        }

        [Test]
        public void UpgradeShowsItsCostAndIsBlockedAtSea()
        {
            var port = PromptCatalog.ForStation(StationKind.EngineUpgrade, Voyage(), default, default);
            Assert.AreEqual(HudControl.Interact, port.Key);
            Assert.AreEqual(PromptTone.Normal, port.Tone);
            Assert.That(port.Cost, Does.EndWith("coins"));

            var sea = PromptCatalog.ForStation(StationKind.EngineUpgrade, Voyage(VoyagePhase.Sailing), default, default);
            Assert.AreEqual(HudControl.None, sea.Key, "A blocked prompt must not advertise a key.");
            Assert.AreEqual(PromptTone.Blocked, sea.Tone);
        }

        [Test]
        public void FullyUpgradedStationOffersNoAction()
        {
            var voyage = Voyage();
            voyage.EngineLevel = 2;
            var prompt = PromptCatalog.ForStation(StationKind.EngineUpgrade, voyage, default, default);
            Assert.AreEqual(HudControl.None, prompt.Key);
            Assert.IsNull(prompt.Cost);
        }

        [Test]
        public void RepairBenchPromptFollowsLeakAndKitState()
        {
            var voyage = Voyage(VoyagePhase.Sailing);
            Assert.AreEqual(HudControl.None, PromptCatalog.ForStation(StationKind.Repair, voyage, default, default).Key, "Nothing to repair.");

            voyage.Leak = 1;
            Assert.AreEqual("Patch the leak", PromptCatalog.ForStation(StationKind.Repair, voyage, default, default).Action);

            voyage.ReadyRepairKits = 1;
            Assert.AreEqual("Use quick patch", PromptCatalog.ForStation(StationKind.Repair, voyage, default, default).Action);

            var carrying = new CrewState { Carrying = ServiceItem.RepairKit };
            Assert.AreEqual("Deliver patch kit", PromptCatalog.ForStation(StationKind.Repair, voyage, default, carrying).Action);
        }

        [Test]
        public void PassengerPromptPrioritisesOrdersThenTicketsAndAlwaysOffersLift()
        {
            var ordering = new PassengerState { Active = true, Request = ServiceItem.Food };
            var order = PromptCatalog.ForPassenger(2, ordering);
            Assert.AreEqual("Passenger 3", order.Title);
            Assert.AreEqual(HudControl.Interact, order.Key);
            Assert.AreEqual("Serve food", order.Action);
            Assert.AreEqual(HudControl.Check, order.Hint1.Control, "An unchecked ticket stays reachable while an order is pending.");

            var unchecked_ = PromptCatalog.ForPassenger(0, new PassengerState { Active = true });
            Assert.AreEqual(HudControl.Check, unchecked_.Key);
            Assert.AreEqual(HudControl.Carry, unchecked_.Hint1.Control);

            var due = PromptCatalog.ForPassenger(0, new PassengerState { Active = true, TicketChecked = true });
            Assert.AreEqual("Collect fare", due.Action);
            Assert.AreEqual("+40 coins", due.Cost);

            var settled = PromptCatalog.ForPassenger(0, new PassengerState { Active = true, TicketChecked = true, FarePaid = true });
            Assert.AreEqual(HudControl.Carry, settled.Key);
        }

        [Test]
        public void HarborPromptsAdaptToTheActiveDockJob()
        {
            var voyage = Voyage();
            Assert.AreEqual("Buy food", PromptCatalog.ForHarbor(HarborAction.BuyFood, voyage, default).Action);
            Assert.AreEqual("12 coins", PromptCatalog.ForHarbor(HarborAction.BuyFood, voyage, default).Cost);

            voyage.DockJobActive = true;
            voyage.DockJobType = DockJobKind.CargoRelay;
            Assert.AreEqual("Pick up cargo crate", PromptCatalog.ForHarbor(HarborAction.BuyFood, voyage, default).Action);
            var carrying = new CrewState { OnShore = true, Carrying = ServiceItem.Cargo };
            Assert.AreEqual("Deliver cargo crate", PromptCatalog.ForHarbor(HarborAction.MaintainHull, voyage, carrying).Action);
            Assert.AreEqual("Buy hull shield", PromptCatalog.ForHarbor(HarborAction.MaintainHull, voyage, default).Action, "Without cargo the dock keeps its normal service.");
        }

        [Test]
        public void ContextPromptsAreNotTargeted()
        {
            foreach (var prompt in new[] { PromptCatalog.ForCannon(), PromptCatalog.ForHelm(), PromptCatalog.ForCrane(), PromptCatalog.ForCarriedPassenger() })
            {
                Assert.IsFalse(prompt.Targeted);
                Assert.IsFalse(prompt.IsEmpty);
            }
            Assert.IsTrue(default(InteractionPrompt).IsEmpty);
        }

        [Test]
        public void ControlGlyphsFollowTheInputDevice()
        {
            Assert.AreEqual("E", HudControls.Label(HudControl.Interact, false));
            Assert.AreEqual("A", HudControls.Label(HudControl.Interact, true));
            Assert.AreEqual("F", HudControls.Label(HudControl.Check, false));
            Assert.AreEqual("RT", HudControls.Label(HudControl.Fire, true));
            foreach (HudControl control in System.Enum.GetValues(typeof(HudControl)))
                if (control != HudControl.None) Assert.IsNotEmpty(HudControls.Label(control, false), control.ToString());
        }

        [Test]
        public void LobbyScreenBuildsAndRefreshesWithoutASession()
        {
            var host = new GameObject("HudHost");
            try
            {
                var hud = host.AddComponent<PrototypeHud>();
                var controller = HudController.Create(hud);
                Canvas.ForceUpdateCanvases();
                Assert.IsNotNull(controller.GetComponentInChildren<Canvas>());
                var menu = controller.transform.Find("MenuScreen");
                Assert.IsNotNull(menu);
                Assert.IsNotNull(controller.transform.Find("GameHud"));
                // Update runs the same refresh the player would see on the first frame.
                typeof(HudController).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Assert.IsTrue(menu.gameObject.activeSelf);
                Assert.IsFalse(controller.transform.Find("GameHud").gameObject.activeSelf);
                Assert.IsNotNull(menu.GetComponentInChildren<InputField>(true));
                Assert.AreEqual(4, menu.GetComponentsInChildren<Button>(true).Length, "Create, Join, Solo and Cancel.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                var eventSystem = Object.FindAnyObjectByType<EventSystem>();
                if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
            }
        }
    }
}
