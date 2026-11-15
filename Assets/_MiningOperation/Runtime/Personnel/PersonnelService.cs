using System;
using System.Linq;

namespace MiningOperation
{
    public sealed class PersonnelService
    {
        private readonly WorldState world;private readonly WalletService wallets;private readonly StationService stations;
        public PersonnelService(WorldState w,WalletService wa,StationService st){world=w;wallets=wa;stations=st;}
        public PersonState Hire(string name,string profession,long salaryPerDay)
        {
            WorldUtil.Require(!string.IsNullOrWhiteSpace(name)&&salaryPerDay>=0,"Invalid candidate");
            var p=new PersonState{id="person."+WorldUtil.Id(),name=name,profession=profession,employerId=world.playerCompanyId,
                salaryPerDay=salaryPerDay};world.personnel.Add(p);return p;
        }
        public void AssignStation(string personId,string stationId)
        {
            var p=Get(personId);var st=stations.Get(stationId);
            WorldUtil.Require(st.ownerId==p.employerId,"Only own station assignment supported");
            p.assignedStationId=stationId;p.assignedFleetId=null;
        }
        public PersonState Get(string id)
        {var p=world.personnel.FirstOrDefault(x=>x.id==id);WorldUtil.Require(p!=null,"Person missing");return p;}
        public void PayrollDay()
        {
            foreach(var p in world.personnel)
            {
                if(p.salaryPerDay>0 && wallets.Available(p.employerId)>=p.salaryPerDay)
                    wallets.Debit(p.employerId,p.salaryPerDay); // NPC personal economy out of scope.
                else if(p.salaryPerDay>0)world.events.Add(new EventRecord{at=world.gameSeconds,message="Unpaid payroll: "+p.name});
            }
        }
    }
}
