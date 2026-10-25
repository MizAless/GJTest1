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
    Text _round, _note, _title, _subtitle, _comparison, _detailText;
    GameObject _detail;
    Button _confirm;
    readonly List<Button> _hud = new List<Button>();
    readonly List<Button> _rewardSlots = new List<Button>();
    readonly List<Button> _cards = new List<Button>();
    readonly Dictionary<Enemy,Text> _labels = new Dictionary<Enemy,Text>();
    int _chosen, _slot, _detailSlot=-1;
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
        for(int i=0;i<6;i++) { int index=i; var b=MakeButton(_root,"Socket "+(i+1),24+i*206,618,196,84,()=>Inspect(index)); _hud.Add(b); }
        _detail=PanelRect(_root,"Cartridge inspection",20,88,370,220,Panel).gameObject;
        _detailText=Label(_detail.transform,"Details",12,10,346,200,15); _detail.SetActive(false);
        BuildReward();
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
    void BuildReward()
    {
        _modal=PanelRect(_root,"Reward screen",0,0,1280,720,new Color(.025f,.04f,.065f,.98f));
        _title=Label(_modal,"Title",45,25,1190,42,29);_title.color=Gold;
        _subtitle=Label(_modal,"Subtitle",45,73,1190,48,16);
        for(int i=0;i<3;i++)
        {
            int index=i; var b=MakeButton(_modal,"Reward "+(i+1),45+i*400,135,385,270,()=>{_chosen=index;RefreshRewards();});
            _cards.Add(b); var t=b.GetComponentInChildren<Text>();t.alignment=TextAnchor.UpperLeft;t.fontSize=17;t.supportRichText=true;
        }
        _comparison=Label(_modal,"Slot comparison",45,423,1190,55,15);
        for(int i=0;i<6;i++)
        {
            int index=i; var b=MakeButton(_modal,"Install in slot "+(i+1),45+i*200,487,185,88,()=>{_slot=index;RefreshRewards();});
            b.GetComponentInChildren<Text>().fontSize=12;_rewardSlots.Add(b);
        }
        MakeButton(_modal,"Move cartridge left",45,596,230,45,()=>{_run.Drum.SwapCartridges(_slot,_run.Drum.Next(_slot-1));_slot=_run.Drum.Next(_slot-1);RefreshRewards();}).GetComponentInChildren<Text>().text="← Переместить патрон";
        MakeButton(_modal,"Move cartridge right",290,596,230,45,()=>{_run.Drum.SwapCartridges(_slot,_run.Drum.Next(_slot+1));_slot=_run.Drum.Next(_slot+1);RefreshRewards();}).GetComponentInChildren<Text>().text="Переместить патрон →";
        _confirm=MakeButton(_modal,"Confirm reward",820,596,405,52,()=>_run.Choose(_chosen,_slot));
        var help=Label(_modal,"Help",45,659,1170,38,14);help.text="Выберите 1 из 3, затем гнездо и подтвердите. Патрон можно переставить; модуль остаётся в своём гнезде. R — начать заново.";
        _modal.gameObject.SetActive(false);
    }
    public void ShowReward()
    {
        _chosen=0;_slot=0;_detail.SetActive(false);_modal.gameObject.SetActive(true);RefreshRewards();
    }
    public void HideReward() { _modal.gameObject.SetActive(false);Refresh(); }
    void RefreshRewards()
    {
        bool module=_run.ChoosingModule;
        string rewardType = module ? "модуль" : "патрон";
        _title.text = "Волна " + _run.Round + " пройдена · выберите " + rewardType;
        _subtitle.text=_run.Round==0?"Получен стартовый "+_run.Drum.Cartridges[0].Name+". Остальные патроны обычные. Выберите модуль для любого гнезда.":"Награда: один случайный "+(module?"модуль для гнезда":"патрон")+" из трёх. Сравните свойства и выберите место установки.";
        for(int i=0;i<3;i++)
        {
            var item=_run.Offers[i]; string color=ColorUtility.ToHtmlStringRGB(AmmoItem.RarityColor(item.Rarity));
            TextOf(_cards[i],"<b>"+item.Name+"</b>\n<color=#"+color+">"+AmmoItem.RarityName(item.Rarity)+"</color>\n\n"+item.Description+"\n\n<size=12>"+item.Properties.Tags()+"</size>",Color.white);
            _cards[i].GetComponent<Image>().color=i==_chosen?new Color(.28f,.29f,.24f):new Color(.12f,.17f,.24f);
        }
        for(int i=0;i<6;i++)
        {
            var cartridge=_run.Drum.Cartridges[i];var socket=_run.Drum.Sockets[i];
            TextOf(_rewardSlots[i],"ГНЕЗДО "+(i+1)+" · "+_run.Drum.Profiles[i].Damage.ToString("0.#")+" урона\n"+cartridge.Properties.Description().Replace('\n',' ') +"\nМодуль: "+(socket==null?"нет":socket.Properties.Description().Replace('\n',' ')),i==_slot?Gold:Color.white);
        }
        var old=module?_run.Drum.Sockets[_slot]:_run.Drum.Cartridges[_slot];
        _comparison.text="Гнездо "+(_slot+1)+". Заменяется: "+(old==null?"пустой модуль":old.Name)+".\n"+(old==null?"":old.Description.Replace('\n',' '));
        TextOf(_confirm,"Установить в гнездо "+(_slot+1),Gold);
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
