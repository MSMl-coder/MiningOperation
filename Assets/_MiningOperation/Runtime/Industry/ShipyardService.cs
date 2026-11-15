using System;
using System.Linq;

namespace MiningOperation
{
    public sealed class ShipyardService
    {
        private readonly WorldState world;private readonly StationService stations;
        private readonly InventoryService inventory;private readonly ShipService ships;
        public ShipyardService(WorldState w,StationService st,InventoryService inv,ShipService s)
        {world=w;stations=st;inventory=inv;ships=s;}
        public ShipBuildState StartBuild(string stationId,string hullId)
        {
            var st=stations.Owned(stationId);
            WorldUtil.Require(st.mobilityState=="Operational","Shipyard unavailable while traveling");
            WorldUtil.Require(stations.HasModule(st,"shipyard"),"Shipyard module needed");
            WorldUtil.Require(hullId=="hull.utility","Only utility hull available in prototype");
            var inv=inventory.Warehouse(st.ownerId,st.id).id;
            WorldUtil.Require(inventory.Available(inv,"assembly.frame")>=2&&inventory.Available(inv,"stock.steel_sheet")>=6
                &&inventory.Available(inv,"assembly.motor")>=1,"Shipbuilding materials missing");
            inventory.Remove(inv,"assembly.frame",2);
            inventory.Remove(inv,"stock.steel_sheet",6);
            inventory.Remove(inv,"assembly.motor",1);
            var build=new ShipBuildState{id="build."+WorldUtil.Id(),ownerId=st.ownerId,stationId=st.id,
                hullId=hullId,completesAt=world.gameSeconds+600/
                    (1+0.25*(st.modules.First(m=>m.definitionId=="shipyard").level-1))};
            world.shipBuilds.Add(build);return build;
        }
        public void Tick()
        {
            foreach(var b in world.shipBuilds.Where(x=>x.status=="Building"&&x.completesAt<=world.gameSeconds))
            {
                var st=stations.Get(b.stationId);
                ships.Create(b.ownerId,b.hullId,"Utility Vessel",st.id,st.x,st.y,st.z);
                b.status="Completed";
            }
        }
    }
}
