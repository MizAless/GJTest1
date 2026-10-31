using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// A real uGUI hierarchy, available in the inspector during Play Mode.
public class RunUI : MonoBehaviour
{
    public static RunUI Instance { get; private set; }
    RunDirector _run;
    Canvas _canvas;
    Font _font;
    RectTransform _root, _modal;
    Text _round, _note, _detailText;
    GameObject _detail;
    private BandolierUpgradeScreen _upgradeScreen;
    public BandolierUpgradeScreen UpgradeScreen => _upgradeScreen;
    readonly List<Button> _hud = new List<Button>();
    readonly Dictionary<Enemy,Text> _labels = new Dictionary<Enemy,Text>();
    int _detailSlot=-1;
    float[] _pulses=new float[6];
    static Color Panel => new Color(.07f,.1f,.15f,.96f);
    static Color Gold => new Color(1,.74f,.38f);
    public void Initialize(RunDirector run)
    {
        Instance=this; _run=run; _font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go=new GameObject("Run interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        _canvas=go.GetComponent<Canvas>(); _canvas.renderMode=RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder=100;
        _root=go.GetComponent<RectTransform>();
        var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=.5f;
        var top=PanelRect(_root,"Round status",20,16,1240,60,Panel);
        _round=Label(top,"Round",16,6,800,26,20); _note=Label(top,"Feedback",16,32,1200,22,14);
        for (int i = 0; i < 6; i++)
        {
            int index = i;
            Button button = MakeButton(_root, "Socket " + (i + 1), 24 + i * 206, 618, 196, 84, () => Inspect(index));
            button.gameObject.AddComponent<TooltipTrigger>().Bind(() => SlotTooltip(index));
            _hud.Add(button);
        }
        _detail=PanelRect(_root,"Cartridge inspection",20,88,370,220,Panel).gameObject;
        _detailText=Label(_detail.transform,"Details",12,10,346,200,15); _detail.SetActive(false);
        _upgradeScreen = gameObject.AddComponent<BandolierUpgradeScreen>();
        _upgradeScreen.Initialize(run, _canvas);
        _modal = PanelRect(_root, "Victory screen", 0, 0, 1280, 720, new Color(0.025f, 0.04f, 0.065f, 0.98f));
        _modal.gameObject.SetActive(false);
        run.Drum.Transferred+=Transfer;
        Refresh();
    }
    RectTransform Rect(Transform parent,string name,float x,float y,float width,float height)
    {
        var go=new GameObject(name,typeof(RectTransform)); var r=go.GetComponent<RectTransform>(); r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(width,height); return r;
    }
    RectTransform PanelRect(Transform parent,string name,float x,float y,float w,float h,Color color)
    {
        var r=Rect(parent,name,x,y,w,h); var image=r.gameObject.AddComponent<Image>(); image.color=color; return r;
    }
    Text Label(Transform parent,string name,float x,float y,float w,float h,int size)
    {
        var r=Rect(parent,name,x,y,w,h); var t=r.gameObject.AddComponent<Text>(); t.font=_font; t.fontSize=size; t.color=new Color(.96f,.94f,.87f); t.raycastTarget=false;
        t.horizontalOverflow=HorizontalWrapMode.Wrap; t.verticalOverflow=VerticalWrapMode.Truncate; return t;
    }
    Button MakeButton(Transform parent,string name,float x,float y,float w,float h,UnityEngine.Events.UnityAction action)
    {
        var r=PanelRect(parent,name,x,y,w,h,new Color(.15f,.2f,.28f));var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.onClick.AddListener(action);
        var t=Label(r,"Label",9,6,w-18,h-12,15);t.alignment=TextAnchor.MiddleCenter; return b;
    }
    void TextOf(Button b,string text,Color color) { var t=b.GetComponentInChildren<Text>();t.text=text;t.color=color; }
    public void ShowReward()
    {
        TooltipPresenter.For(_canvas).HideAll();
        _detail.SetActive(false);
        _upgradeScreen.ShowStage();
    }

    public void HideReward()
    {
        TooltipPresenter.For(_canvas).HideAll();
        _upgradeScreen.Close();
        Refresh();
    }

    private TooltipContent SlotTooltip(int index)
    {
        return AmmoTooltipContent.Create(_run.Drum.Cartridges[index], _run.Drum.Sockets[index]);
    }

    public void ShowVictory()
    {
        _modal.gameObject.SetActive(true);
        foreach (Transform child in _modal)
        {
            child.gameObject.SetActive(false);
        }
        Text title = Label(_modal, "Victory", 140, 180, 1000, 160, 42);
        title.alignment = TextAnchor.MiddleCenter;
        title.text = "ВСЕ " + GameScript.WaveCount + " ВОЛН ПРОЙДЕНЫ\nБосс повержен";
        title.color = Gold;
        Text details = Label(_modal, "Statistics", 140, 350, 1000, 70, 22);
        details.alignment = TextAnchor.MiddleCenter;
        details.text = "Всего выстрелов: " + _run.Shooter.CompletedShots
            + "\nПопробуйте новый барабан в следующем забеге.";
        Button restart = MakeButton(_modal, "Restart", 440, 470, 400, 65, () =>
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        });
        TextOf(restart, "Новый забег · R", Gold);
    }
    void Inspect(int slot)
    {
        if(_run.AwaitingReward) return;
        if(_detail.activeSelf&&_detailSlot==slot) {_detail.SetActive(false);return;}
        _detailSlot=slot;_detail.SetActive(true);
        var item=_run.Drum.Cartridges[slot];var socket=_run.Drum.Sockets[slot];
        _detailText.text="ГНЕЗДО "+(slot+1)+" · урон "+_run.Drum.Profiles[slot].Damage.ToString("0.#")+"\n"+item.Description+"\nМОДУЛЬ: "+(socket==null?"нет":socket.Description);
    }
    public void Transfer(int source,int target)
    {
        _pulses[source]=_pulses[target]=Time.unscaledTime+.7f;
        Note("Срабатывание: гнездо "+(source+1)+" → "+(target+1));
    }
    public void Note(string text) { if(_note!=null) _note.text=text; }
    void Update() { if(_run!=null) Refresh(); }
    void Refresh()
    {
        if (_run.Victory)
        {
            _round.text = "Победа";
        }
        else if (_run.AwaitingReward)
        {
            _round.text = "Выбор награды · бой на паузе";
        }
        else
        {
            _round.text = "Волна " + _run.Round + " / " + GameScript.WaveCount
                + " · группа " + _run.Pack + " / " + _run.PackCount
                + " · врагов " + G.Enemies.Count;
            if (_run.Drum.IsReloading)
            {
                _round.text += " · ПЕРЕЗАРЯДКА";
            }
        }
        for(int i=0;i<_hud.Count;i++)
        {
            var item=_run.Drum.Cartridges[i]; var module=_run.Drum.Sockets[i];
            string text=(i+1)+" · "+_run.Drum.Profiles[i].Damage.ToString("0.#")+" урона\n"+item.Properties.Description().Replace('\n',' ')+"\n"+(module==null?"Без модуля":"М: "+module.Properties.Description().Replace('\n',' '));
            TextOf(_hud[i],text,i<_run.Drum.Used?new Color(.5f,.52f,.57f):AmmoItem.RarityColor(item.Rarity));
            _hud[i].GetComponent<Image>().color=_pulses[i]>Time.unscaledTime?new Color(.45f,.34f,.16f):i==_run.Drum.Used?new Color(.25f,.27f,.22f):new Color(.1f,.14f,.2f,.95f);
            _hud[i].GetComponentInChildren<Text>().fontSize=12;
        }
        foreach(var enemy in _labels.Keys.Where(e=>e==null||!e.IsAlive).ToArray()) { if(_labels[enemy]!=null) Destroy(_labels[enemy].gameObject);_labels.Remove(enemy); }
        foreach(var enemy in G.Enemies)
        {
            if(enemy==null||!enemy.IsAlive) continue;
            if(!_labels.TryGetValue(enemy,out var label)) {label=Label(_root,"Enemy health",0,0,240,42,12);label.alignment=TextAnchor.MiddleCenter;_labels[enemy]=label;label.transform.SetAsFirstSibling();}
            Vector3 screen=Camera.main.WorldToScreenPoint(enemy.transform.position+Vector3.up*(enemy.MaxHealth>=80?1.2f:.8f));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root,screen,null,out var local);
            var r=label.rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=local;
            label.text=enemy.Kind+" "+enemy.Health.ToString("0.#")+" / "+enemy.MaxHealth.ToString("0")+"\n"+enemy.StatusSummary;
        }
    }
    public void Damage(Vector3 world,float amount,Color color) { StartCoroutine(FloatDamage(world,amount,color)); }
    IEnumerator FloatDamage(Vector3 world,float amount,Color color)
    {
        var text=Label(_root,"Hit damage",0,0,120,35,22);text.transform.SetAsFirstSibling();text.alignment=TextAnchor.MiddleCenter;text.text="−"+amount.ToString("0.#");text.color=color;
        var r=text.rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root,Camera.main.WorldToScreenPoint(world),null,out var local);
        float start=Time.time;while(Time.time-start<.55f) {float t=Time.time-start;r.anchoredPosition=local+Vector2.up*(20+t*65);text.color=new Color(color.r,color.g,color.b,1-t/.55f);yield return null;}
        Destroy(text.gameObject);
    }
    void OnDestroy() { if(_run!=null&&_run.Drum!=null) _run.Drum.Transferred-=Transfer;if(_canvas!=null) Destroy(_canvas.gameObject);if(Instance==this) Instance=null; }
}
