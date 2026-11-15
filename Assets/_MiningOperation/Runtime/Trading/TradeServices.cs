using System;
using System.Linq;
using System.Collections.Generic;

namespace MiningOperation
{
    // The shipment ledger owns cargo between pickup and delivery. Unity visual ships are never authoritative.
    public sealed class TradeOrderService
    {
        private readonly WorldState world; private readonly InventoryService inv;private readonly WalletService wallet;
        private readonly StationService stations;private readonly ContentCatalog catalog;
        public TradeOrderService(WorldState w,InventoryService i,WalletService wal,StationService st,ContentCatalog c)
        {world=w;inv=i;wallet=wal;stations=st;catalog=c;}
        public MarketOrderState PlaceSell(string owner,string stationId,string itemId,double qty,long price)
        {
            var station=stations.Get(stationId);
            WorldUtil.Require(stations.CanUse(station,owner)&&station.allowsTrading,"Trading restricted");
            WorldUtil.Require(WorldUtil.IsPositive(qty)&&price>0,"Invalid sell order");
            var def=catalog.Item(itemId);
            WorldUtil.Require(Math.Abs(qty-Math.Round(qty))<WorldUtil.Epsilon,"Prototype market uses whole trade units");
            var source=inv.Warehouse(owner,stationId).id;
            var o=new MarketOrderState {id="order."+WorldUtil.Id(),side="Sell",ownerId=owner,stationId=stationId,
                inventoryId=source,itemId=itemId,quantity=qty,unitPrice=price};
            inv.Reserve(source,itemId,qty,o.id);
            world.orders.Add(o);return o;
        }
        public MarketOrderState PlaceBuy(string owner,string stationId,string itemId,double qty,long maxUnitPrice)
        {
            var st=stations.Get(stationId);
            WorldUtil.Require(stations.CanUse(st,owner)&&st.allowsTrading,"Trading restricted");
            WorldUtil.Require(WorldUtil.IsPositive(qty)&&maxUnitPrice>0,"Invalid buy order");
            var def=catalog.Item(itemId);
            WorldUtil.Require(Math.Abs(qty-Math.Round(qty))<WorldUtil.Epsilon,"Prototype market uses whole trade units");
            var held=WorldUtil.Cost(maxUnitPrice,qty);
            wallet.Hold(owner,held);
            var o=new MarketOrderState {id="order."+WorldUtil.Id(),side="Buy",ownerId=owner,stationId=stationId,
                inventoryId=inv.Warehouse(owner,stationId).id,itemId=itemId,quantity=qty,unitPrice=maxUnitPrice,heldCredits=held};
            world.orders.Add(o);return o;
        }
        public MarketOrderState Get(string id)
        { var o=world.orders.FirstOrDefault(x=>x.id==id);WorldUtil.Require(o!=null,"Order missing");return o; }
        public void Cancel(string id)
        {
            var o=Get(id);
            WorldUtil.Require(o.status!="Cancelled" && o.status!="Completed","Order already closed");
            WorldUtil.Require(o.matched-o.fulfilled<WorldUtil.Epsilon,"Orders with shipments in progress cannot be cancelled");
            if(o.side=="Sell")
            {
                double remainder=o.quantity-o.fulfilled;
                if(remainder>WorldUtil.Epsilon)inv.Release(o.inventoryId,o.itemId,o.id,remainder);
            }
            else {if(o.heldCredits>0){wallet.Release(o.ownerId,o.heldCredits);o.heldCredits=0;}}
            o.status="Cancelled";
        }
        public void RefreshStatus(MarketOrderState o)
        {
            if(o.status=="Cancelled")return;
            if(o.fulfilled>=o.quantity-WorldUtil.Epsilon)
            {
                o.status="Completed";
                if(o.side=="Buy"&&o.heldCredits>0){wallet.Release(o.ownerId,o.heldCredits);o.heldCredits=0;}
            }
            else o.status=o.matched>WorldUtil.Epsilon?"PartiallyMatched":"Open";
        }
    }

