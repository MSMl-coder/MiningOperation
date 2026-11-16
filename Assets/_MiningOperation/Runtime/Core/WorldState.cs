using System;
using System.Collections.Generic;
using System.Linq;

namespace MiningOperation
{
    // Serializable DTOs only: Unity JsonUtility does not support dictionaries.
    [Serializable] public sealed class WorldState
    {
        public int schemaVersion = 1;
        public double gameSeconds;
        public long lastEconomyCycle = -1; public long lastMatchingCycle = -1; public long lastPayrollDay = 0;
        public string playerCompanyId = "player";
        public string controlledShipId = "ship.starter";
        public string pendingArrivalScene;
        public List<WalletState> wallets = new List<WalletState>();
        public List<InventoryState> inventories = new List<InventoryState>();
        public List<StationState> stations = new List<StationState>();
        public List<ShipState> ships = new List<ShipState>();
        public List<AsteroidState> asteroids = new List<AsteroidState>();
        public List<MarketOrderState> orders = new List<MarketOrderState>();
        public List<ShipmentState> shipments = new List<ShipmentState>();
        public List<ProductionJobState> production = new List<ProductionJobState>();
        public List<PersonState> personnel = new List<PersonState>();
        public List<FleetState> fleets = new List<FleetState>();
        public List<MarketRegionState> markets = new List<MarketRegionState>();
        public List<EconomicAgentState> agents = new List<EconomicAgentState>();
        public List<ShipBuildState> shipBuilds = new List<ShipBuildState>();
        public List<EventRecord> events = new List<EventRecord>();
    }

    [Serializable] public sealed class EventRecord { public double at; public string message; }
    [Serializable] public sealed class WalletState { public string ownerId; public long balance; public long held; }
    [Serializable] public sealed class ItemStack { public string itemId; public double amount; }
    [Serializable] public sealed class ItemReservation { public string key; public string itemId; public double amount; }
    [Serializable] public sealed class InventoryState
    {
        public string id; public string ownerId; public double capacityKg;
        public List<ItemStack> stacks = new List<ItemStack>();
        public List<ItemReservation> reserved = new List<ItemReservation>();
    }
    [Serializable] public sealed class StationModuleState { public string definitionId; public int level = 1; }
    [Serializable] public sealed class StationTariffs
    {
        public long dockingFee = 30; public long fittingFee = 25; public long processingFee = 10;
        public double saleCommission = 0.04;
    }
    [Serializable] public sealed class StationState
    {
        public string id; public string displayName; public string ownerId; public string kind = "public";
        public string regionId; public string storageId; public string sceneId; public double x; public double y; public double z;
        public int moduleSlots = 4; public double powerLimit = 20;
        public bool allowsVisitors = true; public bool allowsTrading = true; public bool allowsIndustry = true;
        public string mobilityState = "Operational"; public double travelUntil;
        public double travelStartedAt; public double originX; public double originZ; public double destinationX; public double destinationZ;
        public StationTariffs tariffs = new StationTariffs();
        public List<StationModuleState> modules = new List<StationModuleState>();
    }
    [Serializable] public sealed class ShipSlotState { public string slotId; public string slotType; public string itemId; }
    [Serializable] public sealed class ShipState
    {
        public string id; public string displayName; public string ownerId; public string hullDefinitionId;
        public string cargoInventoryId; public string dockedAtStationId; public string dockingTargetId; public string sceneId;
        public double dockingCompleteAt; public double x; public double y; public double z;
        public int collectorCount = 3;
        public List<ShipSlotState> slots = new List<ShipSlotState>();
    }
    [Serializable] public sealed class AsteroidState
    {
        public string id; public string displayName; public string oreItemId; public string sceneId;
        public double remainingKg; public double x; public double y; public double z;
    }
    [Serializable] public sealed class MarketOrderState
    {
        public string id; public string side; public string ownerId; public string stationId;
        public string inventoryId; public string itemId; public double quantity; public double matched;
        public double fulfilled; public long unitPrice; public long heldCredits; public string status = "Open";
    }
    [Serializable] public sealed class ShipmentState
    {
        public string id; public string sellOrderId; public string buyOrderId;
        public string itemId; public double quantity; public string fromStationId; public string toStationId;
        public string status = "Scheduled"; public double pickupAt; public double deliveryAt;
        public long totalPrice; public long commission; public string carrierId;
    }
    [Serializable] public sealed class ProductionJobState
    {
        public string id; public string recipeId; public string stationId; public string ownerId;
        public string outputInventoryId; public string status = "Running";
        public double startedAt; public double completesAt; public int batches;
    }
    [Serializable] public sealed class PersonState
    {
        public string id; public string name; public string profession; public string employerId;
        public string assignedStationId; public string assignedFleetId; public int skill = 1;
        public long salaryPerDay;
    }
    [Serializable] public sealed class FleetState
    {
        public string id; public string name; public string ownerId; public string homeStationId;
        public List<string> shipIds = new List<string>();
        public string commanderPersonId; public string mission = "Idle";
        public string targetAsteroidId; public double targetKg; public double minedKg;
    }
    [Serializable] public sealed class MarketPriceState
    {
        public string itemId; public long basePrice; public long currentPrice;
        public double supply; public double demand;
    }
    [Serializable] public sealed class MarketRegionState
    {
        public string id; public string displayName;
        public List<MarketPriceState> prices = new List<MarketPriceState>();
    }
    [Serializable] public sealed class EconomicAgentState
    {
        public string id; public string kind; public string stationId;
        public string preferredItemId; public double targetStockKg;
    }
    [Serializable] public sealed class ShipBuildState
    {
        public string id; public string stationId; public string ownerId; public string hullId;
        public double completesAt; public string status = "Building";
    }

    public static class WorldUtil
    {
        public const double Epsilon = 0.000001;
        public static string Id() { return Guid.NewGuid().ToString("N"); }
        public static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        public static bool IsPositive(double value) { return !double.IsNaN(value) && !double.IsInfinity(value) && value > Epsilon; }
        public static double Distance(double ax,double ay,double az,double bx,double by,double bz)
        { var x=ax-bx; var y=ay-by; var z=az-bz; return Math.Sqrt(x*x+y*y+z*z); }
        public static long Cost(long unitPrice, double quantity)
        {
            Require(unitPrice >= 0 && IsPositive(quantity), "Invalid price or quantity");
            return checked((long)Math.Ceiling(unitPrice*quantity-0.00000001));
        }
    }
}
