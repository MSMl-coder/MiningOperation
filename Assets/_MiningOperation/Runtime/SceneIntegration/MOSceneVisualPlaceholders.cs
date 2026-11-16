using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiningOperation
{
    // Temporary presentation ONLY. The shipment ledger in WorldRuntime is authoritative.
    // Existing NPC station art and asteroid meshes are never touched.
    [DisallowMultipleComponent]
    public sealed class MOSceneVisualPlaceholders : MonoBehaviour
    {
        private MOSceneCoordinator coordinator;
        private readonly Dictionary<string, GameObject> transports = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> ownedBases = new Dictionary<string, GameObject>();
        private float nextUpdate;
        private WorldRuntime cachedGame;
        private void Awake() { coordinator = GetComponent<MOSceneCoordinator>(); }
        private void Update()
        {
            if (coordinator == null || coordinator.Game == null) return;
            if (!ReferenceEquals(cachedGame,coordinator.Game))
            {
                ClearAll();cachedGame=coordinator.Game;
            }
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + .2f;
            UpdateOwned();UpdateTransports();
        }
        private static GameObject Shape(string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color)
        {
            var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.position=position;
            o.transform.localScale=scale;
            var r=o.GetComponent<Renderer>();if(r!=null)r.material.color=color;
            var c=o.GetComponent<Collider>();if(c!=null)Destroy(c);
            return o;
        }
        private void UpdateOwned()
        {
            foreach(var st in coordinator.Game.State.stations.Where(x=>x.ownerId=="player"&&x.sceneId==coordinator.SceneKey))
            {
                if(!ownedBases.TryGetValue(st.id,out var obj) || obj==null)
                {
                    obj=Shape("MO Owned Base [PLACEHOLDER] " + st.displayName,PrimitiveType.Cube,
                        new Vector3((float)st.x,(float)st.y,(float)st.z),new Vector3(24,12,30),new Color(.91f,.34f,.11f));
                    ownedBases[st.id]=obj;
                }
                obj.transform.position=new Vector3((float)st.x,(float)st.y,(float)st.z);
            }
        }
        private void UpdateTransports()
        {
            var world=coordinator.Game.State;
            var visible = world.shipments.Where(s=>
            {
                if(s.status=="Delivered" || s.status=="BlockedPickup")return false;
                var from=world.stations.FirstOrDefault(x=>x.id==s.fromStationId);
                var to=world.stations.FirstOrDefault(x=>x.id==s.toStationId);
                bool outbound=from!=null&&from.sceneId==coordinator.SceneKey && s.status=="Scheduled"&&world.gameSeconds>=s.pickupAt-16;
                bool inbound=to!=null&&to.sceneId==coordinator.SceneKey && s.status=="InTransit"&&world.gameSeconds>=s.deliveryAt-16;
                return outbound || inbound || (s.status=="AwaitingStorage" && to!=null && to.sceneId==coordinator.SceneKey);
            }).Take(12).ToList();
            var ids=new HashSet<string>(visible.Select(x=>x.id));
            foreach(var key in transports.Keys.ToList())
            {
                if(ids.Contains(key))continue;
                if(transports[key]!=null)Destroy(transports[key]);transports.Remove(key);
            }
            foreach(var s in visible)
            {
                var inbound=s.status!="Scheduled";
                var id=inbound?s.toStationId:s.fromStationId;
                var st=world.stations.First(x=>x.id==id);
                var stationPos=new Vector3((float)st.x,(float)st.y,(float)st.z);
                float remaining=(float)Math.Max(0,(inbound?s.deliveryAt:s.pickupAt)-world.gameSeconds);
                var t=Mathf.Clamp01(remaining/16f);
                var offset = new Vector3(60*t+18,8,-100*t+22);
                var position=stationPos+offset;
                if(!transports.TryGetValue(s.id,out var obj) || obj==null)
                {
                    obj=Shape("MO NPC Cargo Transport [PLACEHOLDER] "+s.id.Substring(0,Math.Min(12,s.id.Length)),
                        PrimitiveType.Capsule,position,new Vector3(4,5,9),new Color(.92f,.38f,.12f));
                    transports[s.id]=obj;
                }
                obj.transform.position=position;
            }
        }
        private void ClearAll()
        {
            foreach(var go in transports.Values)if(go!=null)Destroy(go);
            foreach(var go in ownedBases.Values)if(go!=null)Destroy(go);
            transports.Clear();ownedBases.Clear();
        }
        private void OnDestroy() {ClearAll();}
    }
}
