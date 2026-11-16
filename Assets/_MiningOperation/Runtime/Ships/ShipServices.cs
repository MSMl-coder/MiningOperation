using System;
using System.Collections.Generic;
using System.Linq;

namespace MiningOperation
{
    public sealed class ShipService
    {
        private readonly WorldState world;
        public ShipService(WorldState w){world=w;}
        public ShipState Get(string id)
        {var x=world.ships.FirstOrDefault(s=>s.id==id);WorldUtil.Require(x!=null,"Ship missing: "+id);return x;}
        public ShipState Create(string owner,string hullId,string displayName,string dockId,double x,double y,double z)
        {
            var id="ship."+WorldUtil.Id();
            var cargo=new InventoryState {id="cargo."+id,ownerId=owner,capacityKg=240};
            world.inventories.Add(cargo);
            var ship=new ShipState {id=id,displayName=displayName,ownerId=owner,hullDefinitionId=hullId,
                cargoInventoryId=cargo.id,dockedAtStationId=dockId,x=x,y=y,z=z,sceneId=world.stations.FirstOrDefault(t=>t.id==dockId)?.sceneId};
            ship.slots.Add(new ShipSlotState {slotId="industrial.1",slotType="industrial"});
            ship.slots.Add(new ShipSlotState {slotId="industrial.2",slotType="industrial"});
            ship.slots.Add(new ShipSlotState {slotId="utility.1",slotType="utility"});
            ship.slots.Add(new ShipSlotState {slotId="propulsion.1",slotType="propulsion"});
            ship.slots.Add(new ShipSlotState {slotId="cargo.1",slotType="cargo"});
            world.ships.Add(ship);return ship;
        }
    }
    public sealed class DockingService
    {
        private readonly WorldState world;private readonly ShipService ships;private readonly StationService stations;
        private readonly WalletService wallets;
        public DockingService(WorldState w,ShipService s,StationService st,WalletService wal)
        {world=w;ships=s;stations=st;wallets=wal;}
        public void RequestDock(string shipId,string stationId)
        {
            var ship=ships.Get(shipId);var st=stations.Get(stationId);
            WorldUtil.Require(string.IsNullOrEmpty(ship.dockedAtStationId)&&string.IsNullOrEmpty(ship.dockingTargetId),"Ship already docking or docked");
            WorldUtil.Require(stations.CanUse(st,ship.ownerId),"Station denies access");
            WorldUtil.Require(!string.IsNullOrEmpty(ship.sceneId) && ship.sceneId==st.sceneId,"Station is in a different scene. Travel there first.");
            WorldUtil.Require(wallets.Available(ship.ownerId)>= (st.ownerId==ship.ownerId?0:st.tariffs.dockingFee),"Docking fee cannot be paid");
            var distance=WorldUtil.Distance(ship.x,ship.y,ship.z,st.x,st.y,st.z);
            ship.dockingTargetId=stationId;
            ship.dockingCompleteAt=world.gameSeconds+Math.Max(2,distance/25);
        }
        public void Undock(string shipId)
        {
            var ship=ships.Get(shipId);
            WorldUtil.Require(!string.IsNullOrEmpty(ship.dockedAtStationId),"Ship is not docked");
            var st=stations.Get(ship.dockedAtStationId);
            ship.dockedAtStationId=null;ship.dockingTargetId=null;
            ship.x=st.x+35;ship.y=st.y;ship.z=st.z+20;ship.sceneId=st.sceneId;
        }
        public void Tick()
        {
            foreach(var s in world.ships)
            {
                if(string.IsNullOrEmpty(s.dockingTargetId)||s.dockingCompleteAt>world.gameSeconds)continue;
                var st=stations.Get(s.dockingTargetId);
                if(!stations.CanUse(st,s.ownerId)){s.dockingTargetId=null;continue;}
                var fee=st.ownerId==s.ownerId?0:st.tariffs.dockingFee;
                if(wallets.Available(s.ownerId)<fee){s.dockingTargetId=null;continue;}
                if(fee>0)wallets.Pay(s.ownerId,st.ownerId,fee);
                s.dockedAtStationId=st.id;s.dockingTargetId=null;
                s.x=st.x;s.y=st.y;s.z=st.z;s.sceneId=st.sceneId;
            }
        }
    }
    public sealed class ShipFittingService
    {
        private readonly WorldState world;private readonly ShipService ships;private readonly InventoryService inventory;
        private readonly ContentCatalog catalog;private readonly StationService stations;private readonly WalletService wallets;
        private const double MaxPower=34, MaxModuleMass=120;
        public ShipFittingService(WorldState w,ShipService s,InventoryService i,ContentCatalog c,StationService st,WalletService wal)
        {world=w;ships=s;inventory=i;catalog=c;stations=st;wallets=wal;}
        public void Fit(string shipId,string slotId,string itemId)
        {
            var ship=ships.Get(shipId);
            WorldUtil.Require(!string.IsNullOrEmpty(ship.dockedAtStationId),"Fit only while docked");
            var st=stations.Get(ship.dockedAtStationId);
            WorldUtil.Require(stations.CanUse(st,ship.ownerId),"No fitting access");
            var slot=ship.slots.FirstOrDefault(x=>x.slotId==slotId);
            WorldUtil.Require(slot!=null,"Slot missing");
            var item=catalog.Item(itemId);WorldUtil.Require(item.fitsSlot==slot.slotType,"Incompatible ship slot");
            var wh=inventory.Warehouse(ship.ownerId,st.id).id;
            WorldUtil.Require(inventory.Available(wh,itemId)>=1,"Component not in warehouse");
            // Predict with candidate replacing old fitting.
            double newPower=item.powerMW,newMass=item.unitMassKg;
            foreach(var other in ship.slots.Where(x=>x!=slot && !string.IsNullOrEmpty(x.itemId)))
            {var def=catalog.Item(other.itemId);newPower+=def.powerMW;newMass+=def.unitMassKg;}
            WorldUtil.Require(newPower<=MaxPower && newMass<=MaxModuleMass,"Power/mass limit exceeded");
            var fee=st.ownerId==ship.ownerId?0:st.tariffs.fittingFee;
            WorldUtil.Require(wallets.Available(ship.ownerId)>=fee,"Insufficient fitting funds");
            if(!string.IsNullOrEmpty(slot.itemId))
                WorldUtil.Require(inventory.CanAdd(wh,slot.itemId,1),"No storage room for old part");
            if(fee>0)wallets.Pay(ship.ownerId,st.ownerId,fee);
            inventory.Remove(wh,itemId,1);
            if(!string.IsNullOrEmpty(slot.itemId))inventory.Add(wh,slot.itemId,1);
            slot.itemId=itemId;
        }
        public void Unfit(string shipId,string slotId)
        {
            var ship=ships.Get(shipId);WorldUtil.Require(!string.IsNullOrEmpty(ship.dockedAtStationId),"Dock required");
            var slot=ship.slots.First(x=>x.slotId==slotId);
            WorldUtil.Require(!string.IsNullOrEmpty(slot.itemId),"Empty slot");
            var station=stations.Get(ship.dockedAtStationId);
            WorldUtil.Require(stations.CanUse(station,ship.ownerId),"Fitting access denied");
            var wh=inventory.Warehouse(ship.ownerId,station.id).id;
            WorldUtil.Require(inventory.CanAdd(wh,slot.itemId,1),"Station warehouse is full");
            var fee=station.ownerId==ship.ownerId?0:station.tariffs.fittingFee;
            WorldUtil.Require(wallets.Available(ship.ownerId)>=fee,"Insufficient fitting funds");
            if(fee>0)wallets.Pay(ship.ownerId,station.ownerId,fee);
            inventory.Add(wh,slot.itemId,1);slot.itemId=null;
        }
    }
}
