using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MiningOperation
{
    // Deliberately basic IMGUI diagnostic console. It has no production UI dependency.
    [DisallowMultipleComponent]
    public sealed class MiningOperationDemoBootstrap : MonoBehaviour
    {
        private WorldRuntime game;
        private WorldSaveService saves;
        private PrototypeWorldView sceneView;
        private Rect windowRect=new Rect(12,12,615,710);
        private Vector2 scroll;
        private int tab;
        private string status="Ready";
        private string itemToTrade="ore.iron_nickel";
        private string tradeAmount="5";
        private string tradePrice="15";
        private string selectedStationId="station.public";
        private float speed=1;
        private readonly string[] tabs={"Station","Storage","Market","Industry","Fitting","Fleet","Owned Base","Catalog"};
        private void Awake()
        {
            // Legacy demo may still exist in an authored scene but be disabled by the v0.2 installer.
            // Disabled MonoBehaviours may receive Awake: NEVER spawn demo geometry in that case.
            if (!enabled) return;
            saves=WorldSaveService.Default();
            game=WorldRuntime.NewGame();
            sceneView=gameObject.GetComponent<PrototypeWorldView>();
            if(sceneView==null)sceneView=gameObject.AddComponent<PrototypeWorldView>();
            sceneView.Initialize(game);
        }
        private void Update()
        {
            if(game==null)return;
            try{game.Advance(Math.Min(Time.deltaTime,0.10f)*speed);}
            catch(Exception e){status="Simulation: "+e.Message;speed=0;Debug.LogException(e);}
        }
        private void OnGUI()
        {
            if(game==null)return;
            windowRect.width=Mathf.Min(615,Screen.width-24);
            windowRect.height=Mathf.Min(710,Screen.height-24);
            windowRect=GUILayout.Window(70605,windowRect,DrawConsole,"MINING OPERATION / TECHNICAL PROTOTYPE v0.1");
        }
        private void DrawConsole(int id)
        {
            var p=game.Ships.Get(game.State.controlledShipId);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Time "+game.State.gameSeconds.ToString("F0")+"s",GUILayout.Width(110));
            GUILayout.Label("Credits "+game.Wallets.Available("player"),GUILayout.Width(150));
            GUILayout.Label("Ship "+(p.dockedAtStationId??(p.dockingTargetId!=null?"Docking":"Flying")));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Save",GUILayout.Width(75)))Do(()=>{saves.Save(game);status="Saved to "+saves.PathOnDisk;});
            if(GUILayout.Button("Load",GUILayout.Width(75)))Do(()=>{game=saves.Load();sceneView.Initialize(game);status="Save loaded";});
            if(GUILayout.Button("New",GUILayout.Width(75)))Do(()=>{game=WorldRuntime.NewGame();sceneView.Initialize(game);status="New world";});
            if(GUILayout.Button("+60s",GUILayout.Width(65)))Do(()=>game.Advance(60));
            if(GUILayout.Button("+5m",GUILayout.Width(65)))Do(()=>game.Advance(300));
            if(GUILayout.Button(speed==0?"Resume":speed==1?"Speed ×1":"Speed ×10",GUILayout.Width(90)))speed=speed==0?1:speed==1?10:0;
            GUILayout.EndHorizontal();
            tab=GUILayout.Toolbar(tab,tabs);
            scroll=GUILayout.BeginScrollView(scroll,GUILayout.ExpandHeight(true));
            switch(tab)
            {
                case 0:DrawStation();break;
                case 1:DrawStorage();break;
                case 2:DrawMarket();break;
                case 3:DrawIndustry();break;
                case 4:DrawFitting();break;
                case 5:DrawFleet();break;
                case 6:DrawOwned();break;
                case 7:DrawCatalog();break;
            }
            GUILayout.Space(12);
            GUILayout.Label("RECENT ACTIVITY");
            foreach(var x in game.State.events.Skip(Math.Max(0,game.State.events.Count-5)))GUILayout.Label("["+x.at.ToString("F0")+"] "+x.message);
            GUILayout.EndScrollView();
            GUILayout.Label(status);
            GUI.DragWindow(new Rect(0,0,windowRect.width,24));
        }
        private void Do(Action action)
        {
            try{action();game.Validate();status="OK";}
            catch(Exception e){status="ERROR: "+e.Message;Debug.LogWarning("[MO] "+e);}
        }
        private void DrawStation()
        {
            var s=game.Ships.Get(game.State.controlledShipId);
            GUILayout.Label("OUTPOST 19 / PUBLIC SERVICE CENTER");
            GUILayout.Label("Public NPC ownership. Service fees are charged per use.");
            GUILayout.Label("Controls: W A S D = fly, Q/E = vertical. Dock command is automatic.");
            GUILayout.Label("Position: "+s.x.ToString("F1")+", "+s.y.ToString("F1")+", "+s.z.ToString("F1"));
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Undock"))Do(()=>{game.Docking.Undock(s.id);game.Log("Undocked");});
            if(GUILayout.Button("Dock at Outpost 19"))Do(()=>{game.Docking.RequestDock(s.id,"station.public");game.Log("Dock command accepted");});
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Fly to Fe-Ni field [DEBUG]"))Do(()=>{
                if(!string.IsNullOrEmpty(s.dockedAtStationId))game.Docking.Undock(s.id);
                s.x=85;s.y=0;s.z=4;
            });
            if(GUILayout.Button("Mine 10 kg"))Do(()=>{var amt=game.Mining.Extract(s.id,"asteroid.iron",10);game.Log("Mined "+amt+" kg Fe-Ni ore");});
            GUILayout.EndHorizontal();
            GUILayout.Label("Ship cargo / "+game.Inventory.Mass(s.cargoInventoryId).ToString("F1")+" kg");
            foreach(var stack in game.Inventory.Get(s.cargoInventoryId).stacks)
                GUILayout.Label("  "+stack.itemId+" — "+stack.amount.ToString("F1"));
            if(GUILayout.Button("Unload ALL ship cargo (when docked)"))Do(()=>{
                foreach(var stack in game.Inventory.Get(s.cargoInventoryId).stacks.ToArray())
                    game.Mining.Unload(s.id,s.dockedAtStationId,stack.itemId,stack.amount);
                game.Log("Cargo unloaded");
            });
            GUILayout.Space(6);
            GUILayout.Label("Test world: "+game.State.stations.Count+" stations / "+game.State.shipments.Count+" shipments / "+game.State.asteroids.Count+" asteroids");
            foreach(var st in game.State.stations)
                GUILayout.Label("  "+st.displayName+" ["+st.kind+"] "+st.mobilityState);
        }
        private void DrawStorage()
        {
            var wh=game.Inventory.Warehouse("player",selectedStationId);
            GUILayout.Label("Player warehouse at "+selectedStationId);
            GUILayout.Label("Used "+game.Inventory.Mass(wh.id).ToString("F1")+" / "+wh.capacityKg.ToString("F0")+" kg");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Public Outpost"))selectedStationId="station.public";
            foreach(var st in game.State.stations.Where(x=>x.ownerId=="player"))
                if(GUILayout.Button(st.displayName))selectedStationId=st.id;
            GUILayout.EndHorizontal();
            foreach(var stack in wh.stacks.OrderBy(x=>x.itemId).Take(35))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(game.Catalog.Item(stack.itemId).name,GUILayout.Width(240));
                GUILayout.Label(stack.amount.ToString("F1"),GUILayout.Width(75));
                GUILayout.Label("free "+game.Inventory.Available(wh.id,stack.itemId).ToString("F1"));
                GUILayout.EndHorizontal();
            }
        }
        private void DrawMarket()
        {
            GUILayout.Label("ALPHA MARKET / DELIVERY-BASED SETTLEMENT");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Item",GUILayout.Width(45));itemToTrade=GUILayout.TextField(itemToTrade);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Qty",GUILayout.Width(45));tradeAmount=GUILayout.TextField(tradeAmount,GUILayout.Width(75));
            GUILayout.Label("Unit price",GUILayout.Width(70));tradePrice=GUILayout.TextField(tradePrice,GUILayout.Width(95));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Place SELL order"))Do(()=>{
                var o=game.Trading.PlaceSell("player",selectedStationId,itemToTrade,
                    double.Parse(tradeAmount,System.Globalization.CultureInfo.InvariantCulture),long.Parse(tradePrice));
                game.Log("SELL listed: "+o.itemId+"; payment only after pickup");
            });
            if(GUILayout.Button("Place BUY order"))Do(()=>{
                var o=game.Trading.PlaceBuy("player",selectedStationId,itemToTrade,
                    double.Parse(tradeAmount,System.Globalization.CultureInfo.InvariantCulture),long.Parse(tradePrice));
                game.Log("BUY placed: "+o.itemId+"; credits held");
            });
            GUILayout.EndHorizontal();
            GUILayout.Label("Quick-select item:");
            GUILayout.BeginHorizontal();
            foreach(var item in new[]{"ore.iron_nickel","module.thruster","module.cargo"})
                if(GUILayout.Button(item)) {itemToTrade=item;tradePrice=game.Economy.Price("region.alpha",item).ToString();tradeAmount=item.StartsWith("module")?"1":"5";}
            GUILayout.EndHorizontal();
            GUILayout.Label("Available player credits: "+game.Wallets.Available("player")+"; held: "+game.Wallets.Get("player").held);
            GUILayout.Label("PLAYER ORDERS:");
            foreach(var o in game.State.orders.Where(x=>x.ownerId=="player").Skip(Math.Max(0,game.State.orders.Count-12)))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(o.side+" "+o.itemId+" "+o.fulfilled.ToString("F1")+"/"+o.quantity.ToString("F1")+" "+o.status,GUILayout.ExpandWidth(true));
                if(o.status!="Completed"&&o.status!="Cancelled"&&GUILayout.Button("Cancel",GUILayout.Width(70)))Do(()=>game.Trading.Cancel(o.id));
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("SHIPMENTS:");
            foreach(var sh in game.State.shipments.Skip(Math.Max(0,game.State.shipments.Count-12)))
                GUILayout.Label(sh.itemId+" ×"+sh.quantity.ToString("F1")+" ["+sh.status+"] pickup " +sh.pickupAt.ToString("F0")+", arrive "+sh.deliveryAt.ToString("F0"));
        }
        private void DrawIndustry()
        {
            GUILayout.Label("INDUSTRIAL PROCESSES / PUBLIC STATION SERVICE FEE");
            foreach(var recipe in game.Catalog.Recipes)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(recipe.name+" ("+recipe.seconds+" s)",GUILayout.ExpandWidth(true));
                if(GUILayout.Button("Start",GUILayout.Width(90)))Do(()=>{
                    var job=game.Industry.Start("player",selectedStationId,recipe.id);
                    game.Log("Started "+job.recipeId);
                });
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("JOBS:");
            foreach(var j in game.State.production.Skip(Math.Max(0,game.State.production.Count-10)))
                GUILayout.Label(j.recipeId+"  "+j.status+"  end: "+j.completesAt.ToString("F0"));
        }
        private void DrawFitting()
        {
            var ship=game.Ships.Get(game.State.controlledShipId);
            GUILayout.Label(ship.displayName+" / fitting slots");
            GUILayout.Label("Fit items from player's warehouse at the currently docked station.");
            foreach(var slot in ship.slots)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(slot.slotId+" : "+(slot.itemId??"empty"),GUILayout.Width(310));
                foreach(var def in game.Catalog.Items.Where(x=>x.fitsSlot==slot.slotType).Take(2))
                    if(GUILayout.Button(def.name,GUILayout.Width(120)))Do(()=>game.Fitting.Fit(ship.id,slot.slotId,def.id));
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Hint: buy 'module.thruster' on Market, wait for delivery, then fit in propulsion.1");
        }
        private void DrawFleet()
        {
            GUILayout.Label("FLEET MANAGEMENT / SIMULATED MISSIONS");
            GUILayout.Label("Employed personnel: "+game.State.personnel.Count+" / Fleets: "+game.State.fleets.Count);
            if(GUILayout.Button("Hire Fleet Commander (salary 150/day)"))Do(()=>{
                var p=game.Personnel.Hire("Captain "+(game.State.personnel.Count+1),"Fleet Commander",150);
                game.Log("Hired "+p.name);
            });
            if(GUILayout.Button("Create Fleet"))Do(()=>{
                var f=game.Fleets.Create("Prospector "+(game.State.fleets.Count+1),"station.public");
                game.Log("Fleet created "+f.name);
            });
            if(GUILayout.Button("[DEBUG] Spawn autonomous miner"))Do(()=>{
                var ship=game.Ships.Create("player","hull.utility","Automated Miner",null,80,0,0);
                ship.slots.First(s=>s.slotId=="industrial.1").itemId="module.mining_laser";
                game.Log("Debug fleet ship spawned");
            });
            foreach(var fleet in game.State.fleets)
            {
                GUILayout.Label(fleet.name+" / "+fleet.mission+" / "+fleet.minedKg.ToString("F1")+" kg / "+fleet.shipIds.Count+" ships");
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Assign spare ship"))Do(()=>{
                    var spare=game.State.ships.First(s=>s.ownerId=="player"&&s.id!=game.State.controlledShipId&&
                        !game.State.fleets.Any(f=>f.shipIds.Contains(s.id)));
                    game.Fleets.AssignShip(fleet.id,spare.id);
                });
                if(GUILayout.Button("Assign commander"))Do(()=>{
                    var p=game.State.personnel.First(x=>x.profession=="Fleet Commander");game.Fleets.AssignCommander(fleet.id,p.id);
                });
                if(GUILayout.Button("Mine 20 kg"))Do(()=>game.Fleets.StartMission(fleet.id,"asteroid.iron",20));
                GUILayout.EndHorizontal();
            }
        }
        private void DrawOwned()
        {
            GUILayout.Label("PUBLIC OUTPOST IS NOT PLAYER OWNED AND CANNOT BE UPGRADED");
            if(GUILayout.Button("Buy OWN orbital station (180,000 CR)"))Do(()=>{
                var st=game.Stations.BuyStation("Pioneer Orbital Hub",false);selectedStationId=st.id;
                game.Log("Bought "+st.displayName);
            });
            if(GUILayout.Button("Buy OWN mobile platform (260,000 CR)"))Do(()=>{
                var st=game.Stations.BuyStation("Prospector Mobile Platform",true);selectedStationId=st.id;
                game.Log("Bought mobile platform");
            });
            if(GUILayout.Button("[DEBUG] Grant 200,000 credits"))Do(()=>game.Wallets.Credit("player",200000));
            foreach(var st in game.State.stations.Where(x=>x.ownerId=="player"))
            {
                GUILayout.Label(st.displayName+" / "+st.kind+" / slots "+game.Stations.UsedSlots(st)+"/"+st.moduleSlots+
                    ", power "+game.Stations.UsedPower(st).ToString("F1")+"/"+st.powerLimit);
                GUILayout.BeginHorizontal();
                foreach(var m in game.Catalog.Modules.Where(x=>x.id=="refinery"||x.id=="fabricator"||x.id=="shipyard"))
                    if(GUILayout.Button("+"+m.name))Do(()=>game.Stations.InstallModule(st.id,m.id));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Toggle visitors: "+st.allowsVisitors))Do(()=>game.Stations.SetVisitors(st.id,!st.allowsVisitors));
                if(GUILayout.Button("Increase sales commission"))Do(()=>{
                    var t=st.tariffs;t.saleCommission=Math.Min(0.25,t.saleCommission+0.01);game.Stations.SetTariffs(st.id,t);
                });
                GUILayout.EndHorizontal();
                if(st.kind=="mobile"&&GUILayout.Button("Travel to new sector (120s)"))
                    Do(()=>game.Stations.BeginMove(st.id,st.x+100,st.z+100,120));
                if(game.Stations.HasModule(st,"shipyard"))
                {
                    if(GUILayout.Button("[DEBUG] Grant hull materials"))Do(()=>{
                        var inv=game.Inventory.Warehouse("player",st.id).id;
                        game.Inventory.Add(inv,"assembly.frame",2);game.Inventory.Add(inv,"stock.steel_sheet",6);
                        game.Inventory.Add(inv,"assembly.motor",1);
                    });
                    if(GUILayout.Button("Build Utility Vessel"))Do(()=>game.Shipyard.StartBuild(st.id,"hull.utility"));
                }
            }
            GUILayout.Label("Finished ship builds: "+game.State.shipBuilds.Count(x=>x.status=="Completed"));
        }
        private void DrawCatalog()
        {
            GUILayout.Label("STABLE CONTENT ID / CATEGORY / BASE PRICE");
            foreach(var item in game.Catalog.Items)
                GUILayout.Label(item.id+"   ["+item.category+"]   "+item.basePrice+" CR");
        }
    }
}
