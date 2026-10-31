using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(30000)]
public class PixelUiTheme : MonoBehaviour
{
    private static PixelUiTheme _instance;
    private static Font _font;
    private Canvas[] _canvases;
    private bool _refreshRequested = true;
    private float _nextCanvasScan;
    private readonly List<Text> _texts = new List<Text>();

    public static Font Font
    {
        get
        {
            if (_font == null)
            {
                _font = Resources.Load<Font>("Fonts/m6x11cyrillic");
            }
            return _font;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _font = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (_instance != null)
        {
            return;
        }
        _instance = new GameObject("Pixel UI theme").AddComponent<PixelUiTheme>();
        DontDestroyOnLoad(_instance.gameObject);
    }

    public static void RequestRefresh()
    {
        if (_instance != null)
        {
            _instance._refreshRequested = true;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        UnityEngine.Font.textureRebuilt += OnFontTextureRebuilt;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.Font.textureRebuilt -= OnFontTextureRebuilt;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _nextCanvasScan = 0;
        _refreshRequested = true;
    }

    private static void OnFontTextureRebuilt(Font font)
    {
        if (font == Font && font.material != null && font.material.mainTexture != null)
        {
            font.material.mainTexture.filterMode = FilterMode.Point;
        }
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= _nextCanvasScan)
        {
            _nextCanvasScan = Time.unscaledTime + 1;
            _canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _refreshRequested = true;
        }
        if (!_refreshRequested || Font == null)
        {
            return;
        }
        _refreshRequested = false;
        foreach (Canvas canvas in _canvases)
        {
            if (canvas == null)
            {
                continue;
            }
            if (canvas.GetComponent<PixelCanvasWatcher>() == null)
            {
                canvas.gameObject.AddComponent<PixelCanvasWatcher>();
            }
            MarkUpgradePanel(canvas.transform.Find("Reward screen"));
            MarkUpgradePanel(canvas.transform.Find("Bandolier upgrades"));
            _texts.Clear();
            canvas.GetComponentsInChildren(true, _texts);
            foreach (Text text in _texts)
            {
                if (text.font != Font)
                {
                    text.font = Font;
                }
                if (text.GetComponentInParent<UpgradeTextMotionScope>(true) != null
                    && text.GetComponent<WavyUpgradeText>() == null)
                {
                    text.gameObject.AddComponent<WavyUpgradeText>();
                }
            }
        }
    }

    private static void MarkUpgradePanel(Transform panel)
    {
        if (panel != null && panel.GetComponent<UpgradeTextMotionScope>() == null)
        {
            panel.gameObject.AddComponent<UpgradeTextMotionScope>();
        }
    }
}
