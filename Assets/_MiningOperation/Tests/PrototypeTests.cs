#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MiningOperation.Tests
{
    public class PrototypeTests
    {
        [Test]
        public void StartStation_IsPublic_AndCannotBeUpgraded()
        {
            var game=WorldRuntime.NewGame();
            Assert.AreEqual("npc.public",game.Stations.Get("station.public").ownerId);
            Assert.Throws<InvalidOperationException>(()=>game.Stations.InstallModule("station.public","storage"));
        }
        [Test]
        public void InventoryReservations_PreventDoubleSpending()
        {
            var game=WorldRuntime.NewGame();
            var wh=game.Inventory.Warehouse("player","station.public").id;
            game.Inventory.Reserve(wh,"ore.iron_nickel",20,"test.1");
            Assert.Throws<InvalidOperationException>(()=>game.Inventory.Reserve(wh,"ore.iron_nickel",10,"test.2"));
            Assert.AreEqual(5,game.Inventory.Available(wh,"ore.iron_nickel"),0.001);
            game.Inventory.Release(wh,"ore.iron_nickel","test.1",20);
            game.Validate();
        }
        [Test]
        public void SellOrder_PaysOnlyAfterPhysicalPickup()
        {
            var game=WorldRuntime.NewGame();
            var credits=game.Wallets.Get("player").balance;
            var order=game.Trading.PlaceSell("player","station.public","ore.iron_nickel",5,15);
            Assert.AreEqual(credits,game.Wallets.Get("player").balance);
            Assert.AreEqual(5,game.Inventory.ReservationAmount(order.inventoryId,order.itemId,order.id),0.001);
            game.Advance(2); // NPC buyer posts order and trade is matched.
            Assert.Greater(order.matched,0);
            Assert.AreEqual(credits,game.Wallets.Get("player").balance);
            game.Advance(30); // Pickup occurs.
            Assert.Greater(game.Wallets.Get("player").balance,credits);
            game.Validate();
        }
        [Test]
        public void Purchase_HoldsFunds_DeliversLater_AndFitsOnlyWhenDocked()
        {
            var game=WorldRuntime.NewGame();
            var order=game.Trading.PlaceBuy("player","station.public","module.thruster",1,20000);
            var initialBalance=game.Wallets.Get("player").balance;
            Assert.Greater(game.Wallets.Get("player").held,0);
            game.Advance(2);
            Assert.Greater(order.matched,0);
            Assert.AreEqual(0,game.Inventory.Amount(order.inventoryId,"module.thruster"),0.001);
            game.Advance(150);
            Assert.AreEqual(1,game.Inventory.Amount(order.inventoryId,"module.thruster"),0.001);
            Assert.Less(game.Wallets.Get("player").balance,initialBalance);
            game.Fitting.Fit(game.State.controlledShipId,"propulsion.1","module.thruster");
            Assert.AreEqual("module.thruster",game.Ships.Get(game.State.controlledShipId).slots.First(x=>x.slotId=="propulsion.1").itemId);
            game.Validate();
        }
        [Test]
        public void Industry_ConsumesAtStart_ProducesOnCompletion()
        {
            var game=WorldRuntime.NewGame();
            var wh=game.Inventory.Warehouse("player","station.public").id;
            var initial=game.Inventory.Amount(wh,"ore.iron_nickel");
            game.Industry.Start("player","station.public","separate.iron_nickel");
            Assert.AreEqual(initial-10,game.Inventory.Amount(wh,"ore.iron_nickel"),0.001);
            game.Advance(40);
            Assert.GreaterOrEqual(game.Inventory.Amount(wh,"concentrate.iron"),18);
            game.Validate();
        }
        [Test]
        public void SaveLoad_PreservesInFlightShipmentAndReservations()
        {
            var game=WorldRuntime.NewGame();
            game.Trading.PlaceSell("player","station.public","ore.iron_nickel",5,15);
            game.Advance(2);
            var filename=Path.Combine(Application.temporaryCachePath,"mo_test_"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                var save=new WorldSaveService(filename);
                save.Save(game);
                var loaded=save.Load();
                Assert.AreEqual(game.State.orders.Count,loaded.State.orders.Count);
                Assert.AreEqual(game.State.shipments.Count,loaded.State.shipments.Count);
                loaded.Advance(180);
                loaded.Validate();
            }
            finally{if(File.Exists(filename))File.Delete(filename);if(File.Exists(filename+".bak"))File.Delete(filename+".bak");}
        }
        [Test]
        public void Docking_AutomaticApproach_ChargesFeeOnlyOnCompletion()
        {
            var game=WorldRuntime.NewGame();
            var ship=game.Ships.Get(game.State.controlledShipId);
            game.Docking.Undock(ship.id);
            var before=game.Wallets.Get("player").balance;
            game.Docking.RequestDock(ship.id,"station.public");
            Assert.AreEqual(before,game.Wallets.Get("player").balance);
            game.Advance(20);
            Assert.AreEqual("station.public",ship.dockedAtStationId);
            Assert.AreEqual(before-30,game.Wallets.Get("player").balance);
        }
        [Test]
        public void CancelOrder_ReleasesGoodsOrCredits()
        {
            var game=WorldRuntime.NewGame();
            var wh=game.Inventory.Warehouse("player","station.public").id;
            var sell=game.Trading.PlaceSell("player","station.public","ore.iron_nickel",8,18);
            Assert.AreEqual(17,game.Inventory.Available(wh,"ore.iron_nickel"),0.001);
            game.Trading.Cancel(sell.id);
            Assert.AreEqual(25,game.Inventory.Available(wh,"ore.iron_nickel"),0.001);
            var buy=game.Trading.PlaceBuy("player","station.public","module.thruster",1,20000);
            Assert.AreEqual(20000,game.Wallets.Get("player").held);
            game.Trading.Cancel(buy.id);
            Assert.AreEqual(0,game.Wallets.Get("player").held);
            game.Validate();
        }
        [Test]
        public void Production_WaitsForFreeStorage_WithoutDuplicatingOutputs()
        {
            var game=WorldRuntime.NewGame();
            var wh=game.Inventory.Warehouse("player","station.public");
            var job=game.Industry.Start("player","station.public","separate.iron_nickel");
            wh.capacityKg=game.Inventory.Mass(wh.id);
            game.Advance(40);
            Assert.AreEqual("AwaitingStorage",job.status);
            wh.capacityKg+=50;
            game.Advance(1);
            Assert.AreEqual("Completed",job.status);
            Assert.AreEqual(18,game.Inventory.Amount(wh.id,"concentrate.iron"),0.001);
            game.Advance(100);
            Assert.AreEqual(18,game.Inventory.Amount(wh.id,"concentrate.iron"),0.001);
            game.Validate();
        }
        [Test]
        public void MobileStation_TravelBlocksIndustryAndSurvivesTicks()
        {
            var game=WorldRuntime.NewGame();
            game.Wallets.Credit("player",400000);
            var station=game.Stations.BuyStation("Carrier",true);
            game.Stations.InstallModule(station.id,"refinery");
            game.Stations.BeginMove(station.id,400,80,120);
            Assert.AreEqual("Traveling",station.mobilityState);
            game.Advance(60);
            Assert.AreEqual("Traveling",station.mobilityState);
            Assert.Greater(station.x,180);
            game.Advance(61);
            Assert.AreEqual("Operational",station.mobilityState);
            Assert.AreEqual(400,station.x,0.001);
            game.Validate();
        }
        [Test]
        public void PlayerStation_IsPurchasedSeparately_NotUpgradedFromPublic()
        {
            var game=WorldRuntime.NewGame();
            game.Wallets.Credit("player",250000);
            var own=game.Stations.BuyStation("Test Station",false);
            Assert.AreEqual("player",own.ownerId);
            Assert.AreEqual("npc.public",game.Stations.Get("station.public").ownerId);
            game.Stations.InstallModule(own.id,"refinery");
            Assert.IsTrue(game.Stations.HasModule(own,"refinery"));
            game.Validate();
        }
        [Test]
        public void Stations_HaveDistinctSceneIdentifiers_ForSeparateMaps()
        {
            var g=WorldRuntime.NewGame();
            var st=g.State.stations.Where(x=>x.kind=="public").ToArray();
            Assert.AreEqual(3,st.Length);
            Assert.AreEqual(3,st.Select(x=>x.sceneId).Distinct().Count());
        }
        [Test]
        public void Docking_RejectsStationFromDifferentScene()
        {
            var g=WorldRuntime.NewGame();var ship=g.Ships.Get(g.State.controlledShipId);
            g.Docking.Undock(ship.id);
            Assert.Throws<InvalidOperationException>(()=>g.Docking.RequestDock(ship.id,"station.trade"));
            Assert.IsNull(ship.dockingTargetId);
        }
        [Test]
        public void Mining_RejectsAsteroidFromAnotherSector()
        {
            var g=WorldRuntime.NewGame();var ship=g.Ships.Get(g.State.controlledShipId);
            g.Docking.Undock(ship.id);
            var a=g.State.asteroids[0];a.sceneId="sector.other";
            Assert.Throws<InvalidOperationException>(()=>g.Mining.Extract(ship.id,a.id,5));
        }
        [Test]
        public void NewStation_InheritsMainSector_NotPublicStationOwnership()
        {
            var g=WorldRuntime.NewGame();g.Wallets.Credit("player",200000);
            var st=g.Stations.BuyStation("Test",false);
            Assert.AreEqual("starter",st.sceneId);
            Assert.AreEqual("player",st.ownerId);
            Assert.AreNotEqual(g.Stations.Get("station.public").id,st.id);
        }
        [Test]
        public void SceneIdentity_SurvivesWorldSaveAndLoad()
        {
            var g=WorldRuntime.NewGame();g.Stations.Get("station.public").sceneId="Assets/Scenes/02_EmptyGame.unity";
            var filename=Path.Combine(Application.temporaryCachePath,"mo_sector_"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                var io=new WorldSaveService(filename);io.Save(g);
                Assert.AreEqual("Assets/Scenes/02_EmptyGame.unity",io.Load().Stations.Get("station.public").sceneId);
            }
            finally{if(File.Exists(filename))File.Delete(filename);if(File.Exists(filename+".bak"))File.Delete(filename+".bak");}
        }
    }
}
#endif
