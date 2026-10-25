using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class BandolierPreviewValidation
{
    static double _stageTime;
    static int _stage;
    public static void Run()
    {
        try
        {
            BandolierPreviewAuthoring.CreateScene();
            var controller=Object.FindAnyObjectByType<BandolierPreview>();
            if(controller==null||controller.CartridgeCount!=6||controller.CartridgeShadowCount!=6)throw new Exception("Expected six independent cartridges and six shadow copies");
            if(Mathf.Abs(BandolierPreview.Opening(.2f)-.68f)>.001f||BandolierPreview.Opening(.4f)<.999f)throw new Exception("Throw must open sharply, leaving time to settle");
            if(Object.FindAnyObjectByType<RunDirector>()!=null||Object.FindAnyObjectByType<Shooter>()!=null)throw new Exception("Preview must be isolated from the game");
            var shader=Shader.Find("Game/Bandolier Curl");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Curl shader failed to compile");
            var slider=Object.FindAnyObjectByType<Slider>();
            if(slider.onValueChanged.GetPersistentEventCount()!=1)throw new Exception("Timeline listener missing");
            slider.onValueChanged.SetPersistentListenerState(0,UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
            slider.value=.42f;
            if(Mathf.Abs(controller.Timeline-.42f)>.001f)throw new Exception("Timeline listener is not connected");
            slider.onValueChanged.SetPersistentListenerState(0,UnityEngine.Events.UnityEventCallState.RuntimeOnly);
            Directory.CreateDirectory("Artifacts/BandolierThrowFrames");
            for(int i=0;i<=96;i++)
            {
                controller.Scrub(Mathf.Clamp01(i/81f));
                foreach(var filter in controller.GetComponentsInChildren<MeshFilter>())
                    if(filter.name.EndsWith("— rigid")&&(filter.sharedMesh.vertexCount!=4||filter.transform.localScale!=Vector3.one))throw new Exception("Cartridges must remain rigid, unscaled quads");
                Capture("Artifacts/BandolierThrowFrames/frame_"+i.ToString("D3")+".png");
            }
            foreach(float p in new[]{0,.12f,.2f,.3f,.4f,.55f,.75f,1f})
            {
                controller.Scrub(p);var point=controller.Surface(2.55f,-.065f,out float angle);
                if(float.IsNaN(point.x)||float.IsNaN(point.z)||float.IsNaN(angle))throw new Exception("Invalid curl mapping");
                Capture("Artifacts/bandolier-"+Mathf.RoundToInt(p*100)+".png");
            }
            controller.Scrub(1);
            var flat=controller.Surface(2.55f,-.065f,out float a);
            if(Mathf.Abs(flat.x-2.55f)>.001f||Mathf.Abs(flat.z+.065f)>.001f||Mathf.Abs(a)>.001f)throw new Exception("Flat cartridge pose does not match leather");
            File.WriteAllText("Artifacts/bandolier-validation.txt","PASS: isolated scene; 6 rigid unscaled four-vertex cartridges; 6 black shadow copies; sharp opening curve; shader compiled; scrub connected; finite curl; flat final pose; 97 rendered throw/settling frames.");
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode){_stageTime=EditorApplication.timeSinceStartup;_stage=0;EditorApplication.update+=CheckPlayback;}};
            EditorApplication.isPlaying=true;
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void CheckPlayback()
    {
        try
        {
            var c=Object.FindAnyObjectByType<BandolierPreview>();if(c==null)return;c.SetDuration(.4f);
            double t=EditorApplication.timeSinceStartup-_stageTime;
            if(_stage==0&&t>1.2){if(c.Progress<.99f)throw new Exception("Autoplay did not finish");c.Fold();_stage=1;_stageTime=EditorApplication.timeSinceStartup;}
            else if(_stage==1&&t>.7){if(c.Progress>.01f)throw new Exception("Fold did not finish");c.Open();_stage=2;_stageTime=EditorApplication.timeSinceStartup;}
            else if(_stage==2&&t>.17){if(c.Timeline<=0||c.Timeline>=1)throw new Exception("Open must interpolate");c.Scrub(.37f);_stage=3;_stageTime=EditorApplication.timeSinceStartup;}
            else if(_stage==3&&t>.3){if(Mathf.Abs(c.Timeline-.37f)>.001f)throw new Exception("Scrubbing must pause playback");c.Replay();_stage=4;_stageTime=EditorApplication.timeSinceStartup;}
            else if(_stage==4&&t>1){if(c.Progress<.99f)throw new Exception("Replay failed");File.AppendAllText("Artifacts/bandolier-validation.txt"," Play Mode: autoplay, fold, open, scrub pause and replay passed.");Debug.Log("BANDOLIER_PREVIEW_VALIDATED");EditorApplication.update-=CheckPlayback;EditorApplication.Exit(0);}
            if(t>12)throw new Exception("Playback validation timed out");
        }
        catch(Exception e){EditorApplication.update-=CheckPlayback;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Capture(string path)
    {
        var camera=Camera.main;var canvas=Object.FindAnyObjectByType<Canvas>();
        var target=new RenderTexture(1600,900,24);
        camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        Object.DestroyImmediate(image);Object.DestroyImmediate(target);
    }
}
