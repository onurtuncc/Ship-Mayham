using NUnit.Framework;

namespace ShipMayham.Tests
{
    /// <summary>
    /// Most tests are about something other than the gangway walk, so passengers appear seated unless a test asks for the
    /// walk (PassengerFlowTests switch it on and restore it).
    /// </summary>
    [SetUpFixture]
    public sealed class TestDefaults
    {
        [OneTimeSetUp]
        public void SetUpAll() => VoyageSimulation.WalkingPassengers = false;

        [OneTimeTearDown]
        public void TearDownAll() => VoyageSimulation.WalkingPassengers = true;
    }
}
