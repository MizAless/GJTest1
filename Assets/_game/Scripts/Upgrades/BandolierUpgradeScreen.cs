using UnityEngine;
using UnityEngine.UI;

public class BandolierUpgradeScreen : MonoBehaviour
{
    private RunDirector _run;
    private Canvas _canvas;
    private RectTransform _view;
    private RectTransform _confirmation;
    private Text _heading;
    private Text _instructions;
    private Text _confirmationText;
    private readonly Text[] _offerLabels = new Text[3];
    private readonly Image[] _offerPanels = new Image[3];
    private readonly Outline[] _offerOutlines = new Outline[3];
    private readonly RectTransform[] _slots = new RectTransform[6];
    private UpgradeSelection _selection;
    private UpgradePresentation _presentation;
    private StackedUpgradeTooltip _tooltips;
    private Font _font;
    private static Color Gold => new Color(1, 0.76f, 0.4f);

    public UpgradeSelection Selection => _selection;
    public UpgradePresentation Presentation => _presentation;
    public StackedUpgradeTooltip Tooltips => _tooltips;
    public bool IsOpen => _view != null && _view.gameObject.activeSelf;

    public void Initialize(RunDirector run, Canvas canvas)
    {
        _run = run;
        _canvas = canvas;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _selection = new UpgradeSelection(run);
        _selection.Changed += Refresh;
        _view = Panel(canvas.transform, "Bandolier upgrades", Vector2.zero, Vector2.one,
            new Color(0.065f, 0.046f, 0.033f, 1));
        _heading = Label(_view, "Upgrade title", new Vector2(0.035f, 0.87f), new Vector2(0.94f, 0.965f), 27);
        Label(_view, "Bandolier title", new Vector2(0.43f, 0.79f), new Vector2(0.97f, 0.86f), 20).text = "ВАШ ПАТРОНТАШ";
        for (int index = 0; index < 3; index++)
        {
            int offer = index;
            float bottom = 0.605f - index * 0.22f;
            Button button = Button(_view, "Upgrade offer " + (index + 1), new Vector2(0.025f, bottom),
                new Vector2(0.36f, bottom + 0.195f), () => _selection.SelectOffer(offer));
            _offerPanels[index] = button.GetComponent<Image>();
            _offerOutlines[index] = button.gameObject.AddComponent<Outline>();
            _offerOutlines[index].effectColor = Gold;
            _offerOutlines[index].effectDistance = new Vector2(2, -2);
            _offerLabels[index] = Label(button.transform, "Offer description", new Vector2(0.43f, 0.07f), new Vector2(0.97f, 0.93f), 16);
            button.gameObject.AddComponent<TooltipTrigger>().Bind(() => AmmoTooltipContent.Create(_run.Offers[offer]));
        }
        RectTransform renderRect = Rect(_view, "Rendered bandolier and offers", Vector2.zero, Vector2.one);
        RawImage image = renderRect.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        _presentation = new GameObject("Upgrade presentation stage").AddComponent<UpgradePresentation>();
        _presentation.Initialize(run, _selection, image);
        _tooltips = _view.gameObject.AddComponent<StackedUpgradeTooltip>();
        _tooltips.Initialize(canvas);
        for (int index = 0; index < 6; index++)
        {
            int slot = index;
            Button button = Button(_view, "Upgrade slot " + (index + 1), Vector2.zero, Vector2.one,
                () => _selection.SelectSlot(slot));
            button.GetComponent<Image>().color = Color.clear;
            _slots[index] = (RectTransform)button.transform;
            _slots[index].anchorMin = _slots[index].anchorMax = new Vector2(0.5f, 0.5f);
            button.gameObject.AddComponent<UpgradeSlotHover>().Initialize(run, _tooltips, slot);
            Text number = Label(button.transform, "Slot number", new Vector2(0, 0), new Vector2(1, 0.12f), 16);
            number.text = (index + 1).ToString();
            number.alignment = TextAnchor.MiddleCenter;
            number.color = Gold;
        }
        _instructions = Label(_view, "Upgrade instructions", new Vector2(0.41f, 0.065f), new Vector2(0.97f, 0.19f), 16);
        Button clear = Button(_view, "Clear upgrade selection", new Vector2(0.025f, 0.07f), new Vector2(0.36f, 0.13f), () => _selection.Reset());
        Text clearLabel = Label(clear.transform, "Label", Vector2.zero, Vector2.one, 16);
        clearLabel.text = "Снять выбор · Esc";
        clearLabel.alignment = TextAnchor.MiddleCenter;
        BuildConfirmation();
        _view.gameObject.SetActive(false);
        _presentation.gameObject.SetActive(false);
    }

