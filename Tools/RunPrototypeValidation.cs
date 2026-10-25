using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RunPrototypeValidation
{
    static double _start;
    static bool _playing, _rules, _loadout, _boss, _big;
    static int _rewards;
    static double _heartbeat;
    static readonly HashSet<string> _packs = new HashSet<string>();
    static readonly List<string> _errors = new List<string>();
    static void Capture(string name)
    {
        var canvas=GameObject.Find("Run interface").GetComponent<Canvas>();
        var camera=Camera.main; var target=new RenderTexture(1280,720,24);
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
        Directory.CreateDirectory("Artifacts");File.WriteAllBytes("Artifacts/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        Object.Destroy(image);Object.Destroy(target);
    }
    public static void Run()
    {
        try
        {
            AuthorHitVFX();
            EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            _start=EditorApplication.timeSinceStartup;
            Application.logMessageReceived+=OnLog;
            EditorApplication.playModeStateChanged+=state=>{Debug.Log("RUN_CHECK_STATE: "+state);if(state==PlayModeStateChange.EnteredPlayMode)_playing=true;};
            EditorApplication.update+=Tick;
            EditorApplication.isPlaying=true;
        }
        catch(Exception e) {Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void OnLog(string message,string trace,LogType type) {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)_errors.Add(message);}
    static void AuthorHitVFX()
    {
        const string path="Assets/_game/Prefabs/HitVFX.prefab";
        var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_game/Prefabs/OnDeadVFX.prefab");
        var root=Object.Instantiate(original);root.name="HitVFX";
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_game/Art/Materials/ExplosionCore.mat");
        foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.stopAction=ParticleSystemStopAction.None;main.duration=.25f;main.loop=false;main.playOnAwake=false;
            var sub=ps.subEmitters;sub.enabled=false;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.08f,.18f);main.startSize=new ParticleSystem.MinMaxCurve(.05f,.12f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(1f,3f);main.startColor=Color.white;main.maxParticles=12;
            var emission=ps.emission;emission.enabled=true;emission.rateOverTime=0;emission.rateOverDistance=0;emission.SetBursts(new[] {new ParticleSystem.Burst(0,6)});
            var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.06f;
            var color=ps.colorOverLifetime;color.enabled=false;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=60;
        }
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);
        var enemy=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_game/Prefabs/Enemy.prefab");
        var so=new SerializedObject(enemy.GetComponent<Enemy>());so.FindProperty("_hitVFX").objectReferenceValue=prefab.GetComponent<ParticleSystem>();so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(enemy);AssetDatabase.SaveAssets();
    }
    static void Assert(bool condition,string message) {if(!condition)throw new Exception(message);}
    static void Rules()
    {
        Assert(GameScript.Round(1).Length==1&&GameScript.Round(1)[0].Length==6,"Round 1 composition");
        Assert(GameScript.Round(2).Length==3&&GameScript.Round(2).All(x=>x.Length==4)&&GameScript.Round(2).Count(x=>x.Contains(80))==2,"Round 2 composition");
        Assert(GameScript.Round(3).Length==5&&GameScript.Round(3)[4].Contains(200),"Boss composition");
        var profile=new CombatProfile();profile.Apply(new AmmoProperties { More=50 });profile.Apply(new AmmoProperties { More=50 });Assert(Mathf.Approximately(profile.Damage,22.5f),"Multipliers must multiply");
        var go=new GameObject("Rules drum");var drum=go.AddComponent<BulletContainer>();drum.Init();
        for(int i=0;i<6;i++){drum.Cartridges[i]=AmmoItem.Ordinary();drum.Sockets[i]=null;}
        drum.Cartridges[0]=new AmmoItem{Properties=new AmmoProperties{More=50},Trigger=AmmoTrigger.ReloadNext};drum.Sockets[1]=new AmmoItem{Properties=new AmmoProperties{More=50}};drum.Rebuild();Assert(Mathf.Approximately(drum.Profiles[1].Damage,22.5f),"Reload transfer");drum.Rebuild();Assert(Mathf.Approximately(drum.Profiles[1].Damage,22.5f),"Reload must not accumulate");Object.Destroy(go);
        var enemyPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_game/Prefabs/Enemy.prefab");var enemy=Object.Instantiate(enemyPrefab).GetComponent<Enemy>();enemy.Configure(80);
        var burn=new CombatProfile();burn.Apply(new AmmoProperties{Burn=1});for(int i=0;i<3;i++)enemy.ReceiveHit(burn,Vector3.zero);G.Enemies.Add(enemy);enemy.TickStatuses();Assert(Mathf.Approximately(enemy.Health,29),"Burst status aggregation");Assert(enemy.Count("огонь")==3,"Burn charges");Object.Destroy(enemy.gameObject);
        enemy=Object.Instantiate(enemyPrefab).GetComponent<Enemy>();enemy.Configure(80);var overload=new CombatProfile();overload.Apply(new AmmoProperties{Puncture=2,Overload=true});enemy.ReceiveHit(overload,Vector3.zero);Assert(Mathf.Approximately(enemy.Health,22),"Puncture overload");Object.Destroy(enemy.gameObject);
        var rarities=new HashSet<BulletRarity>();for(int i=0;i<2000;i++)rarities.Add(AmmoGenerator.Generate(true).Rarity);Assert(rarities.Count==4,"All rarity tiers generated");
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-_start>150)throw new Exception("Play-mode run timed out");
            if(EditorApplication.timeSinceStartup-_heartbeat>5)
            {
                _heartbeat=EditorApplication.timeSinceStartup;var current=RunDirector.Instance;
                Debug.Log("RUN_CHECK_PROGRESS: playing="+_playing+" rewards="+_rewards+" packs="+_packs.Count+(current==null||current.Shooter==null?" no instance":" round="+current.Round+" pack="+current.Pack+" active="+current.CombatActive+" scale="+Time.timeScale+" firing="+current.Shooter.IsFiring+" reload="+current.Drum.IsReloading+" ammo="+current.Drum.BulletsRemaining+" enemies="+string.Join(",",G.Enemies.Select(e=>e==null?"NULL":e.Health.ToString("0.#")))));
                if(current!=null&&current.CombatActive)Capture("run-progress");
            }
            if(!_playing)return;
            if(_errors.Count>0)throw new Exception(string.Join("\n",_errors));
            var run=RunDirector.Instance;if(run==null||run.Shooter==null||RunUI.Instance==null)return;
            if(!_rules){Rules();_rules=true;}
            if(run.AwaitingReward)
            {
                if(_rewards==0)Capture("run-start-reward");
                if(_rewards==1)Capture("run-bullet-reward");
                var item=run.Offers[0];bool module=run.ChoosingModule;run.Choose(0,module?1:0);
                Assert(ReferenceEquals(item,module?run.Drum.Sockets[1]:run.Drum.Cartridges[0]),"Reward installed into selected socket");
                _rewards++;_loadout=false;return;
            }
            if(run.Victory)
            {
                Capture("run-victory");
                Assert(_rewards==5,"Expected start module plus two rewards per round break");Assert(_packs.Count==9,"Expected all nine packs");Assert(_boss&&_big,"Boss and large enemies appeared");
                Assert(run.Shooter.CompletedShots>0,"Real projectiles were fired");
                Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/run-validation.txt","PASS: health, multipliers, reload, states, rarity, rewards; 3 rounds / 9 packs; large 80 HP; boss 200 HP; shots "+run.Shooter.CompletedShots);
                Debug.Log("RUN_PROTOTYPE_VALIDATED: 3 rounds, 9 packs, 5 rewards; shots "+run.Shooter.CompletedShots);
                EditorApplication.update-=Tick;EditorApplication.Exit(0);return;
            }
            if(!run.CombatActive)return;
            if(!_loadout)
            {
                for(int i=0;i<6;i++)run.Drum.Cartridges[i]=new AmmoItem{Name="Validation hybrid",Properties=new AmmoProperties{Ricochet=2,Burn=1,Poison=1,Chill=1,Puncture=1,Overload=true,More=100,Burst=1,Split=i==2?1:0}};
                run.Drum.Rebuild();_loadout=true;
            }
            Time.timeScale=8;
            _packs.Add(run.Round+":"+run.Pack);
            foreach(var e in G.Enemies){if(e.MaxHealth==200){if(!_boss)Capture("run-boss");_boss=true;}if(e.MaxHealth==80)_big=true;}
            if(G.Enemies.Count>0)run.Shooter.TryFire();
        }
        catch(Exception e){EditorApplication.update-=Tick;Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
