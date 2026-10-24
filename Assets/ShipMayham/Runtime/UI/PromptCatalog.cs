namespace ShipMayham
{
    /// <summary>Pure mapping from game state to the structured prompt shown at the crosshair.</summary>
    public static class PromptCatalog
    {
        private static readonly PromptHint LiftHint = new PromptHint(HudControl.Carry, "Lift");

        public static InteractionPrompt ForCannon() => InteractionPrompt.Context("Cannon",
            new PromptHint(HudControl.Fire, "Fire"), new PromptHint(HudControl.Reload, "Reload"), new PromptHint(HudControl.Release, "Leave"));

        public static InteractionPrompt ForHelm() => InteractionPrompt.Context("Helm",
            new PromptHint(HudControl.Jump, "Depart / dock"), new PromptHint(HudControl.Camera, "Camera"), new PromptHint(HudControl.Release, "Leave"));

        public static InteractionPrompt ForCrane() => InteractionPrompt.Context("Harbor crane",
            new PromptHint(HudControl.Jump, "Lift / land"), new PromptHint(HudControl.Release, "Leave"));

        public static InteractionPrompt ForCarriedPassenger() => InteractionPrompt.Context("Carrying passenger",
            new PromptHint(HudControl.Carry, "Put down"), new PromptHint(HudControl.Throw, "Throw"));

        public static InteractionPrompt ForAirborne() => InteractionPrompt.Note("Land first");

        public static InteractionPrompt ForStation(StationKind kind, in VoyageState voyage, in EncounterState encounter, in CrewState crew)
        {
            switch (kind)
            {
                case StationKind.Manifest:
                    return voyage.Phase == VoyagePhase.Sailing
                        ? InteractionPrompt.Target("Purser", "Back at port", "Passengers and upgrades are handled at port", key: HudControl.None, tone: PromptTone.Info)
                        : InteractionPrompt.Target("Purser", "Talk to the purser", voyage.Phase == VoyagePhase.Boarding ? "Board passengers, upgrade the ship" : "Settle up, prepare the next voyage");
                case StationKind.Helm:
                    return InteractionPrompt.Target("Helm", "Take the helm", "Steer, depart and dock");
                case StationKind.Food:
                    return InteractionPrompt.Target("Galley: food", "Take food", $"{voyage.FoodStock} in stock");
                case StationKind.Drink:
                    return InteractionPrompt.Target("Galley: drinks", "Take drink", $"{voyage.DrinkStock} in stock");
                case StationKind.Repair:
                    return ForRepairBench(voyage, crew);
                case StationKind.RepairSupply:
                    return crew.Carrying == ServiceItem.RepairKit
                        ? InteractionPrompt.Target("Patch kit locker", "Return patch kit")
                        : InteractionPrompt.Target("Patch kit locker", "Take patch kit", $"{voyage.RepairKitStock} in store");
                case StationKind.Ammo:
                    return crew.Carrying == ServiceItem.Ammo
                        ? InteractionPrompt.Target("Magazine", "Return cannonball")
                        : InteractionPrompt.Target("Magazine", "Take cannonball", $"{encounter.MagazineAmmo} in magazine");
                case StationKind.Cannon:
                    if (crew.Carrying != ServiceItem.Ammo) return InteractionPrompt.Target("Cannon", "Man the cannon", "Pirates in range? Fire away");
                    int used = encounter.ReadyAmmo + (encounter.ReloadRemaining > 0 ? 1 : 0);
                    return InteractionPrompt.Target("Cannon", "Load cannonball", $"Rack {used}/{VoyageSimulation.CannonBufferCapacity}");
                case StationKind.Power:
                    return encounter.PowerOut
                        ? InteractionPrompt.Target("Power panel", "Reset breaker", $"{encounter.PowerRepairSteps}/3 breakers", tone: PromptTone.Normal)
                        : InteractionPrompt.Target("Power panel", "Power online", "Nothing to repair", key: HudControl.None, tone: PromptTone.Info);
                case StationKind.EngineUpgrade:
                    return Upgrade("Engine upgrade", kind, voyage, voyage.EngineLevel, 2);
                case StationKind.CapacityUpgrade:
                    return Upgrade("Seat upgrade", kind, voyage, voyage.CapacityLevel, 2);
                case StationKind.ComfortUpgrade:
                    return Upgrade("Comfort upgrade", kind, voyage, voyage.ComfortLevel, 1);
                default:
                    return InteractionPrompt.Target(kind.ToString(), "Use");
            }
        }

        private static InteractionPrompt ForRepairBench(in VoyageState voyage, in CrewState crew)
        {
            if (crew.Carrying == ServiceItem.RepairKit)
                return InteractionPrompt.Target("Repair bench", "Deliver patch kit", $"Bench {voyage.ReadyRepairKits}/{VoyageSimulation.RepairBufferLimit(voyage)} kits ready");
            if (voyage.Leak == 0)
                return InteractionPrompt.Target("Repair bench", "Hull watertight", "Bring patch kits here to prepare quick fixes", key: HudControl.None, tone: PromptTone.Info);
            if (voyage.ReadyRepairKits > 0)
                return InteractionPrompt.Target("Repair bench", "Use quick patch", "Seals the leak and restores hull");
            return InteractionPrompt.Target("Repair bench", "Patch the leak", $"Step {voyage.RepairSteps}/4. A kit makes it instant");
        }

        private static InteractionPrompt Upgrade(string title, StationKind kind, in VoyageState voyage, int level, int max)
        {
            string detail = $"Level {level}/{max}";
            if (level >= max) return InteractionPrompt.Target(title, "Fully upgraded", detail, key: HudControl.None, tone: PromptTone.Info);
            bool inPort = voyage.Phase == VoyagePhase.Boarding || voyage.Phase == VoyagePhase.Arrived;
            return InteractionPrompt.Target(title, inPort ? "Buy upgrade" : "Available in port", detail,
                $"{VoyageSimulation.UpgradeCost(kind, voyage)} coins", inPort ? HudControl.Interact : HudControl.None,
                inPort ? PromptTone.Normal : PromptTone.Blocked);
        }

        /// <summary>The gangway gate has no button: walking into it opens it, unless the crew member's hands are full.</summary>
        public static InteractionPrompt ForGate(bool toShore, in CrewState crew)
        {
            string action = toShore ? "Walk through to go ashore" : "Walk through to board the ship";
            if (crew.Carrying != ServiceItem.None)
                return InteractionPrompt.Target("Gangway gate", action, "Hands must be empty: put your item down first", key: HudControl.None, tone: PromptTone.Blocked);
            return InteractionPrompt.Target("Gangway gate", action, toShore ? "Supplies and repairs are in town" : "Return to the deck", key: HudControl.None, tone: PromptTone.Info);
        }

        public static InteractionPrompt ForLadder(in Ladder ladder, in CrewState crew, bool harborOpen)
        {
            if (crew.Swimming) return InteractionPrompt.Target(ladder.Name, "Climb up", "Pull yourself out of the water");
            if (!harborOpen) return InteractionPrompt.Target(ladder.Name, "Climb down", "The ship is under way: nobody swims now", key: HudControl.None, tone: PromptTone.Blocked);
            if (crew.Carrying != ServiceItem.None) return InteractionPrompt.Target(ladder.Name, "Climb down", "Put your item down first", key: HudControl.None, tone: PromptTone.Blocked);
            return InteractionPrompt.Target(ladder.Name, "Dive in and swim", "Swim with the movement keys, climb out at any ladder");
        }

        /// <summary>What a swimmer sees when no ladder is near.</summary>
        public static InteractionPrompt ForSwimming(bool atSea) =>
            InteractionPrompt.Context(atSea ? "Overboard!" : "Swimming", new PromptHint(HudControl.Interact, "Climb a ladder"),
                new PromptHint(HudControl.None, "Swim to the rope ladders"));

        public static InteractionPrompt ForHarbor(HarborAction action, in VoyageState voyage, in CrewState crew)
        {
            if (voyage.DockJobActive)
            {
                var job = ForDockJob(action, voyage, crew);
                if (!job.IsEmpty) return job;
            }
            switch (action)
            {
                case HarborAction.GoAshore:
                case HarborAction.BoardShip: return ForGate(action == HarborAction.GoAshore, crew);
                case HarborAction.BuyFood: return InteractionPrompt.Target("Food stall", "Buy food", "+8 food. Storage holds 32", "12 coins");
                case HarborAction.BuyDrink: return InteractionPrompt.Target("Drink stall", "Buy drinks", "+8 drinks. Storage holds 32", "12 coins");
                case HarborAction.HarborMaster: return InteractionPrompt.Target("Port office", "Talk to harbor master", "Choose a route or a town job");
                case HarborAction.PaintShop: return InteractionPrompt.Target("Paint dock", "Choose ship colors", "Livery unlocks with reputation");
                case HarborAction.FishMarket: return InteractionPrompt.Target("Fish market", "Take fish crate", "Needed for crane loading");
                case HarborAction.CraneLoadingPad: return InteractionPrompt.Target("Load pad", "Deliver fish crate");
                case HarborAction.CraneControls: return InteractionPrompt.Target("Harbor crane", "Operate crane", "Part of the crane loading job");
                default: return InteractionPrompt.Target("Hull dock", "Buy hull shield", "Absorbs 20 damage", "30 coins");
            }
        }

        private static InteractionPrompt ForDockJob(HarborAction action, in VoyageState voyage, in CrewState crew)
        {
            switch (voyage.DockJobType)
            {
                case DockJobKind.CraneLoading:
                    if (crew.Carrying != ServiceItem.CraneCrate) return default;
                    return action == HarborAction.CraneLoadingPad
                        ? InteractionPrompt.Target("Load pad", "Deliver crate", "Then operate the crane")
                        : action == HarborAction.FishMarket || action == HarborAction.BuyFood
                            ? InteractionPrompt.Target("Return point", "Return crate") : default;
                case DockJobKind.SalvageRun:
                    if (!VoyageSimulation.IsSalvage(crew.Carrying)) return default;
                    return action == HarborAction.HarborMaster
                        ? InteractionPrompt.Target("Port office", "Deliver salvage", "Counts toward the town job")
                        : action == HarborAction.BuyFood ? InteractionPrompt.Target("Return point", "Return salvage") : default;
                case DockJobKind.LuggageSort:
                    if (VoyageSimulation.IsLuggage(crew.Carrying))
                    {
                        string detail = $"Bag {VoyageSimulation.LuggageTag(crew.Carrying)} goes to the {VoyageSimulation.LuggageDesk(voyage, crew.Carrying)} desk";
                        return action == HarborAction.BuyFood
                            ? InteractionPrompt.Target("Baggage pile", "Return bag", detail)
                            : InteractionPrompt.Target("Baggage desk", "Deliver bag", detail);
                    }
                    return action == HarborAction.BuyFood
                        ? InteractionPrompt.Target("Baggage pile", "Take next bag", "Tagged A, B or C") : default;
                default:
                    if (action == HarborAction.BuyFood)
                        return crew.Carrying == ServiceItem.Cargo
                            ? InteractionPrompt.Target("Cargo pile", "Return cargo crate")
                            : InteractionPrompt.Target("Cargo pile", "Pick up cargo crate", "Deliver it to the hull dock");
                    if (action == HarborAction.MaintainHull && crew.Carrying == ServiceItem.Cargo)
                        return InteractionPrompt.Target("Hull dock", "Deliver cargo crate");
                    return default;
            }
        }

        public static InteractionPrompt ForSalvage(int index) =>
            InteractionPrompt.Target("Salvage bundle", "Take salvage", $"Bundle {index + 1}/{HarborTownLayout.SalvageCount}. Bring it to the port office");

        public static InteractionPrompt ForPassenger(int index, in PassengerState p)
        {
            string title = $"Passenger {index + 1}";
            string ticket = PassengerPresentation.TicketText(p) + $" | {p.Personality}" +
                (p.TicketChecked && !p.FarePaid ? " | R / D-pad down: cleaning job instead of fare" : " | R / D-pad down: reassure");

            if (p.Motion != PassengerMotion.OnDeck)
                return InteractionPrompt.Target(title, "Lift passenger", PassengerPresentation.ReactionText(p), null, HudControl.Carry);
            if (p.Request != ServiceItem.None)
            {
                var check = !p.TicketChecked ? new PromptHint(HudControl.Check, "Check ticket") : !p.FarePaid ? new PromptHint(HudControl.Check, "Collect fare") : default;
                return InteractionPrompt.Target(title, "Serve " + p.Request.ToString().ToLowerInvariant(), ticket, null, HudControl.Interact, PromptTone.Normal, check, LiftHint);
            }
            if (!p.TicketChecked) return InteractionPrompt.Target(title, "Check ticket", "No order right now", null, HudControl.Check, PromptTone.Normal, LiftHint);
            if (!p.FarePaid) return InteractionPrompt.Target(title, "Collect fare", ticket, "+40 coins", HudControl.Check, PromptTone.Normal, LiftHint);
            return InteractionPrompt.Target(title, "Lift passenger", "Ticket valid. No order", null, HudControl.Carry);
        }
    }
}
