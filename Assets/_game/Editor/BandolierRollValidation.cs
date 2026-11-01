using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BandolierRollValidation
{
    private static readonly float[] Times = { 0, 0.025f, 0.05f, 0.075f, 0.1f, 0.14f, 0.18f, 0.23f, 0.28f, 0.34f, 0.4f, 0.65f, 1 };

    public static void Run()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Shader shader = Shader.Find("Game/Bandolier Curl");
            Require(shader != null && !ShaderUtil.ShaderHasError(shader), "Shader compilation failed");
            UpgradePresentationAssets assets = Resources.Load<UpgradePresentationAssets>("Upgrades/PresentationAssets");
            GameObject root = UnityEngine.Object.Instantiate(assets.BandolierPrefab);
            BandolierPreview controller = root.GetComponent<BandolierPreview>();
            Camera camera = new GameObject("Roll validation camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -20);
            camera.orthographic = true;
            camera.orthographicSize = 4.3f;
            camera.aspect = 16f / 9;
            camera.backgroundColor = new Color(0.055f, 0.035f, 0.025f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var settings = new SerializedObject(controller);
            MeshRenderer leather = (MeshRenderer)settings.FindProperty("_base").objectReferenceValue;
            SerializedProperty loops = settings.FindProperty("_loops");
            Mesh mesh = assets.CreateModuleSurfaceMesh();
            Require(mesh.vertexCount == 66 && assets.ClipMesh.vertexCount == 4, "Attachment subdivision modified the shared offer mesh");
            MeshRenderer[] modules = new MeshRenderer[6];
            for (int index = 0; index < modules.Length; index++)
            {
                modules[index] = assets.CreateClip(controller.GetCartridge(index).parent, "Module " + index, mesh);
            }
            string folder = ".utmp/Artifacts/roll-fix-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(folder);
            foreach (float time in Times)
            {
                controller.Scrub(time);
                for (int index = 0; index < modules.Length; index++)
                {
                    controller.UpdateModuleAttachment(modules[index], index);
                    var properties = new MaterialPropertyBlock();
                    modules[index].GetPropertyBlock(properties);
                    Require(Mathf.Approximately(properties.GetFloat("_Progress"), controller.Progress), "Module and leather progress differ");
                    Require(Mathf.Approximately(properties.GetFloat("_Gesture"), controller.Timeline), "Module impact time differs");
                    Require(properties.GetFloat("_Rigid") == 0 && properties.GetFloat("_InteriorOnly") == 1, "Module did not follow the interior surface");
                    MeshRenderer loop = (MeshRenderer)loops.GetArrayElementAtIndex(index).objectReferenceValue;
                    float nativeDepth = loop.GetComponent<MeshFilter>().sharedMesh.vertices[0].z * loop.transform.localScale.z;
                    Require(Mathf.Abs(properties.GetFloat("_SurfaceOffset") - nativeDepth + 0.025f) < 0.00001f, "Unstable module clearance");
                    Require(modules[index].transform.localPosition == loop.transform.localPosition, "Module origin detached from loop");
                    Require(Mathf.Approximately(properties.GetFloat("_ImpactYScale"), loop.transform.localScale.y), "Module vertical movement differs");
                }
                Render(camera, folder + "/timeline-" + time.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + ".png");
            }
            controller.Scrub(0);
            foreach (MeshRenderer module in modules)
            {
                controller.UpdateModuleAttachment(module, Array.IndexOf(modules, module));
            }
            Color32[] plain = Render(camera, folder + "/exterior-plain.png");
            var block = new MaterialPropertyBlock();
            leather.GetPropertyBlock(block);
            Require(block.GetFloat("_PlainBack") == 1, "Leather exterior is not enabled");
            block.SetFloat("_PlainBack", 0);
            leather.SetPropertyBlock(block);
            Color32[] original = Render(camera, folder + "/exterior-old-texture.png");
            int changed = plain.Where((color, index) => color.r != original[index].r || color.g != original[index].g || color.b != original[index].b).Count();
            Require(changed > 1000, "Exterior is not visible from the upgrade camera");
            Debug.Log("BANDOLIER_ROLL_VALIDATION_PASSED: plain exterior visible (" + changed + " pixels), 13 animation positions, 6 subdivided modules with stable surface clearance");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static Color32[] Render(Camera camera, string path)
    {
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        Color32[] pixels = image.GetPixels32();
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        target.Release();
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
        return pixels;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
