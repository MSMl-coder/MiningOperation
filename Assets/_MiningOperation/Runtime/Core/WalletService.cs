using System;
using System.Linq;

namespace MiningOperation
{
    public sealed class WalletService
    {
        private readonly WorldState world;
        public WalletService(WorldState w){world=w;}
        public WalletState Get(string owner)
        { var w=world.wallets.FirstOrDefault(x=>x.ownerId==owner); WorldUtil.Require(w!=null,"Wallet missing: "+owner); return w; }
        public long Available(string owner) { var w=Get(owner); return w.balance-w.held; }
        public void Debit(string owner,long credits)
        { WorldUtil.Require(credits>=0&&Available(owner)>=credits,"Insufficient credits"); Get(owner).balance-=credits; }
        public void Credit(string owner,long credits)
        { WorldUtil.Require(credits>=0,"Negative credits"); checked{Get(owner).balance+=credits;} }
        public void Pay(string from,string to,long amount)
        {
            WorldUtil.Require(amount>=0,"Negative payment");
            WorldUtil.Require(Available(from)>=amount,"Insufficient funds");
            Debit(from,amount); Credit(to,amount);
        }
        public void Hold(string owner,long amount)
        { WorldUtil.Require(amount>=0 && Available(owner)>=amount,"Insufficient funds for hold");checked{Get(owner).held+=amount;} }
        public void Release(string owner,long amount)
        { WorldUtil.Require(amount>=0 && Get(owner).held>=amount,"Invalid funds release");Get(owner).held-=amount; }
        public void PayHeld(string owner,string recipient,long amount)
        {
            WorldUtil.Require(amount>=0&&Get(owner).held>=amount,"Insufficient held credits");
            var payer=Get(owner);
            WorldUtil.Require(payer.balance>=amount,"Balance below held amount");
            payer.held-=amount; payer.balance-=amount; Credit(recipient,amount);
        }
        public void Validate()
        {
            foreach(var w in world.wallets)WorldUtil.Require(w.balance>=0 && w.held>=0 && w.held<=w.balance,"Invalid wallet: "+w.ownerId);
        }
    }
}
