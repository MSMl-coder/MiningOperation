using System;
using System.Linq;

namespace MiningOperation
{
    public sealed class FleetService
    {
        private readonly WorldState world;private readonly ShipService ships;private readonly MiningService mining;
        private readonly InventoryService inventory;private readonly PersonnelService personnel;private readonly StationService stations;
        public FleetService(WorldState w,ShipService s,MiningService m,InventoryService i,PersonnelService p,StationService st)
        {world=w;ships=s;mining=m;inventory=i;personnel=p;stations=st;}
        public FleetState Create(string name,string homeStationId)
        {
            var station=stations.Get(homeStationId);
            WorldUtil.Require(stations.CanUse(station,world.playerCompanyId),"Station unavailable");
            int capacity=1;
            if(station.ownerId==world.playerCompanyId)
            {
                var control=station.modules.FirstOrDefault(x=>x.definitionId=="fleet_control");
                if(control!=null)capacity+=control.level;
            }
            WorldUtil.Require(world.fleets.Count(x=>x.ownerId==world.playerCompanyId)<capacity,"Fleet command capacity reached");
            var fleet=new FleetState {id="fleet."+WorldUtil.Id(),name=name,ownerId=world.playerCompanyId,homeStationId=homeStationId};
            world.fleets.Add(fleet);return fleet;
        }
        public FleetState Get(string id)
        {var x=world.fleets.FirstOrDefault(f=>f.id==id);WorldUtil.Require(x!=null,"Fleet missing");return x;}
        public void AssignShip(string fleetId,string shipId)
        {
            var fleet=Get(fleetId);var ship=ships.Get(shipId);
            WorldUtil.Require(ship.ownerId==fleet.ownerId,"Ship not owned");
            WorldUtil.Require(!world.fleets.Any(f=>f.shipIds.Contains(shipId)),"Ship already in a fleet");
            fleet.shipIds.Add(shipId);
        }
        public void AssignCommander(string fleetId,string personId)
        {
            var fleet=Get(fleetId);var p=personnel.Get(personId);
            WorldUtil.Require(p.employerId==fleet.ownerId && p.profession=="Fleet Commander","Qualified commander required");
            fleet.commanderPersonId=personId;p.assignedFleetId=fleetId;p.assignedStationId=null;
        }
        public void StartMission(string fleetId,string asteroidId,double targetKg)
        {
            var fleet=Get(fleetId);var asteroid=mining.Get(asteroidId);
            WorldUtil.Require(fleet.mission=="Idle"||fleet.mission=="Completed","Fleet busy");
            WorldUtil.Require(WorldUtil.IsPositive(targetKg)&&fleet.shipIds.Count>0,"No ships or invalid target");
            WorldUtil.Require(!string.IsNullOrEmpty(fleet.commanderPersonId),"Autonomous mission requires commander");
            foreach(var id in fleet.shipIds)
            {
                WorldUtil.Require(id!=world.controlledShipId,"Controlled ship cannot run autonomous mission");
                var ship=ships.Get(id);
                WorldUtil.Require(ship.slots.Any(x=>x.itemId=="module.mining_laser"),"A mining laser is needed");
                ship.dockedAtStationId=null;ship.dockingTargetId=null;
                ship.x=asteroid.x+3;ship.y=asteroid.y;ship.z=asteroid.z+5;
            }
            fleet.mission="Mining";fleet.targetAsteroidId=asteroidId;fleet.targetKg=targetKg;fleet.minedKg=0;
        }
        public void Tick()
        {
            foreach(var fleet in world.fleets.Where(f=>f.mission=="Mining"))
            {
                foreach(var id in fleet.shipIds)
                {
                    if(fleet.minedKg>=fleet.targetKg-WorldUtil.Epsilon)break;
                    try{fleet.minedKg+=mining.Extract(id,fleet.targetAsteroidId,Math.Min(0.4,fleet.targetKg-fleet.minedKg));}
                    catch(InvalidOperationException){fleet.mission="Completed";break;}
                }
                if(fleet.minedKg>=fleet.targetKg-WorldUtil.Epsilon)fleet.mission="Completed";
            }
        }
    }
}
