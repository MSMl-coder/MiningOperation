using System;
using System.Linq;

namespace MiningOperation
{
    public sealed class InventoryService
    {
        private readonly WorldState world; private readonly ContentCatalog catalog;
        public InventoryService(WorldState w,ContentCatalog c) { world=w; catalog=c; }
        public InventoryState Get(string id)
        { var x=world.inventories.FirstOrDefault(i=>i.id==id); WorldUtil.Require(x!=null,"Inventory missing: "+id); return x; }
        public InventoryState Warehouse(string owner,string station)
        {
            var id="warehouse."+station+"."+owner;
            var existing=world.inventories.FirstOrDefault(i=>i.id==id);
            if(existing!=null) return existing;
            var inv=new InventoryState { id=id,ownerId=owner,capacityKg=1500 };
            world.inventories.Add(inv); return inv;
        }
        public double Amount(string inv,string item)
        { var s=Get(inv).stacks.FirstOrDefault(x=>x.itemId==item); return s==null?0:s.amount; }
        public double Reserved(string inv,string item)
        { return Get(inv).reserved.Where(x=>x.itemId==item).Sum(x=>x.amount); }
        public double Available(string inv,string item)
        { return Math.Max(0,Amount(inv,item)-Reserved(inv,item)); }
        public double Mass(string inv)
        { return Get(inv).stacks.Sum(s=>s.amount*catalog.Item(s.itemId).unitMassKg); }
        public bool CanAdd(string inv,string item,double amount)
        {
            if(!WorldUtil.IsPositive(amount)) return false;
            var def=catalog.Item(item);
            if(!def.bulk && Math.Abs(amount-Math.Round(amount))>WorldUtil.Epsilon) return false;
            return Mass(inv)+amount*def.unitMassKg<=Get(inv).capacityKg+WorldUtil.Epsilon;
        }
        public void Add(string inv,string item,double amount)
        {
            WorldUtil.Require(CanAdd(inv,item,amount),"Inventory capacity/quantity invalid: "+inv);
            var stock=Get(inv).stacks.FirstOrDefault(s=>s.itemId==item);
            if(stock==null) Get(inv).stacks.Add(new ItemStack {itemId=item,amount=amount});
            else stock.amount+=amount;
        }
        public void Remove(string inv,string item,double amount)
        {
            WorldUtil.Require(WorldUtil.IsPositive(amount) && Available(inv,item)+WorldUtil.Epsilon>=amount,"Insufficient unreserved inventory: "+item);
            RemoveUnchecked(inv,item,amount);
        }
        private void RemoveUnchecked(string inv,string item,double amount)
        {
            var stock=Get(inv).stacks.First(s=>s.itemId==item);
            stock.amount-=amount;
            if(stock.amount<=WorldUtil.Epsilon) Get(inv).stacks.Remove(stock);
        }
        public void Transfer(string from,string to,string item,double amount)
        {
            WorldUtil.Require(Available(from,item)+WorldUtil.Epsilon>=amount,"Insufficient stock");
            WorldUtil.Require(CanAdd(to,item,amount),"Destination full");
            Remove(from,item,amount); Add(to,item,amount);
        }
        public void Reserve(string inv,string item,double amount,string key)
        {
            WorldUtil.Require(WorldUtil.IsPositive(amount) && Available(inv,item)+WorldUtil.Epsilon>=amount,"Not enough goods to reserve");
            WorldUtil.Require(!string.IsNullOrWhiteSpace(key),"Reservation key required");
            var r=Get(inv).reserved.FirstOrDefault(x=>x.key==key && x.itemId==item);
            if(r==null) Get(inv).reserved.Add(new ItemReservation {key=key,itemId=item,amount=amount});
            else r.amount+=amount;
        }
        public double ReservationAmount(string inv,string item,string key)
        { return Get(inv).reserved.Where(x=>x.key==key && x.itemId==item).Sum(x=>x.amount); }
        public void Release(string inv,string item,string key,double amount)
        {
            var r=Get(inv).reserved.FirstOrDefault(x=>x.key==key&&x.itemId==item);
            WorldUtil.Require(r!=null&&WorldUtil.IsPositive(amount)&&r.amount+WorldUtil.Epsilon>=amount,"Missing reservation");
            r.amount-=amount; if(r.amount<=WorldUtil.Epsilon)Get(inv).reserved.Remove(r);
        }
        public void ConsumeReserved(string inv,string item,string key,double amount)
        {
            WorldUtil.Require(ReservationAmount(inv,item,key)+WorldUtil.Epsilon>=amount,"Not reserved");
            Release(inv,item,key,amount);
            WorldUtil.Require(Available(inv,item)+WorldUtil.Epsilon>=amount,"Corrupt inventory reservation");
            RemoveUnchecked(inv,item,amount);
        }
        public void Validate()
        {
            foreach(var inv in world.inventories)
            {
                WorldUtil.Require(Mass(inv.id) <= inv.capacityKg + WorldUtil.Epsilon,
                    "Inventory overload: " + inv.id);
                foreach(var stock in inv.stacks)
                {
                    WorldUtil.Require(stock.amount>0,"Non-positive stock");
                    WorldUtil.Require(Reserved(inv.id,stock.itemId)<=stock.amount+WorldUtil.Epsilon,"Overreserved stock");
                }
                foreach(var r in inv.reserved)
                    WorldUtil.Require(r.amount>0&&r.amount<=Amount(inv.id,r.itemId)+WorldUtil.Epsilon,"Invalid reservation");
            }
        }
    }
}
