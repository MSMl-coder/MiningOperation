using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MiningOperation
{
    // Runtime UI Toolkit. All controls invoke real domain services; no mock button behavior.
    // The only 3D dependency is MOSceneCoordinator and its existing scene anchors.
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class MOStationTerminal : MonoBehaviour
    {
        [SerializeField] private MOSceneCoordinator coordinator;
        private UIDocument document;
        private VisualElement root, frame, content, nav, header;
        private Label statusLabel, liveLabel;
        private string page = "Station", status = "Ready", itemId = "ore.iron_nickel";
        private string tradeQty = "5", tradePrice = "18", transferQty = "5", batchQty = "1", mineQty = "10";
        private string crewName = "Crew Member", salary = "150", crewProfession = "Fleet Commander";
        private string selectedShipSlot = "industrial.1", selectedFitItem = "module.collector_launcher", selectedFittingShipId = "";
        private string selectedRecipe = "separate.iron_nickel";
        private string selectedAsteroidId = "", selectedFleetId = "", selectedPersonnelId = "", selectedStationId = "";
        private string selectedFleetShip = "", selectedFleetCommander = "", selectedFleetAsteroid = "";
        private string nextScene = "", nameOfFleet = "Prospector", orderFilter = "", catalogFilter = "";
        private long dockingFee = 0, fittingFee = 0, processingFee = 0;
        private string commission = ".02";
        private bool visible = true, subscribed;
        public bool IsOpen => visible;
        private float liveTimer;
        private string lastSnapshot = "";
        private static readonly string[] pages = {"Station", "Storage", "Market", "Industry", "Ship Fitting", "Asteroids", "Fleet", "Personnel", "My Stations", "Economy", "Catalog", "Activity"};
        private readonly Color bg = new Color(.045f,.075f,.11f,.96f), card = new Color(.095f,.145f,.19f,.96f);
        private readonly Color fg = new Color(.88f,.93f,.95f), muted = new Color(.59f,.72f,.77f), orange = new Color(1f,.40f,.14f);
        private WorldRuntime G => coordinator != null ? coordinator.Game : null;
        private string StationId => !string.IsNullOrEmpty(selectedStationId) ? selectedStationId : coordinator.CurrentStationId;
        private StationState Station => G?.State.stations.FirstOrDefault(x=>x.id==StationId);
        private ShipState PlayerShip => G?.State.ships.FirstOrDefault(x=>x.id==G.State.controlledShipId);
        private bool DockedHere => PlayerShip != null && PlayerShip.dockedAtStationId == StationId;
        private void Awake()
        {
            if (coordinator == null) coordinator = GetComponent<MOSceneCoordinator>();
            document = GetComponent<UIDocument>();
        }
        private void OnEnable()
        {
            if(coordinator != null) { coordinator.WorldChanged += HandleWorldChange; subscribed = true; }
            Build();
        }
        private void OnDisable()
        {
            if (subscribed && coordinator != null) coordinator.WorldChanged -= HandleWorldChange;
            subscribed = false;
        }
        private void HandleWorldChange() { if (root != null && G != null && string.IsNullOrEmpty(selectedStationId)) selectedStationId=coordinator.CurrentStationId; }
        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
            { visible = !visible; Build(); }
#endif
            liveTimer += Time.unscaledDeltaTime;
            if (visible && G != null && root != null && !IsEditingText())
            {
                var snapshot = Snapshot();
                if (snapshot != lastSnapshot) { Build(); return; }
            }
            if(liveTimer > 0.5f)
            {
                liveTimer = 0;
                if (liveLabel != null && G != null)
                    liveLabel.text = "TIME " + G.State.gameSeconds.ToString("F0") + "s    CR " + G.Wallets.Available("player").ToString("N0") +
                        "    HELD " + G.Wallets.Get("player").held + "    SHIP " +
                        (PlayerShip?.dockedAtStationId ?? (PlayerShip?.dockingTargetId != null ? "DOCKING" : "SPACE"));
            }
        }
        private string Snapshot()
        {
            if(G == null) return "none";
            var ship = PlayerShip;
            return (ship?.dockedAtStationId ?? "-") + "/" + (ship?.dockingTargetId ?? "-") + "/" +
                G.State.orders.Count + "/" + G.State.shipments.Sum(x=>(int)(x.status=="Scheduled"?0:x.status=="InTransit"?1:x.status=="Delivered"?2:3)) +
                "/" + G.State.production.Sum(x=>(int)(x.status=="Running"?0:x.status=="Completed"?1:2)) +
                "/" + G.State.events.Count + "/" + G.State.stations.Count + "/" + G.State.shipBuilds.Count(x=>x.status=="Completed");
        }
        private bool IsEditingText()
        {
            var focused = root?.panel?.focusController?.focusedElement as VisualElement;
            while(focused != null)
            {
                if(focused is TextField) return true;
                focused = focused.parent;
            }
            return false;
        }
        private void Build()
        {
            if(document == null || document.panelSettings == null) return;
            root = document.rootVisualElement; root.Clear();
            root.style.flexGrow = 1; root.style.height = Length.Percent(100); root.style.width = Length.Percent(100);
            root.pickingMode = PickingMode.Ignore;
            var open = Btn("◈ MINING OPERATION  /  F6", () => {visible=true;Build();}, false);
            open.style.position=Position.Absolute;open.style.top=12;open.style.left=12;
            root.Add(open); open.style.display=visible?DisplayStyle.None:DisplayStyle.Flex;
            if(!visible) return;
            frame = new VisualElement();frame.style.position=Position.Absolute;frame.style.top=12;frame.style.left=12;
            frame.style.width=Length.Percent(90);frame.style.maxWidth=1200;frame.style.height=Length.Percent(93);
            frame.style.backgroundColor=bg; frame.style.borderBottomWidth=1;frame.style.borderLeftWidth=1;
            frame.style.borderTopWidth=1;frame.style.borderRightWidth=1;
            frame.style.borderBottomColor=new Color(.25f,.38f,.46f);frame.style.borderTopColor=new Color(.25f,.38f,.46f);
            frame.style.borderRightColor=new Color(.25f,.38f,.46f);frame.style.borderLeftColor=new Color(.25f,.38f,.46f);
            frame.style.borderTopLeftRadius=10;frame.style.borderTopRightRadius=10;
            frame.style.borderBottomLeftRadius=10;frame.style.borderBottomRightRadius=10;
            root.Add(frame);
            header = new VisualElement();header.style.paddingLeft=16;header.style.paddingRight=16;
            header.style.paddingTop=12;header.style.paddingBottom=9;header.style.flexDirection=FlexDirection.Row;
            header.style.alignItems=Align.Center;header.style.backgroundColor=new Color(.065f,.105f,.15f,.96f);
            frame.Add(header);
            var heading = new VisualElement();heading.style.flexGrow=1;
            heading.Add(Text("▰ MINING OPERATION",19,orange,true));
            heading.Add(Text("STATION OPERATIONS TERMINAL  /  " + (coordinator?.SceneKey ?? "SCENE"),10,muted));
            header.Add(heading);
            liveLabel=Text("",12,fg);liveLabel.style.marginRight=12;header.Add(liveLabel);
            header.Add(Btn("SAVE",()=>Act(()=>coordinator.SaveWorld(),"World saved")));
            header.Add(Btn("LOAD",()=>Act(()=>{coordinator.LoadWorld();selectedStationId=coordinator.CurrentStationId;},"Save loaded")));
            header.Add(Btn("NEW",()=>Act(()=>{coordinator.NewWorld();selectedStationId=coordinator.CurrentStationId;},"New session (not saved)")));
            header.Add(Btn("×",()=>{visible=false;Build();}));
            var middle = new VisualElement();middle.style.flexGrow=1;middle.style.flexDirection=FlexDirection.Row;
            middle.style.minHeight=0;frame.Add(middle);
            nav=new ScrollView(ScrollViewMode.Vertical);nav.style.width=185;nav.style.flexShrink=0;
            nav.style.backgroundColor=new Color(.052f,.09f,.13f,1);nav.style.paddingTop=9;
            nav.style.paddingLeft=7;nav.style.paddingRight=7; middle.Add(nav);
            foreach(var name in pages)
            {
                var copy=name; var b=Btn(name,()=>{page=copy;Build();},false);
                if(name==page) {b.style.backgroundColor = new Color(.22f,.29f,.35f);b.style.borderLeftWidth=3;b.style.borderLeftColor=orange;}
                b.style.marginBottom=3; nav.Add(b);
            }
            content = new ScrollView(ScrollViewMode.Vertical);content.style.flexGrow=1;
            content.style.paddingTop=13; content.style.paddingLeft=16; content.style.paddingRight=16;
            middle.Add(content);
            statusLabel = Text(status,12,status.StartsWith("ERROR")?new Color(1f,.53f,.40f):muted);
            statusLabel.style.marginTop=4;statusLabel.style.marginBottom=6;
            var footer=new VisualElement();footer.style.flexDirection=FlexDirection.Row;footer.style.paddingLeft=14;footer.style.alignItems=Align.Center;
            footer.style.backgroundColor=new Color(.065f,.105f,.15f);footer.Add(statusLabel);
            var spacer=new VisualElement();spacer.style.flexGrow=1;footer.Add(spacer);
            footer.Add(Btn("REFRESH",Build));footer.Add(Btn("+1 MIN",()=>Act(()=>coordinator.Advance(60),"World advanced")));
            footer.Add(Btn("+5 MIN",()=>Act(()=>coordinator.Advance(300),"World advanced")));
            footer.Add(Btn("F6 HIDE",()=>{visible=false;Build();}));frame.Add(footer);
            if(G==null){Section("WORLD LOAD ERROR");Note(coordinator?.LastError ?? "No coordinator available");return;}
            if(string.IsNullOrEmpty(selectedStationId)) selectedStationId=coordinator.CurrentStationId;
            lastSnapshot = Snapshot();
            try
            {
                switch(page)
                {
                    case "Station":DrawStation();break;
                    case "Storage":DrawStorage();break;
                    case "Market":DrawMarket();break;
                    case "Industry":DrawIndustry();break;
                    case "Ship Fitting":DrawFitting();break;
                    case "Asteroids":DrawAsteroids();break;
                    case "Fleet":DrawFleet();break;
                    case "Personnel":DrawPersonnel();break;
                    case "My Stations":DrawOwned();break;
                    case "Economy":DrawEconomy();break;
                    case "Catalog":DrawCatalog();break;
                    case "Activity":DrawActivity();break;
                }
            }
            catch(Exception ex) {content.Add(Text("UI: " + ex.Message,13,orange));Debug.LogException(ex);}
        }
        private void Act(Action action,string message="Operation completed")
        {
            try {WorldUtil.Require(G!=null,"No active world");action();G.Validate();status=message;}
            catch(Exception ex) {status="ERROR: " + ex.Message;Debug.LogWarning("[MO UI] "+ex);}
            Build();
        }
        private Label Text(string value,int size=13,Color? color=null,bool bold=false)
        {
            var x=new Label(value ?? "");x.style.fontSize=size;x.style.color=color??fg;
            x.style.whiteSpace=WhiteSpace.Normal;x.style.marginBottom=5;
            if(bold)x.style.unityFontStyleAndWeight=FontStyle.Bold;
            return x;
        }
        private Button Btn(string caption,Action callback,bool accent=true)
        {
            var b=new Button(callback){text=caption};b.style.fontSize=12;b.style.height=29;
            b.style.marginRight=5;b.style.marginBottom=4;b.style.paddingLeft=9;b.style.paddingRight=9;
            b.style.color=fg;b.style.backgroundColor=accent?new Color(.17f,.27f,.34f):new Color(.095f,.145f,.19f);
            b.style.borderTopLeftRadius=4;b.style.borderTopRightRadius=4;b.style.borderBottomLeftRadius=4;b.style.borderBottomRightRadius=4;
            return b;
        }
        private void Section(string title,string sub=null)
        {
            var heading=Text(title.ToUpperInvariant(),17,fg,true);heading.style.marginBottom=7;content.Add(heading);
            if(!string.IsNullOrEmpty(sub))content.Add(Text(sub,12,muted));
        }
        private void Note(string s) => content.Add(Text(s,12,muted));
        private VisualElement Row()
        {var r=new VisualElement();r.style.flexDirection=FlexDirection.Row;r.style.flexWrap=Wrap.Wrap;r.style.alignItems=Align.Center;r.style.marginBottom=7;content.Add(r);return r;}
        private void Card(string title,params string[] details)
        {
            var c=new VisualElement();c.style.backgroundColor=card;c.style.paddingTop=9;c.style.paddingLeft=11;c.style.paddingRight=11;
            c.style.paddingBottom=9;c.style.marginBottom=7;c.style.borderBottomLeftRadius=5;c.style.borderTopLeftRadius=5;
            content.Add(c);c.Add(Text(title,13,fg,true));foreach(var d in details)c.Add(Text(d,12,muted));
        }
        private TextField Field(VisualElement parent,string name,string value,Action<string> changed,int width=150)
        {
            var v=new TextField(name){value=value};v.style.width=width;v.style.color=fg;v.style.marginRight=9;v.style.marginBottom=7;
            v.RegisterValueChangedCallback(e=>changed(e.newValue));parent.Add(v);return v;
        }
        private DropdownField Select(VisualElement parent,string title,IEnumerable<string> values,string current,Action<string> changed,int width=220)
        {
            var list=values.Where(x=>!string.IsNullOrEmpty(x)).Distinct().ToList();
            if(list.Count==0){parent.Add(Text(title+": none",12,muted));return null;}
            if(!list.Contains(current))current=list[0];
            var dd=new DropdownField(title,list,current);dd.style.width=width;dd.style.color=fg;dd.style.marginRight=8;dd.style.marginBottom=6;
            dd.RegisterValueChangedCallback(e=>changed(e.newValue));parent.Add(dd);return dd;
        }
        private double Num(string value)
        {
            if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out double d) &&
               !double.TryParse(value,NumberStyles.Float,CultureInfo.CurrentCulture,out d))
                throw new InvalidOperationException("Invalid number: " + value);
            WorldUtil.Require(WorldUtil.IsPositive(d),"Number must be positive");return d;
        }
        private long Money(string value)
        {WorldUtil.Require(long.TryParse(value,out var n)&&n>0,"Enter a positive whole price");return n;}
        private int Batches() {return checked((int)Math.Round(Num(batchQty)));}
        private string WhId => G.Inventory.Warehouse("player",StationId).id;
        private void StationPicker(bool includeAll=false)
        {
            var stations=includeAll?G.State.stations.Where(x=>x.ownerId=="player"||x.id==coordinator.CurrentStationId):
                G.State.stations.Where(x=>x.id==coordinator.CurrentStationId || x.ownerId=="player");
            var current=stations.Select(x=>x.id).ToList();
            if(current.Count==0){Note("No station bound to this scene. Use the editor setup command.");return;}
            var row=Row();Select(row,"Station",current,StationId,x=>{
                selectedStationId=x;
                var st=G.Stations.Get(x);
                dockingFee=st.tariffs.dockingFee;fittingFee=st.tariffs.fittingFee;
                processingFee=st.tariffs.processingFee;
                commission=st.tariffs.saleCommission.ToString(CultureInfo.InvariantCulture);
                Build();
            },285);
            if(Station!=null)row.Add(Text(Station.displayName + " / " + Station.kind,12,muted));
        }
        private void DrawStation()
        {
            Section("Station overview","Select the station model in the scene and bind it through Mining Operation → Bind Selected Outpost.");
            StationPicker();var st=Station;var ship=PlayerShip;
            if(st==null||ship==null)return;
            Card(st.displayName,"Owner: "+st.ownerId+"    Type: "+st.kind+"    Sector: "+st.sceneId,
                "Dock fee: "+st.tariffs.dockingFee+" CR    Fit fee: "+st.tariffs.fittingFee+" CR    Production: "+st.tariffs.processingFee+" CR/batch",
                "Services: "+(st.allowsTrading?"Trading ":"")+(st.allowsIndustry?"Industry":"") + "     Ship: " +
                (DockedHere?"Docked":ship.dockingTargetId==st.id?"Approaching":"In space"));
            var row=Row();
            if(ship.dockedAtStationId==null && ship.dockingTargetId==null && ship.sceneId==st.sceneId)
                row.Add(Btn("DOCK (AUTO)",()=>Act(()=>G.Docking.RequestDock(ship.id,st.id),"Docking requested")));
            if(DockedHere) row.Add(Btn("UNDOCK",()=>Act(()=>coordinator.Undock(),"Undocked")));
            if(ship.dockingTargetId==st.id) Note("Autopilot docking in progress. Simulation time remaining: "+Math.Max(0,ship.dockingCompleteAt-G.State.gameSeconds).ToString("F0")+"s");
            Card("Player vessel",ship.displayName+"    Hull: "+ship.hullDefinitionId,
                "Cargo: "+G.Inventory.Mass(ship.cargoInventoryId).ToString("F1")+" / "+G.Inventory.Get(ship.cargoInventoryId).capacityKg+" kg");
            if(DockedHere)
            {
                row=Row();row.Add(Btn("UNLOAD ALL CARGO",()=>Act(()=>{
                    foreach(var s in G.Inventory.Get(ship.cargoInventoryId).stacks.ToArray())
                        G.Mining.Unload(ship.id,st.id,s.itemId,s.amount);
                },"Cargo moved to warehouse")));
                row.Add(Btn("OPEN FITTING",()=>{page="Ship Fitting";Build();}));
                row.Add(Btn("OPEN STORAGE",()=>{page="Storage";Build();}));
            }
            Note("Public station upgrades are disabled. Buy your own station on the My Stations page.");
            Section("Scene travel","Type a loaded scene NAME. Add destination scenes to Unity Build Profiles and bind one NPC outpost in each.");
            row=Row();Field(row,"Destination scene",nextScene,x=>nextScene=x,265);
            row.Add(Btn("TRAVEL TO SCENE",()=>Act(()=>coordinator.RequestTravel(nextScene),"Travel initiated")));
        }
        private void DrawStorage()
        {
            Section("Storage","Player-owned inventory at station. Reserved quantities cannot be moved or sold twice.");
            StationPicker();if(Station==null)return;
            var wh=G.Inventory.Warehouse("player",StationId);
            Card("Warehouse",G.Inventory.Mass(wh.id).ToString("F1")+" / "+wh.capacityKg.ToString("F1")+" kg");
            foreach(var s in wh.stacks.OrderBy(x=>x.itemId))
            {
                var row=Row();row.Add(Text(G.Catalog.Item(s.itemId).name+"   " +s.amount.ToString("F2")+"  (free "+G.Inventory.Available(wh.id,s.itemId).ToString("F2")+")",12,fg));
                row.Add(Btn("SELECT",()=>{itemId=s.itemId;Build();},false));
            }
            Section("Cargo transfer","Both directions require your controlled ship to be docked at this station.");
            var r=Row();Select(r,"Item",G.Catalog.Items.Select(x=>x.id),itemId,x=>itemId=x,240);
            Field(r,"Amount",transferQty,x=>transferQty=x,110);
            r=Row();r.Add(Btn("SHIP → WAREHOUSE",()=>Act(()=>{
                WorldUtil.Require(DockedHere,"Dock first");G.Inventory.Transfer(PlayerShip.cargoInventoryId,wh.id,itemId,Num(transferQty));
            },"Cargo unloaded")));
            r.Add(Btn("WAREHOUSE → SHIP",()=>Act(()=>{
                WorldUtil.Require(DockedHere,"Dock first");G.Inventory.Transfer(wh.id,PlayerShip.cargoInventoryId,itemId,Num(transferQty));
            },"Cargo loaded")));
            Card("Ship cargo",string.Join("  |  ",G.Inventory.Get(PlayerShip.cargoInventoryId).stacks.Select(x=>x.itemId+":"+x.amount.ToString("F1"))));
        }
        private void DrawMarket()
        {
            Section("Market","SELL: payment at physical pickup. BUY: funds held now, goods delivered later.");
            StationPicker();if(Station==null)return;
            var r=Row();Select(r,"Commodity",G.Catalog.Items.Select(x=>x.id),itemId,x=>{
                itemId=x;tradePrice=G.Economy.Price(Station.regionId,itemId).ToString();Build();},310);
            Field(r,"Quantity",tradeQty,x=>tradeQty=x,110);Field(r,"Unit price",tradePrice,x=>tradePrice=x,115);
            Note("Local indicative price: " +G.Economy.Price(Station.regionId,itemId)+" CR per unit");
            r=Row();r.Add(Btn("PLACE SELL",()=>Act(()=>{
                var o=G.Trading.PlaceSell("player",StationId,itemId,Num(tradeQty),Money(tradePrice));
                G.Log("Sell order listed: "+o.id);
            },"Sell order listed; payment pending pickup")));
            r.Add(Btn("PLACE BUY",()=>Act(()=>{
                var o=G.Trading.PlaceBuy("player",StationId,itemId,Num(tradeQty),Money(tradePrice));
                G.Log("Buy order placed: "+o.id);
            },"Buy order placed; funds reserved")));
            r.Add(Btn("CHECK MATCHES",()=>Act(()=>G.Matching.Match(20),"Matching cycle complete"),false));
            Section("My orders");
            foreach(var o in G.State.orders.Where(x=>x.ownerId=="player").Reverse().Take(40))
            {
                r=Row();r.Add(Text(o.side.ToUpperInvariant()+"  "+o.itemId+"  "+o.fulfilled.ToString("F1")+"/"+o.quantity.ToString("F1")+
                    "  @"+o.unitPrice+" CR   "+o.status,12,fg));
                if(o.status!="Completed"&&o.status!="Cancelled")
                {var id=o.id;r.Add(Btn("CANCEL",()=>Act(()=>G.Trading.Cancel(id),"Order cancelled"),false));}
            }
            Section("Shipment ledger");
            foreach(var sh in G.State.shipments.Where(x=>
                G.State.orders.Any(o=>o.id==x.buyOrderId&&o.ownerId=="player" || o.id==x.sellOrderId&&o.ownerId=="player")).Reverse().Take(30))
                Card(sh.itemId+"  ×"+sh.quantity.ToString("F1")+"  ["+sh.status+"]",
                    sh.fromStationId+" → "+sh.toStationId+"    pickup: "+sh.pickupAt.ToString("F0")+"s  delivery: "+sh.deliveryAt.ToString("F0")+"s");
        }
        private void DrawIndustry()
        {
            Section("Industry","Available recipes depend on the current station's production facilities.");
            StationPicker();if(Station==null)return;
            var allowed=G.Catalog.Recipes.Where(x=>G.Stations.HasModule(Station,x.facilityId)).ToList();
            var r=Row();Select(r,"Recipe",allowed.Select(x=>x.id),selectedRecipe,x=>{selectedRecipe=x;Build();},300);
            Field(r,"Batches",batchQty,x=>batchQty=x,115);
            var recipe=allowed.FirstOrDefault(x=>x.id==selectedRecipe)??allowed.FirstOrDefault();
            if(recipe!=null)
            {
                Card(recipe.name,"Needs: "+string.Join("; ",recipe.inputs.Select(x=>x.itemId+" ×"+x.amount)),
                    "Produces: "+string.Join("; ",recipe.outputs.Select(x=>x.itemId+" ×"+x.amount)),
                    "Facility: "+recipe.facilityId+"   Duration: "+recipe.seconds+" s/batch");
                r=Row();r.Add(Btn("START PRODUCTION",()=>Act(()=>{
                    G.Industry.Start("player",StationId,recipe.id,Batches());
                },"Production started")));
            }
            else Note("No available production facilities.");
            Section("Jobs");
            foreach(var j in G.State.production.Where(x=>x.ownerId=="player").Reverse().Take(30))
                Card(j.recipeId+"  ×"+j.batches+"    "+j.status,"Station: "+j.stationId+"   Finishes at: "+j.completesAt.ToString("F0")+" s");
        }
        private void DrawFitting()
        {
            Section("Ship fitting","Select a ship docked at a station with fitting services. Use actual items from its station warehouse.");
            var owned=G.State.ships.Where(x=>x.ownerId=="player").ToList();
            if(owned.Count==0){Note("No owned ships");return;}
            var picker=Row();Select(picker,"Player ship",owned.Select(x=>x.id),
                string.IsNullOrEmpty(selectedFittingShipId)?G.State.controlledShipId:selectedFittingShipId,
                x=>{selectedFittingShipId=x;Build();},370);
            var ship=owned.FirstOrDefault(x=>x.id==selectedFittingShipId)??PlayerShip;
            Card(ship.displayName,"Docked at: "+(ship.dockedAtStationId??"Not docked"),
                "Hull: "+ship.hullDefinitionId+"   Cargo: "+G.Inventory.Mass(ship.cargoInventoryId).ToString("F1")+" kg");
            foreach(var s in ship.slots)
            {
                var r=Row();r.Add(Text(s.slotId+" ["+s.slotType+"]  "+(s.itemId??"EMPTY"),12,fg));
                var key=s.slotId;
                r.Add(Btn("SELECT",()=>{selectedShipSlot=key;Build();},false));
            }
            var slot=ship.slots.FirstOrDefault(x=>x.slotId==selectedShipSlot)??ship.slots.FirstOrDefault();
            if(slot==null)return;
            var choices=G.Catalog.Items.Where(x=>x.fitsSlot==slot.slotType).ToArray();
            if(choices.Length==0){Note("No components defined for this slot type.");return;}
            var row=Row();Select(row,"Compatible component",choices.Select(x=>x.id),selectedFitItem,x=>selectedFitItem=x,340);
            row=Row();row.Add(Btn("FIT",()=>Act(()=>G.Fitting.Fit(ship.id,slot.slotId,
                choices.Any(x=>x.id==selectedFitItem)?selectedFitItem:choices[0].id),"Equipment fitted")));
            row.Add(Btn("UNFIT",()=>Act(()=>G.Fitting.Unfit(ship.id,slot.slotId),"Equipment returned to warehouse"),false));
            if(!string.IsNullOrEmpty(ship.dockedAtStationId))
            {
                var wh=G.Inventory.Warehouse("player",ship.dockedAtStationId);
                Note("Available warehouse units: "+string.Join("  |  ",choices.Select(x=>x.id+"="+
                    G.Inventory.Available(wh.id,x.id).ToString("F0"))));
            }
            else Note("Ship must dock before fitting.");
        }
        private void DrawAsteroids()
        {
            Section("Asteroid field","Bind existing scene asteroid meshes using MOSceneAsteroidAnchor to enable mining at their actual positions.");
            var asteroids=G.State.asteroids.Where(x=>x.sceneId==coordinator.SceneKey).ToList();
            var r=Row();Field(r,"Extraction mass (kg)",mineQty,x=>mineQty=x,155);
            if(asteroids.Count==0)Note("No asteroid anchors bound in this scene. Use Mining Operation → Bind Selected Asteroid.");
            foreach(var a in asteroids)
            {
                var distance=WorldUtil.Distance(PlayerShip.x,PlayerShip.y,PlayerShip.z,a.x,a.y,a.z);
                r=Row();r.Add(Text(a.displayName+" / "+a.oreItemId+" / "+a.remainingKg.ToString("F1")+" kg  /  distance "+distance.ToString("F0")+" m",12,fg));
                var id=a.id;r.Add(Btn("EXTRACT",()=>Act(()=>{
                    var amount=G.Mining.Extract(PlayerShip.id,id,Num(mineQty));G.Log("Extracted "+amount+" kg from "+id);
                },"Ore extracted into ship cargo")));
            }
        }
        private void DrawFleet()
        {
            Section("Fleet management","Assign your own available ships and employed commanders, then select a local asteroid operation.");
            var r=Row();Field(r,"Fleet name",nameOfFleet,x=>nameOfFleet=x,190);
            r.Add(Btn("CREATE FLEET",()=>Act(()=>{
                var f=G.Fleets.Create(nameOfFleet,coordinator.CurrentStationId);selectedFleetId=f.id;
            },"Fleet created")));
            var spareShips=G.State.ships.Where(x=>x.ownerId=="player" && x.id!=G.State.controlledShipId &&
                !G.State.fleets.Any(y=>y.shipIds.Contains(x.id))).ToList();
            var commanders=G.State.personnel.Where(x=>x.profession=="Fleet Commander"&&x.employerId=="player"&&
                string.IsNullOrEmpty(x.assignedFleetId)).ToList();
            var targets=G.State.asteroids.Where(x=>x.sceneId==coordinator.SceneKey&&x.remainingKg>0).ToList();
            r=Row();Select(r,"Available ship",spareShips.Select(x=>x.id),selectedFleetShip,x=>selectedFleetShip=x,280);
            Select(r,"Commander",commanders.Select(x=>x.id),selectedFleetCommander,x=>selectedFleetCommander=x,280);
            r=Row();Select(r,"Target asteroid",targets.Select(x=>x.id),selectedFleetAsteroid,x=>selectedFleetAsteroid=x,340);
            foreach(var f in G.State.fleets.Where(x=>x.ownerId=="player"))
            {
                Card(f.name,"Ships: "+string.Join(", ",f.shipIds)+"   Commander: "+(f.commanderPersonId??"none"),
                    "Mission: "+f.mission+"   Mined: "+f.minedKg.ToString("F1")+" / "+f.targetKg.ToString("F1")+" kg");
                r=Row();
                if(spareShips.Count>0)
                    r.Add(Btn("ASSIGN CHOSEN SHIP",()=>Act(()=>G.Fleets.AssignShip(f.id,
                        spareShips.Any(x=>x.id==selectedFleetShip)?selectedFleetShip:spareShips[0].id),"Ship assigned")));
                if(commanders.Count>0)
                    r.Add(Btn("ASSIGN CHOSEN COMMANDER",()=>Act(()=>G.Fleets.AssignCommander(f.id,
                        commanders.Any(x=>x.id==selectedFleetCommander)?selectedFleetCommander:commanders[0].id),"Commander assigned")));
                if(targets.Count>0)
                    r.Add(Btn("MINE 20 KG",()=>Act(()=>G.Fleets.StartMission(f.id,
                        targets.Any(x=>x.id==selectedFleetAsteroid)?selectedFleetAsteroid:targets[0].id,20),"Fleet mission started")));
            }
            Note("For spare mining ships, build at your own shipyard and fit a mining laser.");
        }
        private void DrawPersonnel()
        {
            Section("Personnel","Crew is an economic entity, not physically simulated inside cabins.");
            var r=Row();Field(r,"Name",crewName,x=>crewName=x,200);
            Select(r,"Profession",new[]{"Fleet Commander","Engineer","Station Manager","Industrial Operator","Logistics Officer","Shipyard Specialist"},crewProfession,x=>crewProfession=x,200);
            Field(r,"Salary per day",salary,x=>salary=x,145);
            r=Row();r.Add(Btn("HIRE",()=>Act(()=>G.Personnel.Hire(crewName,crewProfession,Money(salary)),"Employee hired")));
            foreach(var p in G.State.personnel.Where(x=>x.employerId=="player"))
            {
                r=Row();r.Add(Text(p.name+" / "+p.profession+" / "+p.salaryPerDay+" CR/day / assignment: "+(p.assignedFleetId??p.assignedStationId??"none"),12,fg));
                var owned=G.State.stations.FirstOrDefault(x=>x.ownerId=="player");
                if(owned!=null && p.assignedStationId!=owned.id)
                    r.Add(Btn("ASSIGN BASE",()=>Act(()=>G.Personnel.AssignStation(p.id,owned.id),"Employee assigned"),false));
            }
        }
        private void DrawOwned()
        {
            Section("My stations","The original public outpost cannot be upgraded or administered. Buy a different station.");
            var r=Row();r.Add(Btn("BUY ORBITAL STATION · 180k",()=>Act(()=>{
                var s=G.Stations.BuyStation("Pioneer Orbital Hub",false);selectedStationId=s.id;
            },"Station acquired")));
            r.Add(Btn("BUY MOBILE PLATFORM · 260k",()=>Act(()=>{
                var s=G.Stations.BuyStation("Prospector Mobile Base",true);selectedStationId=s.id;
            },"Mobile platform acquired")));
            var owned=G.State.stations.Where(x=>x.ownerId=="player").ToList();
            if(owned.Count==0){Note("No player stations purchased yet.");return;}
            StationPicker(true);var st=Station;
            if(st==null||st.ownerId!="player"){Note("Select your own station to configure it.");return;}
            Card(st.displayName,"Slots: "+G.Stations.UsedSlots(st)+" / "+st.moduleSlots+"    Power: "+G.Stations.UsedPower(st).ToString("F1")+" / "+st.powerLimit,
                "Visitors: "+st.allowsVisitors+"   Trade: "+st.allowsTrading+"   Status: "+st.mobilityState);
            r=Row();r.Add(Btn("TOGGLE VISITORS",()=>Act(()=>G.Stations.SetVisitors(st.id,!st.allowsVisitors),"Visitor policy updated")));
            r.Add(Btn("TOGGLE TRADE",()=>Act(()=>G.Stations.SetTrading(st.id,!st.allowsTrading),"Market policy updated")));
            Section("Tariffs");
            r=Row();Field(r,"Dock CR",dockingFee.ToString(),x=>long.TryParse(x,out dockingFee),125);
            Field(r,"Fitting CR",fittingFee.ToString(),x=>long.TryParse(x,out fittingFee),125);
            Field(r,"Processing CR",processingFee.ToString(),x=>long.TryParse(x,out processingFee),145);
            Field(r,"Sale commission (0–0.25)",commission,x=>commission=x,195);
            r=Row();r.Add(Btn("APPLY TARIFFS",()=>Act(()=>{
                var rate=double.Parse(commission,CultureInfo.InvariantCulture);G.Stations.SetTariffs(st.id,new StationTariffs {
                    dockingFee=dockingFee,fittingFee=fittingFee,processingFee=processingFee,saleCommission=rate});
            },"Tariffs saved")));
            Section("Modules","Only player-owned stations permit construction, upgrade and dismantling.");
            foreach(var m in G.Catalog.Modules)
            {
                var installed=st.modules.FirstOrDefault(x=>x.definitionId==m.id);
                r=Row();r.Add(Text(m.name+"  ["+(installed==null?"not installed":"MK-"+installed.level)+"]   "+m.cost+" CR / "+m.slots+" SP",12,fg));
                var mid=m.id;
                if(installed==null)r.Add(Btn("INSTALL",()=>Act(()=>G.Stations.InstallModule(st.id,mid),"Module installed")));
                else
                {
                    r.Add(Btn("UPGRADE",()=>Act(()=>G.Stations.UpgradeModule(st.id,mid),"Module upgraded")));
                    r.Add(Btn("REMOVE",()=>Act(()=>G.Stations.RemoveModule(st.id,mid),"Module removed"),false));
                }
            }
            if(G.Stations.HasModule(st,"shipyard"))
            {
                Section("Shipyard");
                r=Row();r.Add(Btn("BUILD UTILITY VESSEL",()=>Act(()=>G.Shipyard.StartBuild(st.id,"hull.utility"),"Ship construction started")));
                foreach(var b in G.State.shipBuilds.Where(x=>x.stationId==st.id))Card("Ship build: "+b.hullId,b.status+"   complete at "+b.completesAt.ToString("F0")+"s");
            }
            if(st.kind=="mobile")
            {
                r=Row();r.Add(Btn("REPOSITION +100,+100 (120s)",()=>Act(()=>G.Stations.BeginMove(st.id,st.x+100,st.z+100,120),"Mobile base traveling")));
            }
        }
        private void DrawEconomy()
        {
            Section("World economy","All regions and NPC companies continue their abstract simulation even when their scene is not loaded.");
            foreach(var st in G.State.stations)
                Card(st.displayName,st.ownerId+" / "+st.regionId+" / Scene: "+st.sceneId+" / "+st.mobilityState);
            Section("Regional prices");
            foreach(var reg in G.State.markets)
            {
                content.Add(Text(reg.displayName,14,fg,true));
                foreach(var p in reg.prices.Where(x=>x.itemId==itemId || x.demand>0 || x.supply>0).Take(35))
                    content.Add(Text(p.itemId+"    "+p.currentPrice+" CR    Supply "+p.supply.ToString("F1")+"    Demand "+p.demand.ToString("F1"),12,muted));
            }
            Note("NPC agents: "+G.State.agents.Count+"    Orders: "+G.State.orders.Count+"    Shipments: "+G.State.shipments.Count);
        }
        private void DrawCatalog()
        {
            Section("Content catalog","Items have stable IDs shared by inventory, recipes, markets and fittings.");
            var r=Row();Field(r,"Filter ID/name",catalogFilter,x=>catalogFilter=x,330);
            r.Add(Btn("APPLY FILTER",Build,false));
            foreach(var x in G.Catalog.Items.Where(x=>x.id.IndexOf(catalogFilter,StringComparison.OrdinalIgnoreCase)>=0 ||
                x.name.IndexOf(catalogFilter,StringComparison.OrdinalIgnoreCase)>=0))
            {
                r=Row();r.Add(Text(x.name+" / "+x.id+" / "+x.category+" / "+x.basePrice+" CR / "+x.unitMassKg+" kg",12,fg));
                var id=x.id;r.Add(Btn("TRADE",()=>{itemId=id;tradePrice=G.Economy.Price(Station.regionId,id).ToString();page="Market";Build();},false));
            }
        }
        private void DrawActivity()
        {
            Section("Activity log","Game state changes and system events.");
            foreach(var x in G.State.events.AsEnumerable().Reverse().Take(80))
                content.Add(Text("["+x.at.ToString("F0")+"s]  "+x.message,12,muted));
        }
    }
}
