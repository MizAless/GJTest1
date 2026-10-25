using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class BandolierPreviewAuthoring
{
    const string Art="Assets/_game/Art/Bandolier/";
    public const string ScenePath="Assets/_game/Scenes/BandolierPreview.unity";
    static Font _font;
    [MenuItem("Tools/Prototype/Create Bandolier Preview Scene")]
    public static void CreateScene()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var shader=Shader.Find("Game/Bandolier Curl");if(shader==null)throw new Exception("Bandolier shader missing");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Preview Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";
        camera.transform.position=new Vector3(0,-2.2f,-18);camera.transform.rotation=Quaternion.Euler(-8,0,0);camera.orthographic=true;camera.orthographicSize=4.4f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.045f,.035f);
        camera.nearClipPlane=.1f;camera.farClipPlane=40;
        MakeTable();
        var root=new GameObject("Bandolier — six independent slots");root.transform.position=new Vector3(0,.3f,0);
        var controller=root.AddComponent<BandolierPreview>();
        var presentation=new GameObject("Throw and recoil").transform;presentation.SetParent(root.transform,false);
        var leather=Texture("LeatherBase.png");var loop=Texture("LeatherLoop.png");var bullet=Texture("Cartridge.png");
        var leatherMaterial=Material("Leather",shader,leather);var loopMaterial=Material("Loop",shader,loop);var bulletMaterial=Material("Cartridge",shader,bullet);
        bulletMaterial.SetFloat("_Rigid",1);
        var cartridgeShadowMaterial=Material("CartridgeShadow",shader,bullet);cartridgeShadowMaterial.color=new Color(0,0,0,.52f);
        cartridgeShadowMaterial.SetFloat("_Rigid",1);cartridgeShadowMaterial.SetFloat("_ZWrite",0);cartridgeShadowMaterial.renderQueue=2451;
        var shadowMaterial=Material("Shadow",shader,leather);shadowMaterial.SetFloat("_Shadow",1);shadowMaterial.SetFloat("_ZWrite",0);shadowMaterial.renderQueue=3001;
        var leatherMesh=Mesh("Leather",12.6f,4.3f,0,384,BoundsUV(leather));
        var loopMesh=Mesh("Loop",1.17f,1.85f,-.18f,48,BoundsUV(loop),-.32f);
        var bulletMesh=Mesh("Cartridge",.86f,3.04f,0,1,BoundsUV(bullet));
        var baseRenderer=Renderer(presentation,"Flexible leather base",leatherMesh,leatherMaterial);
        var shadowRenderer=Renderer(presentation,"Roll contact shadow",leatherMesh,shadowMaterial);
        var loops=new MeshRenderer[6];var cartridges=new Transform[6];var cartridgeShadows=new MeshRenderer[6];
        for(int i=0;i<6;i++)
        {
            loops[i] = Renderer(presentation, "Loop " + (i + 1) + " — front leather", loopMesh, loopMaterial);
            loops[i].transform.localScale = new Vector3(1, 0.6425f, 1);
            loops[i].transform.localPosition = new Vector3(0, -0.11f, -0.1f);
            cartridges[i]=Renderer(presentation,"Cartridge "+(i+1)+" — rigid",bulletMesh,bulletMaterial).transform;
            cartridgeShadows[i]=Renderer(cartridges[i],"Cartridge "+(i+1)+" — black shadow copy",bulletMesh,cartridgeShadowMaterial);
        }
        var canvas=new GameObject("Preview controls",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        _font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var title=Label(canvas.transform,"Title",new Vector2(0,-45),new Vector2(1300,50),32);title.text="ПАТРОНТАШ";title.color=new Color(1,.79f,.45f);
        var subtitle=Label(canvas.transform,"Subtitle",new Vector2(0,-96),new Vector2(1350,32),17);subtitle.text="Шесть патронов в кожаных петлях · отдельная сцена просмотра";
        var footer=Panel(canvas.transform,"Control bar",new Vector2(0,52),new Vector2(1400,135),new Vector2(.5f,0),new Color(.055f,.045f,.035f,.96f));
        var replay=Button(footer,"Повторить · пробел",new Vector2(-445,22),new Vector2(260,48));UnityEventTools.AddPersistentListener(replay.onClick,controller.Replay);
        var open=Button(footer,"Раскрыть",new Vector2(-155,22),new Vector2(220,48));UnityEventTools.AddPersistentListener(open.onClick,controller.Open);
        var fold=Button(footer,"Свернуть",new Vector2(95,22),new Vector2(220,48));UnityEventTools.AddPersistentListener(fold.onClick,controller.Fold);
        var status=Label(footer,"Playback state",new Vector2(405,21),new Vector2(330,40),18,new Vector2(.5f,.5f));
        var slider=Slider(footer);UnityEventTools.AddPersistentListener(slider.onValueChanged,controller.Scrub);
        var tip=Label(footer,"Slider hint",new Vector2(0,-44),new Vector2(1300,26),14,new Vector2(.5f,.5f));tip.text="Перетаскивайте ползунок, чтобы рассмотреть любой момент раскатывания";tip.color=new Color(.76f,.69f,.56f);
        controller.Configure(presentation,baseRenderer,shadowRenderer,loops,cartridges,cartridgeShadows,slider,status);
        BandolierAmmoArt.ApplyToController(controller);
        new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        Debug.Log("BANDOLIER_PREVIEW_AUTHORED: "+ScenePath);
    }
    static Texture2D Texture(string filename)
    {
        string path=Art+filename;
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
        importer.isReadable=true;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Rect BoundsUV(Texture2D texture)
    {
        var pixels=texture.GetPixels32();int minX=texture.width,minY=texture.height,maxX=0,maxY=0,opaque=0,clear=0;
        for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
        {
            if(pixels[y*texture.width+x].a>24){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);opaque++;}else clear++;
        }
        if(opaque==0||clear==0)throw new Exception(texture.name+" requires an opaque silhouette and a transparent background");
        minX=Mathf.Max(0,minX-1);minY=Mathf.Max(0,minY-1);maxX=Mathf.Min(texture.width-1,maxX+1);maxY=Mathf.Min(texture.height-1,maxY+1);
        return new Rect((float)minX/texture.width,(float)minY/texture.height,(float)(maxX-minX+1)/texture.width,(float)(maxY-minY+1)/texture.height);
    }
    static Material Material(string name,Shader shader,Texture texture)
    {
        string path=Art+"Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}material.shader=shader;material.mainTexture=texture;material.SetColor("_BackColor",new Color(.78f,.63f,.48f,1));EditorUtility.SetDirty(material);return material;
    }
    static Mesh Mesh(string name,float width,float height,float z,int segments,Rect uv,float yOffset=0)
    {
        string path=Art+name+"Mesh.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool exists=mesh!=null;if(!exists)mesh=new Mesh();mesh.Clear();mesh.name=name+" segmented mesh";
        var vertices=new Vector3[(segments+1)*2];var uvs=new Vector2[vertices.Length];var triangles=new int[segments*6];
        for(int i=0;i<=segments;i++)
        {
            float t=(float)i/segments;
            vertices[i*2]=new Vector3((t-.5f)*width,-height*.5f+yOffset,z);vertices[i*2+1]=new Vector3((t-.5f)*width,height*.5f+yOffset,z);
            uvs[i*2]=new Vector2(uv.x+t*uv.width,uv.y);uvs[i*2+1]=new Vector2(uv.x+t*uv.width,uv.yMax);
            if(i<segments){int a=i*2,j=i*6;triangles[j]=a;triangles[j+1]=a+1;triangles[j+2]=a+2;triangles[j+3]=a+1;triangles[j+4]=a+3;triangles[j+5]=a+2;}
        }
        mesh.vertices=vertices;mesh.uv=uvs;mesh.triangles=triangles;mesh.RecalculateNormals();
        mesh.bounds=new Bounds(Vector3.zero,new Vector3(17,6,6));
        if(!exists)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);return mesh;
    }
    static MeshRenderer Renderer(Transform parent,string name,Mesh mesh,Material material)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
    }
    static void MakeTable()
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_game/Prefabs/PrototypeDungeon.prefab");
        var sprite=source.GetComponentsInChildren<SpriteRenderer>().First(x=>x.name.StartsWith("Floor ")).sprite;
        var root=new GameObject("Preview tabletop — existing wood tiles");root.transform.position=new Vector3(0,0,2);
        for(int y=-6;y<=6;y++)for(int x=-10;x<=10;x++)
        {
            var tile=new GameObject("Wood",typeof(SpriteRenderer));tile.transform.SetParent(root.transform,false);tile.transform.localPosition=new Vector3(x,y,0);
            var renderer=tile.GetComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.color=new Color(.48f,.41f,.33f);renderer.sortingOrder=-10;
        }
    }
    static RectTransform Panel(Transform parent,string name,Vector2 position,Vector2 size,Vector2 anchor,Color color)
    {
        var r=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;r.GetComponent<Image>().color=color;return r;
    }
    static Text Label(Transform parent,string name,Vector2 position,Vector2 size,int fontSize,Vector2? anchor=null)
    {
        var r=new GameObject(name,typeof(RectTransform),typeof(Text)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor??new Vector2(.5f,1);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;
        var text=r.GetComponent<Text>();text.font=_font;text.fontSize=fontSize;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.92f,.86f,.73f);text.raycastTarget=false;return text;
    }
    static Button Button(Transform parent,string caption,Vector2 position,Vector2 size)
    {
        var r=Panel(parent,caption,position,size,new Vector2(.5f,.5f),new Color(.28f,.17f,.09f));var button=r.gameObject.AddComponent<Button>();button.targetGraphic=r.GetComponent<Image>();
        Label(r,"Caption",Vector2.zero,size,19,new Vector2(.5f,.5f)).text=caption;return button;
    }
    static Slider Slider(Transform parent)
    {
        var root=Panel(parent,"Animation timeline",new Vector2(0,-19),new Vector2(1250,22),new Vector2(.5f,.5f),Color.clear);var slider=root.gameObject.AddComponent<Slider>();
        var track=Panel(root,"Track",Vector2.zero,new Vector2(1250,4),new Vector2(.5f,.5f),new Color(.3f,.23f,.15f));track.GetComponent<Image>().raycastTarget=false;
        var handle=Panel(root,"Handle",Vector2.zero,new Vector2(18,24),new Vector2(0,.5f),new Color(1,.73f,.35f));
        slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.minValue=0;slider.maxValue=1;slider.value=1;return slider;
    }
}

