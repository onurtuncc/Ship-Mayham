using NUnit.Framework;

namespace ShipMayham.Tests
{
    public sealed class InteractionTargetTests
    {
        [Test]
        public void InteractionIdKeepsTheWireFormatOfStationsAndPassengers()
        {
            Assert.AreEqual(3, InteractionTarget.Station(3).InteractionId(8));
            Assert.AreEqual(8 + 2, InteractionTarget.Passenger(2).InteractionId(8));
            Assert.AreEqual(-1, InteractionTarget.None.InteractionId(8));
            Assert.AreEqual(-1, InteractionTarget.Harbor(HarborAction.BuyFood).InteractionId(8), "Harbor and salvage use their own commands.");
            Assert.AreEqual(-1, InteractionTarget.Salvage(1).InteractionId(8));
        }

        [Test]
        public void TargetsAreEqualByWhatTheyPointAtNotByPromptText()
        {
            var a = InteractionTarget.Station(2, InteractionPrompt.Note("one"));
            var b = InteractionTarget.Station(2, InteractionPrompt.Note("two"));
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, InteractionTarget.Passenger(2), "The same index in another kind is another target.");
            Assert.AreNotEqual(a, InteractionTarget.Station(3));
        }

        [Test]
        public void HarborTargetsRoundTripTheirAction()
        {
            Assert.AreEqual(HarborAction.PaintShop, InteractionTarget.Harbor(HarborAction.PaintShop).HarborAction);
            Assert.IsTrue(InteractionTarget.Passenger(4).IsPassenger(4));
            Assert.IsFalse(InteractionTarget.Passenger(4).IsStation(4));
        }

        [Test]
        public void ContextTargetsAreNotTargets()
        {
            var context = InteractionTarget.Context(PromptCatalog.ForHelm());
            Assert.IsTrue(context.IsNone);
            Assert.IsFalse(context.Prompt.IsEmpty);
            Assert.AreEqual(InteractionTarget.None, context);
        }
    }
}
