using UnityEngine;
using UnityEngine.UI;
using GameEconomy;

public enum IntermissionPage { Rewards, Craft, Shop }

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
    private RectTransform _rewards;
    private CraftingPage _craftPage;
    private ShopPage _shopPage;
    private Text _rewardsDone;
    private Text _coins;
    private Text _confirmLabel;
    private Button _nextWave;
    private readonly Button[] _tabs = new Button[3];
    public IntermissionPage CurrentPage { get; private set; }
    public CraftingPage CraftPage => _craftPage;
    public ShopPage ShopPage => _shopPage;
    public bool AwaitingConfirmation => _selection.AwaitingConfirmation || _shopPage.AwaitingConfirmation;
    private static Color Gold => new Color(1, 0.76f, 0.4f);

    public UpgradeSelection Selection => _selection;
    public UpgradePresentation Presentation => _presentation;
    public StackedUpgradeTooltip Tooltips => _tooltips;
    public bool IsOpen => _view != null && _view.gameObject.activeSelf;

    public void Initialize(RunDirector run, Canvas canvas)
    {
        _run = run;
        _canvas = canvas;
        _font = PixelUiTheme.Font;
        _selection = new UpgradeSelection(run);
        _selection.Changed += Refresh;
        _view = Panel(canvas.transform, "Bandolier upgrades", Vector2.zero, Vector2.one,
            new Color(0.065f, 0.046f, 0.033f, 1));
        _view.gameObject.AddComponent<UpgradeTextMotionScope>();
        _heading = Label(_view, "Upgrade title", new Vector2(0.035f, 0.87f), new Vector2(0.72f, 0.965f), 27);
        Label(_view, "Bandolier title", new Vector2(0.43f, 0.79f), new Vector2(0.97f, 0.86f), 20).text = "ВАШ ПАТРОНТАШ";
        _rewards = Rect(_view, "Rewards page", Vector2.zero, Vector2.one);
        _rewardsDone = Label(_rewards, "Rewards complete", new Vector2(0.035f, 0.35f), new Vector2(0.37f, 0.7f), 23);
        _rewardsDone.text = "Награды получены\n\nМожно улучшить патроны, заглянуть в магазин или продолжить бой.";
        for (int index = 0; index < 3; index++)
        {
            int offer = index;
            float bottom = 0.605f - index * 0.22f;
            Button button = Button(_rewards, "Upgrade offer " + (index + 1), new Vector2(0.025f, bottom),
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
                () => SelectSlot(slot));
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
        Button clear = Button(_view, "Clear upgrade selection", new Vector2(0.025f, 0.07f), new Vector2(0.36f, 0.13f), ClearSelection);
        Text clearLabel = Label(clear.transform, "Label", Vector2.zero, Vector2.one, 16);
        clearLabel.text = "Снять выбор · Esc";
        clearLabel.alignment = TextAnchor.MiddleCenter;
        BuildPages();
        BuildConfirmation();
        _run.Economy.Inventory.Changed += OnInventoryChanged;
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
        Button cancel = Button(panel, "Cancel replacement", new Vector2(0.05f, 0.06f), new Vector2(0.47f, 0.21f), CancelSelection);
        Text cancelLabel = Label(cancel.transform, "Label", Vector2.zero, Vector2.one, 18);
        cancelLabel.text = "Отмена";
        cancelLabel.alignment = TextAnchor.MiddleCenter;
        Button confirm = Button(panel, "Confirm replacement", new Vector2(0.53f, 0.06f), new Vector2(0.95f, 0.21f), ConfirmSelection);
        _confirmLabel = Label(confirm.transform, "Label", Vector2.zero, Vector2.one, 18);
        _confirmLabel.color = Gold;
        _confirmLabel.alignment = TextAnchor.MiddleCenter;
        _confirmation.gameObject.SetActive(false);
    }

    private void BuildPages()
    {
        RectTransform craft = Rect(_view, "Craft page", new Vector2(0.025f, 0.17f), new Vector2(0.385f, 0.79f));
        _craftPage = craft.gameObject.AddComponent<CraftingPage>();
        _craftPage.Initialize(_run, _selection);
        _craftPage.Changed += Refresh;
        _craftPage.Crafted += slot => _presentation.PulseSlot(slot);
        RectTransform shop = Rect(_view, "Shop page", new Vector2(0.025f, 0.17f), new Vector2(0.385f, 0.79f));
        _shopPage = shop.gameObject.AddComponent<ShopPage>();
        _shopPage.Initialize(_run, _selection);
        _shopPage.Changed += Refresh;
        string[] names = { "Награды", "Крафт", "Магазин" };
        for (int index = 0; index < names.Length; index++)
        {
            IntermissionPage page = (IntermissionPage)index;
            float left = 0.025f + index * 0.123f;
            _tabs[index] = IntermissionWidgets.Button(_view, "Page " + page,
                new Vector2(left, 0.815f), new Vector2(left + 0.115f, 0.865f), names[index], () => ShowPage(page));
        }
        _coins = Label(_view, "Intermission coins", new Vector2(0.73f, 0.89f), new Vector2(0.97f, 0.965f), 22);
        _coins.alignment = TextAnchor.MiddleRight;
        _coins.color = Gold;
        _nextWave = IntermissionWidgets.Button(_view, "Next wave", new Vector2(0.73f, 0.025f),
            new Vector2(0.97f, 0.085f), "Следующая волна", () => _run.ContinueAfterRewards());
    }

    public void ShowStage()
    {
        bool replay = !IsOpen;
        if (replay)
        {
            CurrentPage = IntermissionPage.Rewards;
            _craftPage.ClearSelection();
            _shopPage.ClearSelection();
        }
        _view.gameObject.SetActive(true);
        _view.SetAsLastSibling();
        _selection.Reset();
        Refresh();
        _presentation.Open(replay);
    }

    public void ShowPage(IntermissionPage page)
    {
        if (!IsOpen || !_run.AwaitingReward)
        {
            return;
        }
        _craftPage.ClearSelection();
        _shopPage.ClearSelection();
        CurrentPage = page;
        if (page == IntermissionPage.Shop)
        {
            _shopPage.Open();
        }
        _selection.Reset();
    }

    public void SelectSlot(int slot)
    {
        if (!IsOpen || AwaitingConfirmation)
        {
            return;
        }
        if (CurrentPage == IntermissionPage.Craft && _craftPage.CapturesSlots)
        {
            _selection.FocusSlot(slot);
        }
        else if (CurrentPage != IntermissionPage.Shop || !_shopPage.SelectSlot(slot))
        {
            _selection.SelectSlot(slot);
        }
    }

    public void ClearSelection()
    {
        _craftPage.ClearSelection();
        _shopPage.ClearSelection();
        _selection.Reset();
    }

    public void CancelSelection()
    {
        ClearSelection();
    }

    public void ConfirmSelection()
    {
        if (CurrentPage == IntermissionPage.Shop)
        {
            _shopPage.Confirm();
        }
        else
        {
            _selection.Confirm();
        }
    }

    public void Close()
    {
        if (_view == null)
        {
            return;
        }
        ClearSelection();
        HideTooltips();
        _view.gameObject.SetActive(false);
        _presentation.gameObject.SetActive(false);
    }

    private void OnInventoryChanged(InventoryChange change)
    {
        if (IsOpen)
        {
            Refresh();
        }
    }

    private void HideTooltips()
    {
        _tooltips.HideAll();
        TooltipPresenter.For(_canvas).HideAll();
    }

    private void Refresh()
    {
        if (_presentation == null || _shopPage == null || !IsOpen || _run.Offers == null)
        {
            return;
        }
        HideTooltips();
        bool rewards = CurrentPage == IntermissionPage.Rewards;
        _rewards.gameObject.SetActive(rewards);
        _craftPage.gameObject.SetActive(CurrentPage == IntermissionPage.Craft);
        _shopPage.gameObject.SetActive(CurrentPage == IntermissionPage.Shop);
        _presentation.OffersVisible = rewards && !_run.RewardsComplete;
        _presentation.RefreshItems();
        _coins.text = "Монеты: " + _run.Economy.Inventory.Coins;
        string[] titles = { "НАГРАДЫ", "КРАФТ", "МАГАЗИН" };
        _heading.text = "ВОЛНА " + _run.Round + " ПРОЙДЕНА  /  " + titles[(int)CurrentPage];
        for (int index = 0; index < _tabs.Length; index++)
        {
            IntermissionWidgets.Select(_tabs[index], index == (int)CurrentPage);
        }
        RefreshRewards();
        _craftPage.Refresh();
        _shopPage.Refresh();
        _nextWave.interactable = _run.RewardsComplete && !AwaitingConfirmation;
        RefreshInstructions();
        RefreshConfirmation();
    }

    private void RefreshRewards()
    {
        _rewardsDone.gameObject.SetActive(_run.RewardsComplete);
        if (CurrentPage == IntermissionPage.Rewards && !_run.RewardsComplete)
        {
            string type = _run.ChoosingModule ? "МОДУЛЬ  2 / 2" : "ПАТРОН  1 / 2";
            _heading.text = "ВОЛНА " + _run.Round + " ПРОЙДЕНА  /  " + type;
        }
        for (int index = 0; index < 3; index++)
        {
            _offerPanels[index].gameObject.SetActive(!_run.RewardsComplete);
            if (_run.RewardsComplete)
            {
                continue;
            }
            AmmoItem item = _run.Offers[index];
            _offerLabels[index].text = item.Name + "\n" + AmmoItem.RarityName(item.Rarity) + "\n" + item.Properties.Description();
            _offerLabels[index].color = AmmoItem.RarityColor(item.Rarity);
            _offerOutlines[index].enabled = _selection.Offer == index;
            _offerPanels[index].color = _selection.Offer == index
                ? new Color(0.26f, 0.18f, 0.1f, 1) : new Color(0.13f, 0.095f, 0.07f, 1);
        }
    }

    private void RefreshInstructions()
    {
        if (CurrentPage == IntermissionPage.Craft)
        {
            _instructions.text = "Осколок → патрон или модуль → новое свойство.\nСнимите выбор, чтобы переставлять патроны.";
        }
        else if (CurrentPage == IntermissionPage.Shop)
        {
            _instructions.text = _shopPage.SelectedOffer != null
                ? "Выберите гнездо для купленного патрона."
                : "Выберите товар слева.\nПатроны справа можно менять местами.";
        }
        else if (_run.RewardsComplete)
        {
            _instructions.text = "Бесплатные награды получены.\nПереставьте патроны или начните следующую волну.";
        }
        else
        {
            _instructions.text = _selection.Offer >= 0
                ? "Выберите гнездо для нового предмета."
                : "Выберите бесплатную награду или переставьте патроны.";
        }
        if (!_run.RewardsComplete)
        {
            _instructions.text += "\nДля продолжения заберите патрон и модуль.";
        }
    }

    private void RefreshConfirmation()
    {
        _confirmation.gameObject.SetActive(AwaitingConfirmation);
        if (!AwaitingConfirmation)
        {
            return;
        }
        if (CurrentPage == IntermissionPage.Shop)
        {
            _confirmationText.text = _shopPage.ConfirmationText();
            _confirmLabel.text = "Купить и заменить";
        }
        else
        {
            AmmoItem old = _run.ChoosingModule ? _run.Drum.Sockets[_selection.Slot] : _run.Drum.Cartridges[_selection.Slot];
            AmmoItem replacement = _run.Offers[_selection.Offer];
            string oldName = old == null ? "Пустое гнездо модуля" : old.Name;
            _confirmationText.text = "Гнездо " + (_selection.Slot + 1) + "\nУбираем: " + oldName
                + "\nПолучаем: " + replacement.Name;
            _confirmLabel.text = "Заменить";
        }
        _confirmation.SetAsLastSibling();
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelSelection();
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
        text.gameObject.AddComponent<WavyUpgradeText>();
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
        if (_run != null && _run.Economy != null)
        {
            _run.Economy.Inventory.Changed -= OnInventoryChanged;
        }
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
