using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MiningOperation
{
    // Optional, replaceable 3D adapter. Game truth stays exclusively in WorldState.
    public sealed class PrototypeWorldView : MonoBehaviour
    {
        private WorldRuntime game;
        private Transform shipVisual;
        private readonly Dictionary<string,Transform> stationVisuals=new Dictionary<string,Transform>();
        private readonly Dictionary<string,Transform> transportVisuals=new Dictionary<string,Transform>();
        private readonly List<Transform> asteroidVisuals=new List<Transform>();
        private Camera viewCamera;
        public GameObject stationPrefab;
        public GameObject shipPrefab;
        public void Initialize(WorldRuntime runtime)
        {
            ClearAll();game=runtime;
            foreach(var st in game.State.stations)CreateStation(st);
            foreach(var a in game.State.asteroids)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name=a.displayName;
                go.transform.SetParent(transform,false);
                go.transform.position=new Vector3((float)a.x,(float)a.y,(float)a.z);
                go.transform.localScale=new Vector3(16,12,14);
                SetColor(go,new Color(0.43f,0.42f,0.38f));asteroidVisuals.Add(go.transform);
            }
            CreateShip();CreateCamera();
        }
        private void ClearAll()
        {
            for(int i=transform.childCount-1;i>=0;i--)
            {
                var child=transform.GetChild(i);
                if(child.GetComponent<Camera>()!=null || child.GetComponent<Light>()!=null)continue;
                Destroy(child.gameObject);
            }
            stationVisuals.Clear();transportVisuals.Clear();asteroidVisuals.Clear();shipVisual=null;
        }
        private static void SetColor(GameObject go,Color color)
        {
            var renderer=go.GetComponent<Renderer>();
            if(renderer!=null)
            {
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                if(shader==null)shader=Shader.Find("Standard");
                if(shader!=null){var mat=new Material(shader);mat.color=color;renderer.sharedMaterial=mat;}
            }
        }
        private void CreateStation(StationState s)
        {
            var root=new GameObject(s.displayName);root.transform.SetParent(transform,false);
            root.transform.position=new Vector3((float)s.x,(float)s.y,(float)s.z);
            if(stationPrefab!=null)Instantiate(stationPrefab,root.transform);
            else
            {
                var hull=GameObject.CreatePrimitive(PrimitiveType.Cube);
                hull.transform.SetParent(root.transform,false);hull.transform.localPosition=Vector3.zero;
                hull.transform.localScale=new Vector3(22,24,18);
                SetColor(hull,new Color(0.92f,0.89f,0.82f));
                var ring=GameObject.CreatePrimitive(PrimitiveType.Cube);
                ring.transform.SetParent(root.transform,false);ring.transform.localPosition=new Vector3(0,-9,0);
                ring.transform.localScale=new Vector3(25,6,21);SetColor(ring,new Color(0.99f,0.30f,0.07f));
                var antenna=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                antenna.transform.SetParent(root.transform,false);antenna.transform.localPosition=new Vector3(0,19,0);
                antenna.transform.localScale=new Vector3(0.28f,8,0.28f);SetColor(antenna,Color.white);
            }
            stationVisuals[s.id]=root.transform;
        }
        private void CreateShip()
        {
            var s=game.Ships.Get(game.State.controlledShipId);
            var go=new GameObject("Player Ship / Pioneer");go.transform.SetParent(transform,false);
            if(shipPrefab!=null)Instantiate(shipPrefab,go.transform);
            else
            {
                var hull=GameObject.CreatePrimitive(PrimitiveType.Cube);hull.transform.SetParent(go.transform,false);
                hull.transform.localScale=new Vector3(5,3,13);SetColor(hull,new Color(0.20f,0.36f,0.47f));
                var nose=GameObject.CreatePrimitive(PrimitiveType.Cube);nose.transform.SetParent(go.transform,false);
                nose.transform.localPosition=new Vector3(0,0,6);nose.transform.localScale=new Vector3(3,2,6);
                SetColor(nose,new Color(0.83f,0.73f,0.42f));
                for(int i=-1;i<=1;i+=2)
                {
                    var engine=GameObject.CreatePrimitive(PrimitiveType.Cylinder);engine.transform.SetParent(go.transform,false);
                    engine.transform.localPosition=new Vector3(i*2.9f,-1,-5);
                    engine.transform.localRotation=Quaternion.Euler(90,0,0);
                    engine.transform.localScale=new Vector3(1.3f,3,1.3f);
                    SetColor(engine,new Color(0.38f,0.40f,0.43f));
                }
            }
            shipVisual=go.transform;shipVisual.position=new Vector3((float)s.x,(float)s.y,(float)s.z);
        }
        private void CreateCamera()
        {
            viewCamera=Camera.main;
            if(viewCamera==null)
            {
                var cam=new GameObject("Prototype Camera").AddComponent<Camera>();
                cam.tag="MainCamera";cam.transform.SetParent(transform,false);
                viewCamera=cam;cam.clearFlags=CameraClearFlags.SolidColor;
                cam.backgroundColor=new Color(0.014f,0.036f,0.07f);cam.fieldOfView=52;
                cam.gameObject.AddComponent<AudioListener>();
            }
            if(FindObjectsByType<Light>(FindObjectsSortMode.None).Length==0)
            {
                var light=new GameObject("Prototype Light").AddComponent<Light>();
                light.type=LightType.Directional;light.intensity=1.2f;
                light.transform.rotation=Quaternion.Euler(40,-35,0);light.transform.SetParent(transform,false);
            }
        }
        private void Update()
        {
            if(game==null||shipVisual==null)return;
            var s=game.Ships.Get(game.State.controlledShipId);
            if(string.IsNullOrEmpty(s.dockedAtStationId)&&string.IsNullOrEmpty(s.dockingTargetId))
            {
                Vector3 movement=Vector3.zero;
#if ENABLE_INPUT_SYSTEM
                var kb=Keyboard.current;
                if(kb!=null)
                {
                    if(kb.wKey.isPressed)movement.z+=1;
                    if(kb.sKey.isPressed)movement.z-=1;
                    if(kb.aKey.isPressed)movement.x-=1;
                    if(kb.dKey.isPressed)movement.x+=1;
                    if(kb.qKey.isPressed)movement.y-=1;
                    if(kb.eKey.isPressed)movement.y+=1;
                }
#else
                if(Input.GetKey(KeyCode.W))movement.z+=1;
                if(Input.GetKey(KeyCode.S))movement.z-=1;
                if(Input.GetKey(KeyCode.A))movement.x-=1;
                if(Input.GetKey(KeyCode.D))movement.x+=1;
                if(Input.GetKey(KeyCode.Q))movement.y-=1;
                if(Input.GetKey(KeyCode.E))movement.y+=1;
#endif
                movement=movement.normalized*Mathf.Min(Time.deltaTime,0.1f)*45f;
                s.x+=movement.x;s.y+=movement.y;s.z+=movement.z;
            }
            Vector3 pose=new Vector3((float)s.x,(float)s.y,(float)s.z);
            if(!string.IsNullOrEmpty(s.dockedAtStationId))
            {
                var st=game.Stations.Get(s.dockedAtStationId);
                pose=new Vector3((float)st.x+27,(float)st.y,(float)st.z+16);
            }
            if(!string.IsNullOrEmpty(s.dockingTargetId))
            {
                var target=game.Stations.Get(s.dockingTargetId);
                float left=(float)Math.Max(0,s.dockingCompleteAt-game.State.gameSeconds);
                float t=1f/(1+left);
                pose=Vector3.Lerp(pose,new Vector3((float)target.x,(float)target.y,(float)target.z),t);
            }
            shipVisual.position=pose;
            if(viewCamera!=null)
            {
                Vector3 desired=shipVisual.position+new Vector3(30,75,-94);
                viewCamera.transform.position=Vector3.Lerp(viewCamera.transform.position,desired,Time.deltaTime*2f);
                viewCamera.transform.LookAt(shipVisual.position);
            }
            UpdateStations();UpdateTransports();
        }
        private void UpdateStations()
        {
            foreach(var st in game.State.stations)
            {
                if(!stationVisuals.TryGetValue(st.id,out var t))CreateStation(st);
                else t.position=new Vector3((float)st.x,(float)st.y,(float)st.z);
            }
        }
        private void UpdateTransports()
        {
            foreach(var item in game.State.shipments.Where(x=>x.status=="InTransit" ||
                (x.status=="Scheduled" && game.State.gameSeconds>=x.pickupAt-12) ||
                x.status=="AwaitingStorage").Take(10))
            {
                if(!transportVisuals.TryGetValue(item.id,out var t))
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.name="NPC Cargo Vessel "+item.id.Substring(0,Math.Min(10,item.id.Length));
                    go.transform.SetParent(transform,false);go.transform.localScale=new Vector3(4,6,4);
                    SetColor(go,new Color(0.97f,0.38f,0.12f));
                    transportVisuals[item.id]=go.transform;t=go.transform;
                }
                var from=game.Stations.Get(item.fromStationId);var to=game.Stations.Get(item.toStationId);
                var start=new Vector3((float)from.x,0,(float)from.z);
                var finish=new Vector3((float)to.x,0,(float)to.z);
                if(item.status=="Scheduled")
                {
                    var approaching=(float)Math.Max(0,Math.Min(1,(game.State.gameSeconds-(item.pickupAt-12))/12));
                    t.position=Vector3.Lerp(start+new Vector3(0,5,-85),start,approaching);
                }
                else
                {
                    var ratio=(float)Math.Max(0,Math.Min(1,(game.State.gameSeconds-item.pickupAt)/Math.Max(1,item.deliveryAt-item.pickupAt)));
                    t.position=Vector3.Lerp(start,finish,ratio);
                }
            }
            foreach(var entry in transportVisuals.ToArray())
            {
                if(!game.State.shipments.Any(x=>x.id==entry.Key && (x.status=="InTransit" ||
                    x.status=="AwaitingStorage" || (x.status=="Scheduled" && game.State.gameSeconds>=x.pickupAt-12))))
                {Destroy(entry.Value.gameObject);transportVisuals.Remove(entry.Key);}
            }
        }
    }
}
