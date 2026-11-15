using System;
using System.Linq;

namespace MiningOperation
{
    // Composition root for an independent game world. Does not alter the existing Foundation.
    public sealed class WorldRuntime
    {
        public WorldState State {get;private set;}
        public ContentCatalog Catalog {get;private set;}
        public WalletService Wallets {get;private set;}
        public InventoryService Inventory {get;private set;}
        public StationService Stations {get;private set;}
        public ShipService Ships {get;private set;}
        public DockingService Docking {get;private set;}
        public ShipFittingService Fitting {get;private set;}
        public MiningService Mining {get;private set;}
        public IndustryService Industry {get;private set;}
        public TradeOrderService Trading {get;private set;}
        public TradeMatchingService Matching {get;private set;}
        public LogisticsService Logistics {get;private set;}
        public WorldEconomyService Economy {get;private set;}
        public PersonnelService Personnel {get;private set;}
        public FleetService Fleets {get;private set;}
        public ShipyardService Shipyard {get;private set;}
        public WorldRuntime(WorldState state,ContentCatalog catalog=null)
        {
            State=state??throw new ArgumentNullException(nameof(state));Catalog=catalog??ContentCatalog.Create();
            Wallets=new WalletService(State);Inventory=new InventoryService(State,Catalog);
            Stations=new StationService(State,Wallets,Catalog,Inventory);
            Ships=new ShipService(State);Docking=new DockingService(State,Ships,Stations,Wallets);
            Fitting=new ShipFittingService(State,Ships,Inventory,Catalog,Stations,Wallets);
            Mining=new MiningService(State,Ships,Inventory);
            Industry=new IndustryService(State,Catalog,Inventory,Wallets,Stations);
            Trading=new TradeOrderService(State,Inventory,Wallets,Stations,Catalog);
            Matching=new TradeMatchingService(State,Trading,Stations,Catalog);
            Logistics=new LogisticsService(State,Inventory,Wallets,Stations,Trading);
            Economy=new WorldEconomyService(State,Catalog,Inventory,Trading,Wallets,Stations);
            Personnel=new PersonnelService(State,Wallets,Stations);
            Fleets=new FleetService(State,Ships,Mining,Inventory,Personnel,Stations);
            Shipyard=new ShipyardService(State,Stations,Inventory,Ships);
        }
        public static WorldRuntime NewGame()
        {var c=ContentCatalog.Create();return new WorldRuntime(WorldFactory.Create(c),c);}
        public void Log(string message)
        {
            State.events.Add(new EventRecord{at=State.gameSeconds,message=message});
            while(State.events.Count>80)State.events.RemoveAt(0);
        }
        public void Advance(double seconds)
        {
            WorldUtil.Require(seconds>=0 && seconds<=7200,"Advance out of range");
            var count=(int)Math.Ceiling(seconds);
            for(int i=0;i<count;i++)Tick(Math.Min(1,seconds-i));
        }
        private void Tick(double seconds)
        {
            if(seconds<=0)return;
            State.gameSeconds+=seconds;
            Stations.Tick();Docking.Tick();Industry.Tick();Fleets.Tick();Shipyard.Tick();
            Logistics.Tick();Logistics.RetryBlocked();
            long economyCycle=(long)Math.Floor(State.gameSeconds/30);
            if(economyCycle>State.lastEconomyCycle)
            {
                State.lastEconomyCycle=economyCycle;
                Economy.Tick();
            }
            long matchCycle=(long)Math.Floor(State.gameSeconds/5);
            if(matchCycle>State.lastMatchingCycle)
            {
                State.lastMatchingCycle=matchCycle;
                Matching.Match(12);
            }
            long day=(long)Math.Floor(State.gameSeconds/1800);
            while(day>State.lastPayrollDay){State.lastPayrollDay++;Personnel.PayrollDay();}
        }
        public void Validate()
        {
            WorldUtil.Require(State.schemaVersion==1,"Unsupported world version");
            Wallets.Validate();Inventory.Validate();
            foreach(var o in State.orders)
            {
                WorldUtil.Require(o.quantity>0&&o.matched<=o.quantity+WorldUtil.Epsilon&&o.fulfilled<=o.matched+WorldUtil.Epsilon,
                    "Invalid order ledger: "+o.id);
                if(o.side=="Buy")WorldUtil.Require(o.heldCredits>=0,"Negative order hold");
                if(o.side=="Sell"&&o.status!="Cancelled")
                    WorldUtil.Require(Inventory.ReservationAmount(o.inventoryId,o.itemId,o.id)+WorldUtil.Epsilon>=o.quantity-o.fulfilled,
                        "Sell order reservation inconsistent");
            }
            foreach(var s in State.shipments)
            {
                WorldUtil.Require(State.orders.Any(o=>o.id==s.buyOrderId)&&State.orders.Any(o=>o.id==s.sellOrderId),"Orphan shipment");
            }
        }
    }
}
