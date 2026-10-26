using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Shows what is left to find in the current port: fresh mounds turn into holes once dug, chests disappear once opened and the
    /// sunken coins spin until somebody swims into them. Presentation only; the masks are replicated by the host.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public sealed class PrototypeActivityView : MonoBehaviour
    {
        public PrototypeSession Session;
        public Transform[] Mounds = System.Array.Empty<Transform>(), Holes = System.Array.Empty<Transform>(),
            Chests = System.Array.Empty<Transform>(), Coins = System.Array.Empty<Transform>();

        public float CoinSpinDegreesPerSecond = 90f;

        private void Awake() => LayOutCoins();

        /// <summary>Sunken coins rest at their own depth below the surface; spots beyond the authored coins get copies of the first.</summary>
        public void LayOutCoins()
        {
            int spots = VoyageSimulation.CoinSpots.Length;
            if (Coins.Length > 0 && Coins[0] != null && Coins.Length < spots)
            {
                var all = new Transform[spots];
                System.Array.Copy(Coins, all, Coins.Length);
                for (int i = Coins.Length; i < spots; i++)
                {
                    all[i] = Instantiate(Coins[0], Coins[0].parent);
                    all[i].name = "SunkenCoin_" + i;
                }
                Coins = all;
            }
            for (int i = 0; i < Coins.Length && i < spots; i++)
            {
                if (Coins[i] == null) continue;
                var spot = VoyageSimulation.CoinSpots[i];
                Coins[i].localPosition = new Vector3(spot.x, VoyageSimulation.SeaLevel - VoyageSimulation.CoinDepth(i), spot.y);
            }
        }

        private void LateUpdate()
        {
            if (Session == null || !Session.IsSpawned) return;
            var voyage = Session.Voyage.Value;
            bool harborOpen = Session.Harbor != null && Session.Harbor.IsOpen;
            Apply(Mounds, Holes, Chests, Coins, voyage.DigMask, voyage.ChestMask, voyage.CoinMask, harborOpen);
            float spin = Time.deltaTime * CoinSpinDegreesPerSecond;
            for (int i = 0; i < Coins.Length; i++)
                if (Coins[i] != null && Coins[i].gameObject.activeSelf) Coins[i].Rotate(0, spin, 0, Space.Self);
        }

        /// <summary>The visibility rules, public so tests can drive them without a session.</summary>
        public static void Apply(Transform[] mounds, Transform[] holes, Transform[] chests, Transform[] coins, int digMask, int chestMask, int coinMask, bool harborOpen)
        {
            int dig = VoyageSimulation.DigSpots.Length, chest = VoyageSimulation.ChestSpots.Length;
            for (int i = 0; i < mounds.Length; i++)
                if (mounds[i] != null) mounds[i].gameObject.SetActive((digMask & (1 << (i % dig))) == 0);
            for (int i = 0; i < holes.Length; i++)
                if (holes[i] != null) holes[i].gameObject.SetActive((digMask & (1 << (i % dig))) != 0);
            for (int i = 0; i < chests.Length; i++)
                if (chests[i] != null) chests[i].gameObject.SetActive((chestMask & (1 << (i % chest))) == 0);
            for (int i = 0; i < coins.Length; i++)
                if (coins[i] != null) coins[i].gameObject.SetActive(harborOpen && (coinMask & (1 << i)) == 0);
        }
    }
}
