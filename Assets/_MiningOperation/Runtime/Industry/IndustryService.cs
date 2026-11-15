using System;
using System.Linq;

namespace MiningOperation
{
    public sealed class IndustryService
    {
        private readonly WorldState world;private readonly ContentCatalog catalog;private readonly InventoryService inventory;
        private readonly WalletService wallets;private readonly StationService stations;
        public IndustryService(WorldState w,ContentCatalog c,InventoryService inv,WalletService wal,StationService st)
        {world=w;catalog=c;inventory=inv;wallets=wal;stations=st;}
        public ProductionJobState Start(string owner,string stationId,string recipeId,int batches=1)
        {
            WorldUtil.Require(batches>0&&batches<=100,"Invalid batch count");
            var st=stations.Get(stationId);WorldUtil.Require(stations.CanUse(st,owner)&&st.allowsIndustry,"Industry unavailable");
            var recipe=catalog.Recipe(recipeId);
            WorldUtil.Require(stations.HasModule(st,recipe.facilityId),"Station facility missing: "+recipe.facilityId);
            var wh=inventory.Warehouse(owner,st.id).id;
            foreach(var req in recipe.inputs)
                WorldUtil.Require(inventory.Available(wh,req.itemId)+WorldUtil.Epsilon>=req.amount*batches,"Missing ingredient: "+req.itemId);
            var fee=st.ownerId==owner?0:checked(st.tariffs.processingFee*batches);
            WorldUtil.Require(wallets.Available(owner)>=fee,"Insufficient processing fee");
            if(fee>0)wallets.Pay(owner,st.ownerId,fee);
            foreach(var req in recipe.inputs)inventory.Remove(wh,req.itemId,req.amount*batches);
            var job=new ProductionJobState {id="job."+WorldUtil.Id(),ownerId=owner,stationId=stationId,
                outputInventoryId=wh,recipeId=recipeId,batches=batches,startedAt=world.gameSeconds,
                completesAt=world.gameSeconds+recipe.seconds*batches/
                    (1+0.25*(st.modules.First(m=>m.definitionId==recipe.facilityId).level-1))};
            world.production.Add(job);return job;
        }
        public void Tick()
        {
            foreach(var j in world.production.Where(x=>x.status=="Running"||x.status=="AwaitingStorage"))
            {
                if(j.completesAt>world.gameSeconds)continue;
                var recipe=catalog.Recipe(j.recipeId);
                var dest=inventory.Get(j.outputInventoryId);
                var additional=recipe.outputs.Sum(o=>o.amount*j.batches*catalog.Item(o.itemId).unitMassKg);
                if(inventory.Mass(dest.id)+additional>dest.capacityKg+WorldUtil.Epsilon)
                {j.status="AwaitingStorage";continue;}
                foreach(var o in recipe.outputs)inventory.Add(dest.id,o.itemId,o.amount*j.batches);
                j.status="Completed";
            }
        }
    }
}