    public sealed class TradeMatchingService
    {
        private readonly WorldState world; private readonly TradeOrderService orders;
        private readonly StationService stations;private readonly ContentCatalog catalog;
        public TradeMatchingService(WorldState w,TradeOrderService o,StationService st,ContentCatalog c)
        {world=w;orders=o;stations=st;catalog=c;}
        public int Match(int maxMatches=10)
        {
            int count=0;
            var sellers=world.orders.Where(o=>o.side=="Sell"&&o.status!="Cancelled"&&o.status!="Completed").OrderBy(o=>o.unitPrice).ToList();
            var buyers=world.orders.Where(o=>o.side=="Buy"&&o.status!="Cancelled"&&o.status!="Completed").OrderByDescending(o=>o.unitPrice).ToList();
            foreach(var sell in sellers)
            {
                foreach(var buy in buyers)
                {
                    if(count>=maxMatches)return count;
                    if(sell.itemId!=buy.itemId || sell.ownerId==buy.ownerId || buy.unitPrice<sell.unitPrice)continue;
                    if(sell.quantity-sell.matched<=WorldUtil.Epsilon||buy.quantity-buy.matched<=WorldUtil.Epsilon)continue;
                    if(!stations.Get(sell.stationId).allowsTrading||!stations.Get(buy.stationId).allowsTrading)continue;
                    var amount=Math.Min(5,Math.Min(sell.quantity-sell.matched,buy.quantity-buy.matched));
                    if(!catalog.Item(sell.itemId).bulk)amount=Math.Floor(amount);
                    if(!WorldUtil.IsPositive(amount))continue;
                    var distance=WorldUtil.Distance(stations.Get(sell.stationId).x,stations.Get(sell.stationId).y,stations.Get(sell.stationId).z,
                        stations.Get(buy.stationId).x,stations.Get(buy.stationId).y,stations.Get(buy.stationId).z);
                    var pickupAt=world.gameSeconds+8+distance/80;
                    var shipment=new ShipmentState {id="shipment."+WorldUtil.Id(),buyOrderId=buy.id,sellOrderId=sell.id,
                        itemId=sell.itemId,quantity=amount,fromStationId=sell.stationId,toStationId=buy.stationId,
                        pickupAt=pickupAt,deliveryAt=pickupAt+15+distance/20,
                        totalPrice=WorldUtil.Cost(sell.unitPrice,amount),carrierId="npc.logistics"};
                    sell.matched+=amount;buy.matched+=amount;
                    world.shipments.Add(shipment);orders.RefreshStatus(sell);orders.RefreshStatus(buy);count++;
                }
            }
            return count;
        }
    }
    public sealed class LogisticsService
    {
        private readonly WorldState world;private readonly InventoryService inv;private readonly WalletService wallet;
        private readonly StationService stations;private readonly TradeOrderService orders;
        public LogisticsService(WorldState w,InventoryService i,WalletService wal,StationService st,TradeOrderService o)
        {world=w;inv=i;wallet=wal;stations=st;orders=o;}
        public void Tick()
        {
            foreach(var shipment in world.shipments)
            {
                if(shipment.status=="Scheduled"&&world.gameSeconds>=shipment.pickupAt)Pickup(shipment);
                if(shipment.status=="InTransit"&&world.gameSeconds>=shipment.deliveryAt)Deliver(shipment);
            }
        }
        private void Pickup(ShipmentState s)
        {
            var sell=orders.Get(s.sellOrderId);var buy=orders.Get(s.buyOrderId);
            var station=stations.Get(s.fromStationId);
            if(inv.ReservationAmount(sell.inventoryId,sell.itemId,sell.id)+WorldUtil.Epsilon<s.quantity)
            {s.status="BlockedPickup";return;}
            if(wallet.Get(buy.ownerId).held < buy.heldCredits || buy.heldCredits<WorldUtil.Cost(buy.unitPrice,s.quantity))
            {s.status="BlockedPickup";return;}
            var maxHeldForShipment=WorldUtil.Cost(buy.unitPrice,s.quantity);
            var fee=checked((long)Math.Floor(s.totalPrice*station.tariffs.saleCommission));
            s.commission=fee;
            // Commit on a single authoritative simulation thread after preconditions validate.
            inv.ConsumeReserved(sell.inventoryId,s.itemId,sell.id,s.quantity);
            wallet.PayHeld(buy.ownerId,sell.ownerId,s.totalPrice-fee);
            if(fee>0)wallet.PayHeld(buy.ownerId,station.ownerId,fee);
            wallet.Release(buy.ownerId,maxHeldForShipment-s.totalPrice);
            buy.heldCredits-=maxHeldForShipment;
            sell.fulfilled+=s.quantity;
            s.status="InTransit";
            orders.RefreshStatus(sell);
        }
        private void Deliver(ShipmentState s)
        {
            var buy=orders.Get(s.buyOrderId);
            if(!inv.CanAdd(buy.inventoryId,s.itemId,s.quantity))
            {s.status="AwaitingStorage";return;}
            inv.Add(buy.inventoryId,s.itemId,s.quantity);
            buy.fulfilled+=s.quantity;s.status="Delivered";
            orders.RefreshStatus(buy);
        }
        public void RetryBlocked()
        {
            foreach(var s in world.shipments)
                if(s.status=="AwaitingStorage"&&inv.CanAdd(orders.Get(s.buyOrderId).inventoryId,s.itemId,s.quantity))Deliver(s);
        }
    }
}
