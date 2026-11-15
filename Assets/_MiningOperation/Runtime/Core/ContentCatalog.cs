using System;
using System.Collections.Generic;
using System.Linq;

namespace MiningOperation
{
    // All numeric costs / times are prototype tuning values, NOT realistic engineering figures.
    public sealed class ItemDefinition
    {
        public string id, name, category, fitsSlot;
        public bool bulk;
        public double unitMassKg, powerMW;
        public long basePrice;
        public ItemDefinition(string id, string name, string category, bool bulk, double unitMassKg, long basePrice,
            string fitsSlot = "", double powerMW = 0)
        { this.id=id; this.name=name; this.category=category; this.bulk=bulk;
          this.unitMassKg=unitMassKg; this.basePrice=basePrice; this.fitsSlot=fitsSlot; this.powerMW=powerMW; }
    }
    public sealed class RecipeIngredient
    {
        public string itemId; public double amount;
        public RecipeIngredient(string id, double amount) { itemId=id; this.amount=amount; }
    }
    public sealed class RecipeDefinition
    {
        public string id, name, facilityId; public double seconds;
        public List<RecipeIngredient> inputs = new List<RecipeIngredient>();
        public List<RecipeIngredient> outputs = new List<RecipeIngredient>();
        public RecipeDefinition(string id,string name,string facilityId,double seconds,
            RecipeIngredient[] inputs,RecipeIngredient[] outputs)
        { this.id=id; this.name=name; this.facilityId=facilityId; this.seconds=seconds;
          this.inputs.AddRange(inputs); this.outputs.AddRange(outputs); }
    }
    public sealed class StationModuleDefinition
    {
        public string id, name; public int slots; public double powerMW; public long cost;
        public StationModuleDefinition(string id,string name,int slots,double powerMW,long cost)
        { this.id=id; this.name=name; this.slots=slots; this.powerMW=powerMW; this.cost=cost; }
    }
    public sealed class ContentCatalog
    {
        private readonly Dictionary<string,ItemDefinition> _items = new Dictionary<string,ItemDefinition>();
        private readonly Dictionary<string,RecipeDefinition> _recipes = new Dictionary<string,RecipeDefinition>();
        private readonly Dictionary<string,StationModuleDefinition> _modules = new Dictionary<string,StationModuleDefinition>();
        public IEnumerable<ItemDefinition> Items { get { return _items.Values.OrderBy(x=>x.id); } }
        public IEnumerable<RecipeDefinition> Recipes { get { return _recipes.Values.OrderBy(x=>x.id); } }
        public IEnumerable<StationModuleDefinition> Modules { get { return _modules.Values.OrderBy(x=>x.id); } }
        public ItemDefinition Item(string id) { WorldUtil.Require(_items.ContainsKey(id), "Unknown item: " + id); return _items[id]; }
        public RecipeDefinition Recipe(string id) { WorldUtil.Require(_recipes.ContainsKey(id), "Unknown recipe: " + id); return _recipes[id]; }
        public StationModuleDefinition Module(string id) { WorldUtil.Require(_modules.ContainsKey(id), "Unknown module: " + id); return _modules[id]; }
        private void I(string id,string name,string category,bool bulk,double mass,long price,string slot="",double power=0)
        { _items.Add(id,new ItemDefinition(id,name,category,bulk,mass,price,slot,power)); }
        private static RecipeIngredient R(string id,double amount) { return new RecipeIngredient(id,amount); }
        private void RecipeAdd(string id,string name,string facility,double seconds,RecipeIngredient[] input,RecipeIngredient[] output)
        { _recipes.Add(id,new RecipeDefinition(id,name,facility,seconds,input,output)); }
        private void M(string id,string name,int slots,double power,long cost)
        { _modules.Add(id,new StationModuleDefinition(id,name,slots,power,cost)); }
        public static ContentCatalog Create()
        {
            var c=new ContentCatalog();
            // Raw resources: amounts represent kilograms (bulk=true).
            c.I("ore.iron_nickel","Iron–Nickel Ore","Ore",true,1,18);
            c.I("ore.magnetite","Magnetite Ore","Ore",true,1,14);
            c.I("ore.copper","Copper Ore","Ore",true,1,27);
            c.I("ore.ilmenite","Ilmenite Ore","Ore",true,1,29);
            c.I("ore.pentlandite","Pentlandite Ore","Ore",true,1,36);
            c.I("ore.chromite","Chromite Ore","Ore",true,1,31);
            c.I("ore.tungsten","Tungsten-bearing Ore","Ore",true,1,180);
            c.I("ore.silicate","Silicate Rock","Ore",true,1,5);
            c.I("ore.carbon","Carbonaceous Regolith","Ore",true,1,20);
            c.I("ore.platinum","Platinum-bearing Fragments","Ore",true,1,500);
            c.I("ice.water","Water Ice","Ice",true,1,9);
            c.I("ice.methane","Methane Ice","Ice",true,1,17);
            c.I("ore.graviorite","Graviorite-bearing Ore (fictional)","Exotic",true,1,1600);
            // Concentrates, refined products, chemicals.
            c.I("concentrate.iron","Iron Concentrate","Concentrate",true,1,31);
            c.I("concentrate.nickel","Nickel Concentrate","Concentrate",true,1,68);
            c.I("concentrate.copper","Copper Concentrate","Concentrate",true,1,82);
            c.I("concentrate.carbon","Carbon Concentrate","Concentrate",true,1,54);
            c.I("concentrate.titanium","Titanium Concentrate","Concentrate",true,1,87);
            c.I("material.iron","Refined Iron","Material",true,1,58);
            c.I("material.nickel","Refined Nickel","Material",true,1,135);
            c.I("material.copper","Refined Copper","Material",true,1,164);
            c.I("material.titanium","Refined Titanium","Material",true,1,220);
            c.I("material.graphite","Industrial Graphite","Material",true,1,125);
            c.I("material.silica","Purified Silica","Material",true,1,44);
            c.I("material.steel","Structural Steel","Alloy",true,1,95);
            c.I("material.stainless","Stainless Steel","Alloy",true,1,162);
            c.I("material.titanium_alloy","Titanium Alloy","Alloy",true,1,285);
            c.I("material.ceramic","Technical Ceramic","Material",true,1,115);
            c.I("material.composite","Carbon Composite","Material",true,1,310);
            c.I("fluid.water","Technical Water","Fluid",true,1,18);
            c.I("fluid.coolant","Heat Transfer Coolant","Fluid",true,1,130);
            c.I("fluid.lubricant","Synthetic Lubricant","Fluid",true,1,175);
            c.I("gas.hydrogen","Hydrogen","Gas",true,1,100);
            c.I("gas.oxygen","Oxygen","Gas",true,1,82);
            c.I("gas.methane","Methane","Gas",true,1,55);
            // Bulk stock.
            c.I("stock.steel_sheet","Steel Sheet","Stock",false,5,800);
            c.I("stock.steel_beam","Steel Beam","Stock",false,12,1800);
            c.I("stock.steel_tube","Steel Tube","Stock",false,4,720);
            c.I("stock.steel_rod","Steel Rod","Stock",false,3,530);
            c.I("stock.copper_wire","Copper Wire Coil","Stock",false,2,630);
            c.I("stock.titanium_plate","Titanium Plate","Stock",false,4,1900);
            c.I("stock.ceramic_plate","Ceramic Plate","Stock",false,2,500);
            c.I("stock.composite_panel","Composite Panel","Stock",false,3,1680);
            c.I("stock.optical_glass","Optical Glass Blank","Stock",false,1,960);
            // Parts.
            c.I("part.fasteners","Fastener Set","Part",false,0.5,250);
            c.I("part.spring","Industrial Spring","Part",false,0.8,330);
            c.I("part.gear","Precision Gear","Part",false,1.5,970);
            c.I("part.bearing","Bearing","Part",false,0.8,710);
            c.I("part.shaft","Drive Shaft","Part",false,3,980);
            c.I("part.flange","Pipe Flange","Part",false,1,400);
            c.I("part.valve","Control Valve","Part",false,2,1100);
            c.I("part.sensor","Sensor Array","Part",false,1.2,3800);
            c.I("part.circuit","Control Circuit Board","Part",false,0.5,2700);
            c.I("part.coil","Copper Power Coil","Part",false,1.4,910);
            c.I("part.optics","Optical Lens","Part",false,0.5,1800);
            c.I("part.radiator","Radiator Fin Pack","Part",false,3,1100);
            c.I("part.pipework","Pipework Kit","Part",false,5,1400);
            // Industrial assemblies.
            c.I("assembly.motor","Industrial Electric Motor","Assembly",false,14,6400);
            c.I("assembly.gearbox","Planetary Gearbox","Assembly",false,9,5800);
            c.I("assembly.pump","Coolant Pump","Assembly",false,12,6000);
            c.I("assembly.controller","Industrial Controller","Assembly",false,2,7900);
            c.I("assembly.frame","Structural Frame","Assembly",false,28,9400);
            c.I("assembly.radiator","Cooling Assembly","Assembly",false,16,7500);
            c.I("assembly.generator","Power Generator","Assembly",false,30,16800);
            c.I("assembly.optics","Laser Optical Assembly","Assembly",false,3,12000);
            // Ship fittings: separate physical items from slot instances.
            c.I("module.mining_laser","MK-I Mining Laser","ShipModule",false,20,24000,"industrial",8);
            c.I("module.scanner","Ore Scanner","ShipModule",false,6,11000,"utility",2);
            c.I("module.thruster","Auxiliary Thruster","ShipModule",false,18,18000,"propulsion",7);
            c.I("module.cargo","Cargo Expansion","ShipModule",false,15,12500,"cargo",1);
            c.I("module.collector_launcher","Collector Launcher","ShipModule",false,12,15500,"industrial",4);
            c.I("module.drone_bay","Cargo Drone Bay","ShipModule",false,12,13500,"utility",3);
            c.I("module.ship_repair","Ship Repair Module","ShipModule",false,17,17000,"utility",4);
            c.I("module.grav_field","Gravitational Field Generator","ShipModule",false,20,95000,"industrial",10);
            // Station structural module definitions.
            c.M("refinery","Refinery",2,8,65000); c.M("fabricator","Fabricator",2,9,85000);
            c.M("storage","Storage Expansion",1,1,22000); c.M("fleet_control","Fleet Control",1,3,52000);
            c.M("shipyard","Shipyard",4,18,260000); c.M("trade_hub","Trade Hub",1,2,40000);
            c.M("power","Auxiliary Power Plant",2,-12,53000);
            // Starter accessible recipes. Mass yield / costs are gameplay balances.
            c.RecipeAdd("refine.iron","Smelt Iron Concentrate","refinery",40,
                new[]{R("concentrate.iron",10)},new[]{R("material.iron",8),R("ore.silicate",2)});
            c.RecipeAdd("separate.iron_nickel","Separate Iron-Nickel Ore","refinery",35,
                new[]{R("ore.iron_nickel",10)},new[]{R("concentrate.iron",6),R("concentrate.nickel",2),R("ore.silicate",2)});
            c.RecipeAdd("refine.nickel","Refine Nickel","refinery",55,
                new[]{R("concentrate.nickel",10)},new[]{R("material.nickel",8)});
            c.RecipeAdd("smelt.steel","Alloy Structural Steel","refinery",60,
                new[]{R("material.iron",9),R("material.nickel",1)},new[]{R("material.steel",9)});
            c.RecipeAdd("extract.water","Extract Water Ice","refinery",25,
                new[]{R("ice.water",10)},new[]{R("fluid.water",9)});
            c.RecipeAdd("fabricate.sheet","Roll Steel Sheets","fabricator",45,
                new[]{R("material.steel",10)},new[]{R("stock.steel_sheet",2)});
            c.RecipeAdd("fabricate.beam","Form Steel Beam","fabricator",50,
                new[]{R("material.steel",12)},new[]{R("stock.steel_beam",1)});
            c.RecipeAdd("fabricate.fasteners","Produce Fasteners","fabricator",25,
                new[]{R("material.steel",3)},new[]{R("part.fasteners",4)});
            c.RecipeAdd("fabricate.spring","Wind Spring","fabricator",20,
                new[]{R("stock.steel_rod",1)},new[]{R("part.spring",2)});
            c.RecipeAdd("assemble.frame","Assemble Structural Frame","fabricator",90,
                new[]{R("stock.steel_beam",2),R("part.fasteners",8)},new[]{R("assembly.frame",1)});
            c.RecipeAdd("assemble.motor","Assemble Motor","fabricator",110,
                new[]{R("stock.copper_wire",3),R("part.bearing",2),R("part.shaft",1)},new[]{R("assembly.motor",1)});
            c.RecipeAdd("assemble.repair","Assemble Repair Module","fabricator",160,
                new[]{R("assembly.motor",1),R("assembly.frame",1),R("part.circuit",2)},new[]{R("module.ship_repair",1)});
            return c;
        }
    }
}
