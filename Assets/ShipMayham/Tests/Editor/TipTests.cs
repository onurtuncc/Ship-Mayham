using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class TipTests
    {
        private bool tipsBefore;

        [SetUp]
        public void SetUp()
        {
            tipsBefore = GameSettings.Tips;
            PlayerPrefs.DeleteKey(TipStore.Key);
            TipStore.Reload();
            GameSettings.Tips = true;
        }

        [TearDown]
        public void TearDown()
        {
            GameSettings.Tips = tipsBefore;
            PlayerPrefs.DeleteKey(TipStore.Key);
            TipStore.Reload();
        }

        private static TipContext Boarding() => new TipContext { InVoyage = true, Phase = VoyagePhase.Boarding };

        private static int Mask(params TipId[] ids)
        {
            int mask = 0;
            foreach (var id in ids) mask |= 1 << (int)id;
            return mask;
        }

        [Test]
        public void NothingShowsOutsideAVoyage()
        {
            var c = Boarding();
            c.InVoyage = false;
            Assert.IsNull(TipLogic.Pick(c, 0));
        }

        [Test]
        public void FirstTipIsWelcome_ThenTakeHelmOnceBoarded()
        {
            var c = Boarding();
            Assert.AreEqual(TipId.Welcome, TipLogic.Pick(c, 0));
            c.Boarded = 3;
            Assert.AreEqual(TipId.TakeHelm, TipLogic.Pick(c, Mask(TipId.Welcome)));
        }

        [Test]
        public void SeenTipsAreNeverPickedAgain()
        {
            var c = Boarding();
            Assert.IsNull(TipLogic.Pick(c, Mask(TipId.Welcome)));
        }

        [Test]
        public void HarborTipNeedsShore()
        {
            var c = Boarding();
            c.OnShore = true;
            Assert.AreEqual(TipId.Harbor, TipLogic.Pick(c, 0));
            c.Phase = VoyagePhase.Sailing;
            Assert.IsNull(TipLogic.Pick(c, 0));
        }

        [Test]
        public void LeakAndPirateTipsOnlyWhileSailingAboard()
        {
            var c = Boarding();
            c.Phase = VoyagePhase.Sailing;
            c.Leak = true;
            Assert.AreEqual(TipId.Leak, TipLogic.Pick(c, 0));
            c.Leak = false;
            c.PiratesNear = true;
            Assert.AreEqual(TipId.Pirates, TipLogic.Pick(c, 0));
            c.AtCannon = true;
            Assert.IsFalse(TipLogic.Applies(TipId.Pirates, c));
            c.AtCannon = false;
            c.Phase = VoyagePhase.Arrived;
            Assert.IsFalse(TipLogic.Applies(TipId.Pirates, c));
        }

        [Test]
        public void DockingTipNeedsHelmNearThePort()
        {
            var c = Boarding();
            c.Phase = VoyagePhase.Sailing;
            c.AtHelm = true;
            c.DistanceToPort = TipLogic.DockingHintMeters + 1;
            Assert.IsFalse(TipLogic.Applies(TipId.Docking, c));
            c.DistanceToPort = TipLogic.DockingHintMeters - 1;
            Assert.IsTrue(TipLogic.Applies(TipId.Docking, c));
            c.AtHelm = false;
            Assert.IsFalse(TipLogic.Applies(TipId.Docking, c));
        }

        [Test]
        public void ServeTipNeedsAnOpenOrderAndFreeHands()
        {
            var c = Boarding();
            c.Boarded = 2;
            c.OpenOrders = true;
            Assert.IsTrue(TipLogic.Applies(TipId.ServeOrders, c));
            c.Carrying = true;
            Assert.IsFalse(TipLogic.Applies(TipId.ServeOrders, c));
            c.Carrying = false;
            c.OpenOrders = false;
            Assert.IsFalse(TipLogic.Applies(TipId.ServeOrders, c));
        }

        [Test]
        public void TicketTipNeedsLookingAtAPassenger()
        {
            var c = Boarding();
            Assert.IsFalse(TipLogic.Applies(TipId.CheckTickets, c));
            c.LookingAtPassenger = true;
            Assert.IsTrue(TipLogic.Applies(TipId.CheckTickets, c));
        }

        [Test]
        public void EveryTipHasTextAndOnlyKnownControls()
        {
            foreach (TipId id in System.Enum.GetValues(typeof(TipId)))
            {
                string template = TipLogic.Template(id);
                Assert.IsNotEmpty(template, id.ToString());
                string rendered = TipLogic.Render(template, false);
                StringAssert.DoesNotContain("{", rendered, id + " has an unknown control");
            }
        }

        [Test]
        public void EveryTipIsReachable()
        {
            // Each id must apply in at least one situation, or it could never be shown.
            var cases = new System.Collections.Generic.Dictionary<TipId, TipContext>();
            var c = Boarding();
            cases[TipId.Welcome] = c;
            c.Boarded = 1; cases[TipId.TakeHelm] = c;
            c.OnShore = true; cases[TipId.Harbor] = c;
            c = Boarding(); c.Phase = VoyagePhase.Sailing; c.Leak = true; cases[TipId.Leak] = c;
            c = Boarding(); c.Phase = VoyagePhase.Sailing; c.PiratesNear = true; cases[TipId.Pirates] = c;
            c = Boarding(); c.Phase = VoyagePhase.Sailing; c.AtHelm = true; cases[TipId.Docking] = c;
            c = Boarding(); c.Boarded = 1; c.OpenOrders = true; cases[TipId.ServeOrders] = c;
            c = Boarding(); c.LookingAtPassenger = true; cases[TipId.CheckTickets] = c;
            c = Boarding(); c.Phase = VoyagePhase.Sailing; cases[TipId.Signals] = c;
            c = Boarding(); c.Phase = VoyagePhase.Sailing; c.HazardWarning = true; cases[TipId.Hazard] = c;
            c = Boarding(); c.Phase = VoyagePhase.Sailing; c.Swimming = true; cases[TipId.Overboard] = c;
            c = Boarding(); c.NearLadder = true; cases[TipId.Ladder] = c;
            c = Boarding(); c.Swimming = true; cases[TipId.Dive] = c;
            c = Boarding(); c.OnShore = true; cases[TipId.Explore] = c;
            c = Boarding(); c.OnShore = true; c.NearActivity = true; cases[TipId.Activity] = c;
            foreach (TipId id in System.Enum.GetValues(typeof(TipId)))
            {
                Assert.IsTrue(cases.ContainsKey(id), id + " has no reachability case");
                var context = cases[id];
                Assert.IsTrue(TipLogic.Applies(id, context), id.ToString());
            }
        }

        [Test]
        public void PriorityOrderListsEveryTipExactlyOnce()
        {
            foreach (TipId id in System.Enum.GetValues(typeof(TipId)))
                Assert.AreEqual(1, System.Array.FindAll(TipLogic.Order, o => o == id).Length, id.ToString());
            Assert.AreEqual(System.Enum.GetValues(typeof(TipId)).Length, TipLogic.Order.Length);
        }

        [Test]
        public void RenderUsesTheDeviceLabelAndKeepsUnknownBraces()
        {
            string keyboard = TipLogic.Render("Press {Interact} now {Nope}", false);
            string pad = TipLogic.Render("Press {Interact} now", true);
            StringAssert.Contains(HudControls.Label(HudControl.Interact, false), keyboard);
            StringAssert.Contains("{Nope}", keyboard);
            StringAssert.Contains(HudControls.Label(HudControl.Interact, true), pad);
            Assert.AreEqual(string.Empty, TipLogic.Render(null, false));
        }

        [Test]
        public void StorePersistsAndResets()
        {
            Assert.IsFalse(TipStore.HasSeen(TipId.Leak));
            TipStore.MarkSeen(TipId.Leak);
            TipStore.Reload();
            Assert.IsTrue(TipStore.HasSeen(TipId.Leak));
            Assert.IsFalse(TipStore.HasSeen(TipId.Welcome));
            TipStore.ResetAll();
            TipStore.Reload();
            Assert.IsFalse(TipStore.HasSeen(TipId.Leak));
        }

        [Test]
        public void CardShowsOnceThenMarksSeen()
        {
            var parent = new GameObject("TipParent", typeof(RectTransform));
            try
            {
                var card = new TipCard(parent.transform);
                var c = Boarding();
                card.Tick(10f, () => c, false, false);
                Assert.IsTrue(card.Visible);
                Assert.AreEqual(TipId.Welcome, card.Current);
                Assert.IsTrue(TipStore.HasSeen(TipId.Welcome));

                card.Tick(10f + TipCard.ShowSeconds + .1f, () => c, false, false);
                Assert.IsFalse(card.Visible, "expired");
                card.Tick(10f + TipCard.ShowSeconds + 5f, () => c, false, false);
                Assert.IsFalse(card.Visible, "a seen tip does not come back");
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void CardStaysHiddenWhenTipsAreOffOrBlocked()
        {
            var parent = new GameObject("TipParent", typeof(RectTransform));
            try
            {
                var card = new TipCard(parent.transform);
                var c = Boarding();
                GameSettings.Tips = false;
                card.Tick(10f, () => c, false, false);
                Assert.IsFalse(card.Visible);
                GameSettings.Tips = true;
                card.Tick(11f, () => c, false, true);
                Assert.IsFalse(card.Visible);
                Assert.IsFalse(TipStore.HasSeen(TipId.Welcome), "nothing is consumed while hidden");
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void UrgentTipInterruptsACalmOne()
        {
            var parent = new GameObject("TipParent", typeof(RectTransform));
            try
            {
                var card = new TipCard(parent.transform);
                var c = Boarding();
                card.Tick(10f, () => c, false, false);
                Assert.AreEqual(TipId.Welcome, card.Current);
                c.Phase = VoyagePhase.Sailing;
                c.Leak = true;
                card.Tick(11f, () => c, false, false);
                Assert.AreEqual(TipId.Leak, card.Current);
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
