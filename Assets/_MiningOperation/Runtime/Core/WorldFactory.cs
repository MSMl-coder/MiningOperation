using System;
using System.Linq;

namespace MiningOperation
{
    public static class WorldFactory
    {
        public static WorldState Create(ContentCatalog c)
        {
            var w=new WorldState();
            AddWallet(w,"player",55000);AddWallet(w,"npc.public",120000);AddWallet(w,"npc.trade_station",200000);
            AddWallet(w,"npc.refinery",300000);AddWallet(w,"npc.merchant",400000);
            AddWallet(w,"npc.miner",70000);AddWallet(w,"npc.logistics",80000);
            w.stations.Add(new StationState {id="station.public",displayName="Outpost 19 / Public",ownerId="npc.public",
                kind="public",regionId="region.alpha",x=0,y=0,z=0,moduleSlots=20,powerLimit=120,
                modules={new StationModuleState{definitionId="refinery"},new StationModuleState{definitionId="fabricator"}},
                tariffs=new StationTariffs{dockingFee=30,fittingFee=25,processingFee=15,saleCommission=0.04}});
            w.stations.Add(new StationState{id="station.trade",displayName="Argent Trade Depot",ownerId="npc.trade_station",
                kind="public",regionId="region.alpha",x=450,y=0,z=35,moduleSlots=12,powerLimit=80});
            w.stations.Add(new StationState{id="station.refinery",displayName="Helios Industrial Works",ownerId="npc.refinery",
                kind="public",regionId="region.alpha",x=-340,y=0,z=90,moduleSlots=12,powerLimit=80,
                modules={new StationModuleState{definitionId="refinery"}}});
            var inv=new InventoryService(w,c);var ships=new ShipService(w);
            foreach(var s in w.stations)s.storageId=inv.Warehouse(s.ownerId,s.id).id;
            var playerInv=inv.Warehouse("player","station.public");
            inv.Add(playerInv.id,"ore.iron_nickel",25);
            inv.Add(playerInv.id,"concentrate.iron",12);
            inv.Add(playerInv.id,"material.steel",24);
            inv.Add(playerInv.id,"module.scanner",1);
            inv.Add(playerInv.id,"part.fasteners",5);
            var ship=ships.Create("player","hull.utility","Pioneer / MK-I","station.public",0,0,0);
            w.controlledShipId=ship.id;
            ship.slots.First(s=>s.slotId=="industrial.1").itemId="module.mining_laser";
            ship.slots.First(s=>s.slotId=="utility.1").itemId="module.scanner";
            var merchant=inv.Warehouse("npc.merchant","station.trade");
            inv.Add(merchant.id,"module.thruster",4);inv.Add(merchant.id,"module.cargo",4);
            inv.Add(merchant.id,"stock.steel_sheet",10);
            var miner=inv.Warehouse("npc.miner","station.trade");
            inv.Add(miner.id,"ore.iron_nickel",450);
            inv.Add(miner.id,"ore.copper",75);
            var refiner=inv.Warehouse("npc.refinery","station.refinery");
            inv.Add(refiner.id,"ore.iron_nickel",15);
            w.asteroids.Add(new AsteroidState {id="asteroid.iron",displayName="Fe-Ni Cluster A-01",oreItemId="ore.iron_nickel",
                remainingKg=850,x=90,y=0,z=0});
            w.asteroids.Add(new AsteroidState {id="asteroid.copper",displayName="Cu-rich Fragment A-02",oreItemId="ore.copper",
                remainingKg=440,x=-95,y=0,z=10});
            w.asteroids.Add(new AsteroidState {id="asteroid.ice",displayName="Water Ice C-07",oreItemId="ice.water",
                remainingKg=700,x=35,y=0,z=110});
            var region=new MarketRegionState {id="region.alpha",displayName="Alpha Industrial Cluster"};
            foreach(var item in c.Items)region.prices.Add(new MarketPriceState {itemId=item.id,basePrice=item.basePrice,currentPrice=item.basePrice});
            w.markets.Add(region);
            w.agents.Add(new EconomicAgentState{id="npc.merchant",kind="Supplier",stationId="station.trade",
                preferredItemId="module.thruster",targetStockKg=2});
            w.agents.Add(new EconomicAgentState{id="npc.miner",kind="Supplier",stationId="station.trade",
                preferredItemId="ore.iron_nickel",targetStockKg=25});
            w.agents.Add(new EconomicAgentState{id="npc.refinery",kind="Buyer",stationId="station.refinery",
                preferredItemId="ore.iron_nickel",targetStockKg=230});
            w.events.Add(new EventRecord {at=0,message="New Mining Operation session. Outpost 19 is public property."});
            return w;
        }
        private static void AddWallet(WorldState w,string id,long balance)
        {w.wallets.Add(new WalletState {ownerId=id,balance=balance});}
    }
}