    private void BuildConfirmation()
    {
        _confirmation = Panel(_view, "Replacement confirmation", Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.83f));
        RectTransform panel = Panel(_confirmation, "Confirmation panel", new Vector2(0.24f, 0.25f), new Vector2(0.79f, 0.77f),
            new Color(0.13f, 0.09f, 0.065f, 1));
        panel.gameObject.AddComponent<Outline>().effectColor = Gold;
        Text title = Label(panel, "Confirm title", new Vector2(0.05f, 0.77f), new Vector2(0.95f, 0.95f), 24);
        title.text = "ПОДТВЕРДИТЬ ЗАМЕНУ";
        title.color = Gold;
        _confirmationText = Label(panel, "Replacement details", new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.76f), 17);
        Button cancel = Button(panel, "Cancel replacement", new Vector2(0.05f, 0.06f), new Vector2(0.47f, 0.21f), () => _selection.Cancel());
        Text cancelLabel = Label(cancel.transform, "Label", Vector2.zero, Vector2.one, 18);
        cancelLabel.text = "Отмена";
        cancelLabel.alignment = TextAnchor.MiddleCenter;
        Button confirm = Button(panel, "Confirm replacement", new Vector2(0.53f, 0.06f), new Vector2(0.95f, 0.21f), () => _selection.Confirm());
        Text confirmLabel = Label(confirm.transform, "Label", Vector2.zero, Vector2.one, 18);
        confirmLabel.text = "Заменить";
        confirmLabel.color = Gold;
        confirmLabel.alignment = TextAnchor.MiddleCenter;
        _confirmation.gameObject.SetActive(false);
    }

    public void ShowStage()
    {
        bool replay = !IsOpen;
        _view.gameObject.SetActive(true);
        _view.SetAsLastSibling();
        _selection.Reset();
        _presentation.Open(replay);
    }

    public void Close()
    {
        _tooltips.HideAll();
        TooltipPresenter.For(_canvas).HideAll();
        _view.gameObject.SetActive(false);
        _presentation.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        if (_presentation == null)
        {
            return;
        }
        _tooltips.HideAll();
        TooltipPresenter.For(_canvas).HideAll();
        _presentation.RefreshItems();
        string reward = _run.ChoosingModule ? "МОДУЛЬ" : "ПАТРОН";
        string step = _run.ChoosingModule ? "2 / 2" : "1 / 2";
        _heading.text = "ВОЛНА " + _run.Round + " ПРОЙДЕНА  /  " + reward + "  " + step;
        for (int index = 0; index < 3; index++)
        {
            AmmoItem item = _run.Offers[index];
            _offerLabels[index].text = AmmoItem.RarityName(item.Rarity) + "\n" + item.Properties.Description();
            _offerLabels[index].color = AmmoItem.RarityColor(item.Rarity);
            _offerOutlines[index].enabled = _selection.Offer == index;
            _offerPanels[index].color = _selection.Offer == index
                ? new Color(0.26f, 0.18f, 0.1f, 1) : new Color(0.13f, 0.095f, 0.07f, 1);
        }
        if (_selection.Offer >= 0)
        {
            _instructions.text = "Выберите гнездо справа, чтобы установить новый предмет.\nПерестановка доступна до получения обоих улучшений.";
        }
        else if (_selection.Slot >= 0)
        {
            _instructions.text = "Выберите другой патрон справа для перестановки\nили предложение слева для замены.";
        }
        else
        {
            _instructions.text = "Выберите награду слева или патрон справа.\nПатроны можно менять местами; модули остаются в гнёздах.";
        }
        _confirmation.gameObject.SetActive(_selection.AwaitingConfirmation);
        if (_selection.AwaitingConfirmation)
        {
            AmmoItem old = _run.ChoosingModule ? _run.Drum.Sockets[_selection.Slot] : _run.Drum.Cartridges[_selection.Slot];
            AmmoItem replacement = _run.Offers[_selection.Offer];
            string oldName = old == null ? "Пустое гнездо модуля" : old.Name;
            string ending = _run.ChoosingModule
                ? "После установки модуля экран закроется. Следующая перестановка — между волнами."
                : "Затем выберем модуль. До его установки патроны ещё можно переставлять.";
            _confirmationText.text = "Гнездо " + (_selection.Slot + 1) + "\nУбираем: " + oldName
                + "\nПолучаем: " + replacement.Name + "\n\n" + ending;
            _confirmation.SetAsLastSibling();
        }
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            _selection.Cancel();
        }
    }

    private void LateUpdate()
    {
        RefreshLayout();
    }

    public void RefreshLayout()
    {
        if (!IsOpen)
        {
            return;
        }
        RectTransform canvasRect = (RectTransform)_canvas.transform;
        for (int index = 0; index < 6; index++)
        {
            Vector2 viewport = _presentation.SlotViewportPosition(index);
            float x = (viewport.x - 0.5f) * canvasRect.rect.width;
            _slots[index].anchoredPosition = new Vector2(x, canvasRect.rect.height * 0.02f);
            _slots[index].sizeDelta = new Vector2(canvasRect.rect.width * 0.074f, canvasRect.rect.height * 0.62f);
        }
    }

    private RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        RectTransform rect = Rect(parent, name, min, max);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    private Text Label(Transform parent, string name, Vector2 min, Vector2 max, int size)
    {
        Text text = Rect(parent, name, min, max).gameObject.AddComponent<Text>();
        text.font = _font;
        text.fontSize = size;
        text.color = new Color(0.94f, 0.89f, 0.79f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Button Button(Transform parent, string name, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Panel(parent, name, min, max, new Color(0.19f, 0.14f, 0.1f, 1));
        Button button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        return button;
    }

    private void OnDestroy()
    {
        if (_presentation != null)
        {
            Destroy(_presentation.gameObject);
        }
        if (_view != null)
        {
            Destroy(_view.gameObject);
        }
    }
}
