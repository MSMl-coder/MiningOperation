using System;
using System.Linq;

namespace MiningOperation
{
    public sealed class StationService
    {
        private readonly WorldState world; private readonly WalletService wallets;
        private readonly ContentCatalog catalog; private readonly InventoryService inventory;
        public StationService(WorldState w,WalletService wallet,ContentCatalog c,InventoryService inv)
        {world=w;wallets=wallet;catalog=c;inventory=inv;}
        public StationState Get(string id)
        {var s=world.stations.FirstOrDefault(x=>x.id==id);WorldUtil.Require(s!=null,"Station not found: "+id);return s;}
        public bool HasModule(StationState s,string module)
        {return s.modules.Any(x=>x.definitionId==module);}
        public bool CanUse(StationState s,string company)
        {return s.mobilityState=="Operational" && (s.ownerId==company || s.allowsVisitors);}
        public void SetVisitors(string id,bool enabled)
        {var s=Owned(id);s.allowsVisitors=enabled;}
        public void SetTrading(string id,bool enabled)
        {var s=Owned(id);s.allowsTrading=enabled;}
        public void SetTariffs(string id,StationTariffs t)
        {
            var s=Owned(id);
            WorldUtil.Require(t!=null && t.dockingFee>=0&&t.fittingFee>=0&&t.processingFee>=0&&t.saleCommission>=0&&t.saleCommission<=0.25,"Invalid tariff");
            s.tariffs=t;
        }
        public StationState Owned(string id)
        {var s=Get(id);WorldUtil.Require(s.ownerId==world.playerCompanyId,"Cannot manage a public/NPC station");return s;}
        public int UsedSlots(StationState s)
        {return s.modules.Sum(m=>catalog.Module(m.definitionId).slots);}
        public double UsedPower(StationState s)
        {return s.modules.Sum(m=>catalog.Module(m.definitionId).powerMW);}
        public void InstallModule(string stationId,string moduleId)
        {
            var s=Owned(stationId);var def=catalog.Module(moduleId);
            WorldUtil.Require(s.mobilityState=="Operational","Cannot build during travel");
            WorldUtil.Require(!HasModule(s,moduleId),"Already installed");
            WorldUtil.Require(UsedSlots(s)+def.slots<=s.moduleSlots,"No structural slots");
            WorldUtil.Require(UsedPower(s)+def.powerMW<=s.powerLimit,"Power budget exceeded");
            wallets.Debit(s.ownerId,def.cost);
            s.modules.Add(new StationModuleState {definitionId=moduleId});
            if(moduleId=="storage") inventory.Warehouse(s.ownerId,s.id).capacityKg+=2000;
        }
        public void RemoveModule(string stationId,string moduleId)
        {
            var s=Owned(stationId);var entry=s.modules.FirstOrDefault(x=>x.definitionId==moduleId);
            WorldUtil.Require(entry!=null,"Module not installed");
            WorldUtil.Require(!world.production.Any(j=>j.stationId==s.id && (j.status=="Running"||j.status=="AwaitingStorage") &&
                catalog.Recipe(j.recipeId).facilityId==moduleId),"Facility has active jobs");
            WorldUtil.Require(!(moduleId=="shipyard" && world.shipBuilds.Any(j=>j.stationId==s.id && j.status=="Building")),
                "Shipyard still building");
            var def=catalog.Module(moduleId);
            WorldUtil.Require(UsedPower(s)-def.powerMW<=s.powerLimit,"Removing power plant would overload station");
            if(moduleId=="storage")
            {
                var wh=inventory.Warehouse(s.ownerId,s.id);
                WorldUtil.Require(inventory.Mass(wh.id)<=wh.capacityKg-2000*entry.level+WorldUtil.Epsilon,
                    "Cannot remove filled storage expansion");
                wh.capacityKg-=2000*entry.level;
            }
            s.modules.Remove(entry);
            wallets.Credit(s.ownerId,def.cost/2); // Prototype salvage credit.
        }
        public void UpgradeModule(string stationId,string moduleId)
        {
            var s=Owned(stationId);var module=s.modules.FirstOrDefault(x=>x.definitionId==moduleId);
            WorldUtil.Require(module!=null && module.level<3,"Upgrade not available");
            var cost=checked(catalog.Module(moduleId).cost*module.level);
            wallets.Debit(s.ownerId,cost);module.level++;
            if(moduleId=="storage")inventory.Warehouse(s.ownerId,s.id).capacityKg+=2000;
        }
        public StationState BuyStation(string name,bool mobile)
        {
            const long fixedCost=180000, mobileCost=260000;
            wallets.Debit(world.playerCompanyId,mobile?mobileCost:fixedCost);
            var s=new StationState {
                id="station."+WorldUtil.Id(),displayName=name,ownerId=world.playerCompanyId,
                regionId="region.alpha",kind=mobile?"mobile":"owned",moduleSlots=mobile?6:8,powerLimit=30,
                x=mobile?180:120,y=0,z=mobile?50:150,
                tariffs=new StationTariffs {dockingFee=0,fittingFee=0,processingFee=0,saleCommission=0.02}
            };
            world.stations.Add(s);s.storageId=inventory.Warehouse(s.ownerId,s.id).id;
            return s;
        }
        public void BeginMove(string id,double x,double z,double travelSeconds)
        {
            var s=Owned(id);WorldUtil.Require(s.kind=="mobile"&&s.mobilityState=="Operational","Not an operational mobile station");
            WorldUtil.Require(travelSeconds>0,"Travel time must be positive");
            s.mobilityState="Traveling";s.travelStartedAt=world.gameSeconds;
            s.travelUntil=world.gameSeconds+travelSeconds;s.originX=s.x;s.originZ=s.z;
            s.destinationX=x;s.destinationZ=z;
        }
        public void Tick()
        {
            foreach(var s in world.stations)
            {
                if(s.mobilityState!="Traveling")continue;
                var total=Math.Max(0.001,s.travelUntil-s.travelStartedAt);
                var t=Math.Max(0,Math.Min(1,(world.gameSeconds-s.travelStartedAt)/total));
                s.x=s.originX+(s.destinationX-s.originX)*t;
                s.z=s.originZ+(s.destinationZ-s.originZ)*t;
                if(t>=1)s.mobilityState="Operational";
            }
        }
    }
}
