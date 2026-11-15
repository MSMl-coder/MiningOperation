using System;
using System.Linq;

namespace MiningOperation
{
    public sealed class MiningService
    {
        private readonly WorldState world;private readonly ShipService ships;private readonly InventoryService inventory;
        public MiningService(WorldState w,ShipService s,InventoryService inv){world=w;ships=s;inventory=inv;}
        public AsteroidState Get(string id)
        {var a=world.asteroids.FirstOrDefault(x=>x.id==id);WorldUtil.Require(a!=null,"Unknown asteroid");return a;}
        public double Extract(string shipId,string asteroidId,double requestedKg)
        {
            var s=ships.Get(shipId);var a=Get(asteroidId);
            WorldUtil.Require(string.IsNullOrEmpty(s.dockedAtStationId)&&string.IsNullOrEmpty(s.dockingTargetId),"Cannot mine when docked/docking");
            WorldUtil.Require(s.slots.Any(sl=>sl.itemId=="module.mining_laser"),"Requires a mining laser");
            WorldUtil.Require(s.collectorCount>=3,"Requires three gravity collectors");
            WorldUtil.Require(WorldUtil.IsPositive(requestedKg),"Invalid extraction amount");
            WorldUtil.Require(WorldUtil.Distance(s.x,s.y,s.z,a.x,a.y,a.z)<130,"Asteroid out of range");
            var massFree=inventory.Get(s.cargoInventoryId).capacityKg-inventory.Mass(s.cargoInventoryId);
            var extracted=Math.Max(0,Math.Min(Math.Min(requestedKg,a.remainingKg),massFree));
            WorldUtil.Require(extracted>WorldUtil.Epsilon,"No ore or cargo space");
            a.remainingKg-=extracted;inventory.Add(s.cargoInventoryId,a.oreItemId,extracted);
            return extracted;
        }
        public void Unload(string shipId,string stationId,string itemId,double amount)
        {
            var s=ships.Get(shipId);
            WorldUtil.Require(s.dockedAtStationId==stationId,"Dock at requested station first");
            inventory.Transfer(s.cargoInventoryId,inventory.Warehouse(s.ownerId,stationId).id,itemId,amount);
        }
    }
}
