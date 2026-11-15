using System;
using System.Collections.Generic;
using System.Linq;

namespace MiningOperation
{
    public sealed class WorldEconomyService
    {
        private readonly WorldState world;private readonly ContentCatalog catalog;
        private readonly InventoryService inventory;private readonly TradeOrderService orders;
        private readonly WalletService wallets;private readonly StationService stations;
        public WorldEconomyService(WorldState w,ContentCatalog c,InventoryService i,TradeOrderService o,WalletService wal,StationService st)
        {world=w;catalog=c;inventory=i;orders=o;wallets=wal;stations=st;}
        public long Price(string regionId,string itemId)
        {
            var market=world.markets.FirstOrDefault(x=>x.id==regionId);
            var p=market==null?null:market.prices.FirstOrDefault(x=>x.itemId==itemId);
            return p==null?catalog.Item(itemId).basePrice:p.currentPrice;
        }
        public void Tick()
        {
            // The entire world is simulated at fixed 30-second economic intervals.
            UpdatePrices();
            foreach(var agent in world.agents)
            {
                var st=stations.Get(agent.stationId);
                var inv=inventory.Warehouse(agent.id,st.id);
                bool pending=world.orders.Any(o=>o.ownerId==agent.id && o.itemId==agent.preferredItemId &&
                                            o.status!="Completed" && o.status!="Cancelled");
                if(pending)continue;
                var def=catalog.Item(agent.preferredItemId);
                double amount=def.bulk?Math.Min(25,agent.targetStockKg):Math.Min(2,agent.targetStockKg);
                var price=Price(st.regionId,agent.preferredItemId);
                if(agent.kind=="Supplier")
                {
                    amount=Math.Min(amount,inventory.Available(inv.id,agent.preferredItemId));
                    if(amount>WorldUtil.Epsilon)
                        orders.PlaceSell(agent.id,st.id,agent.preferredItemId,amount,Math.Max(1,price*92/100));
                }
                else if(agent.kind=="Buyer")
                {
                    var stock=inventory.Amount(inv.id,agent.preferredItemId);
                    var openBuy=world.orders.Where(o=>o.side=="Buy"&&o.ownerId==agent.id&&o.itemId==agent.preferredItemId&&o.status!="Cancelled")
                        .Sum(o=>o.quantity-o.fulfilled);
                    amount=Math.Min(amount,Math.Max(0,agent.targetStockKg-stock-openBuy));
                    if(!def.bulk)amount=Math.Floor(amount);
                    if(amount>WorldUtil.Epsilon && wallets.Available(agent.id)>=WorldUtil.Cost(Math.Max(1,price*108/100),amount))
                        orders.PlaceBuy(agent.id,st.id,agent.preferredItemId,amount,Math.Max(1,price*108/100));
                }
            }
        }
        private void UpdatePrices()
        {
            foreach(var market in world.markets)
            foreach(var p in market.prices)
            {
                double sell=world.orders.Where(o=>o.side=="Sell"&&o.itemId==p.itemId&&o.status!="Cancelled"&&o.status!="Completed"
                          &&stations.Get(o.stationId).regionId==market.id).Sum(o=>o.quantity-o.matched);
                double buy=world.orders.Where(o=>o.side=="Buy"&&o.itemId==p.itemId&&o.status!="Cancelled"&&o.status!="Completed"
                          &&stations.Get(o.stationId).regionId==market.id).Sum(o=>o.quantity-o.matched);
                p.supply=sell;p.demand=buy;
                double imbalance=(buy-sell)/Math.Max(10,buy+sell);
                double target=p.basePrice*(1+0.45*imbalance);
                p.currentPrice=Math.Max(1,checked((long)Math.Round(p.currentPrice*0.75+target*0.25)));
            }
        }
    }
}
