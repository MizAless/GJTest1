using System;
using System.Collections.Generic;
using GameEconomy;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopPage : MonoBehaviour
{
    private sealed class ProductCard
    {
        public ShopOffer Offer;
        public Button Button;
        public Text Label;
    }

    private RunDirector _run;
    private UpgradeSelection _selection;
    private IShopService _shop;
    private RectTransform _content;
    private Text _feedback;
    private Text _title;
    private readonly List<ProductCard> _cards = new List<ProductCard>();
    private readonly CartridgeIconCache _icons = new CartridgeIconCache();
    public ShopVisit Visit { get; private set; }
    public ShopOffer SelectedOffer { get; private set; }
    public bool AwaitingConfirmation { get; private set; }
    public event Action Changed;

    public void Initialize(RunDirector run, UpgradeSelection selection)
    {
        _run = run;
        _selection = selection;
        _shop = new ShopService(run.Economy,
            Resources.Load<ShopSettings>(ShopSettings.ResourcePath), run.Drum);
        _title = IntermissionWidgets.Label(transform, "Shop assortment", new Vector2(0, 0.93f), new Vector2(1, 1), 17);
        RectTransform viewport = IntermissionWidgets.Rect(transform, "Shop viewport", new Vector2(0, 0.02f), new Vector2(1, 0.92f));
        viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.12f);
        viewport.gameObject.AddComponent<RectMask2D>();
        _content = IntermissionWidgets.Rect(viewport, "Shop stock", Vector2.zero, Vector2.one);
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = Vector2.one;
        _content.pivot = new Vector2(0.5f, 1);
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = _content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35;
        _feedback = IntermissionWidgets.Label(transform, "Shop feedback", new Vector2(0, -0.07f), new Vector2(1, 0.02f), 17);
    }

    public void Open()
    {
        ShopVisit visit = _shop.Open(_run.Round);
        if (!ReferenceEquals(visit, Visit))
        {
            BuildStock(visit);
        }
        Refresh();
    }

    private void BuildStock(ShopVisit visit)
    {
        ClearSelection();
        Visit = visit;
        foreach (Transform child in _content)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        _cards.Clear();
        _icons.Dispose();
        _content.sizeDelta = new Vector2(0, visit.Offers.Count * 104);
        _content.anchoredPosition = Vector2.zero;
        _title.text = "Ассортимент · после волны " + visit.CompletedWave;
        for (int index = 0; index < visit.Offers.Count; index++)
        {
            ShopOffer offer = visit.Offers[index];
            Button button = IntermissionWidgets.Button(_content, "Shop product " + index,
                new Vector2(0, 1), Vector2.one, "", () => SelectOffer(offer));
            RectTransform rect = (RectTransform)button.transform;
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -index * 104);
            rect.sizeDelta = new Vector2(0, 96);
            Text label = button.GetComponentInChildren<Text>();
            label.rectTransform.anchorMin = new Vector2(0.23f, 0.04f);
            label.alignment = TextAnchor.MiddleLeft;
            var card = new ProductCard { Offer = offer, Button = button, Label = label };
            _cards.Add(card);
            if (offer.Kind == ShopProductKind.Cartridge)
            {
                AmmoItem preview = offer.Cartridge;
                RawImage icon = IntermissionWidgets.Rect(rect, "Cartridge icon", new Vector2(0.01f, 0.03f),
                    new Vector2(0.22f, 0.97f)).gameObject.AddComponent<RawImage>();
                icon.raycastTarget = false;
                icon.texture = _icons.Get(offer.Id, preview);
                button.gameObject.AddComponent<TooltipTrigger>().Bind(() => AmmoTooltipContent.Create(preview));
            }
            else
            {
                Text icon = IntermissionWidgets.Label(rect, "Shard grade", new Vector2(0.02f, 0.14f),
                    new Vector2(0.22f, 0.86f), 24, offer.Shard.Grade.ToString());
                icon.alignment = TextAnchor.MiddleCenter;
                icon.color = IntermissionWidgets.Gold;
                button.gameObject.AddComponent<TooltipTrigger>().Bind(() => CraftingPage.ShardTooltip(offer.Shard));
            }
        }
    }

    public void SelectOffer(ShopOffer offer)
    {
        if (!_run.AwaitingReward || offer.Purchased || AwaitingConfirmation)
        {
            return;
        }
        _feedback.text = "";
        if (offer.Kind == ShopProductKind.Shard)
        {
            ShowPurchaseResult(_shop.TryPurchase(offer.Id));
            return;
        }
        SelectedOffer = ReferenceEquals(SelectedOffer, offer) ? null : offer;
        AwaitingConfirmation = SelectedOffer != null && _selection.Slot >= 0;
        GameAudio.Play(SoundCue.Select);
        Changed?.Invoke();
    }

    public bool SelectSlot(int slot)
    {
        if (SelectedOffer == null)
        {
            return false;
        }
        AwaitingConfirmation = true;
        _selection.FocusSlot(slot);
        return true;
    }

    public string ConfirmationText()
    {
        AmmoItem old = _run.Drum.Cartridges[_selection.Slot];
        return "Гнездо " + (_selection.Slot + 1) + "\nУбираем: " + old.Name
            + "\nПолучаем: " + SelectedOffer.Cartridge.Name + "\n\nЦена: " + SelectedOffer.Price + " монет";
    }

    public void Confirm()
    {
        if (!AwaitingConfirmation || SelectedOffer == null || !_run.AwaitingReward)
        {
            return;
        }
        ShopPurchaseResult result = _shop.TryPurchase(SelectedOffer.Id, _selection.Slot);
        AwaitingConfirmation = false;
        if (result.Succeeded)
        {
            SelectedOffer = null;
            _selection.Reset();
        }
        ShowPurchaseResult(result);
    }

    private void ShowPurchaseResult(ShopPurchaseResult result)
    {
        switch (result.Code)
        {
            case ShopPurchaseCode.Success: _feedback.text = "Куплено"; break;
            case ShopPurchaseCode.InsufficientCoins: _feedback.text = "Не хватает монет"; break;
            case ShopPurchaseCode.AlreadyPurchased: _feedback.text = "Уже продано"; break;
            case ShopPurchaseCode.InventoryFull: _feedback.text = "Нет места для осколков"; break;
            case ShopPurchaseCode.RunEnded: _feedback.text = "Забег завершён"; break;
            default: _feedback.text = "Выберите товар и гнездо ещё раз"; break;
        }
        _feedback.color = result.Succeeded ? IntermissionWidgets.Gold : new Color(1, 0.48f, 0.35f);
        if (result.Succeeded)
        {
            GameAudio.Play(SoundCue.Select);
        }
        Changed?.Invoke();
    }

    public void ClearSelection()
    {
        SelectedOffer = null;
        AwaitingConfirmation = false;
        if (_feedback != null)
        {
            _feedback.text = "";
        }
    }

    public void Refresh()
    {
        foreach (ProductCard card in _cards)
        {
            ShopOffer offer = card.Offer;
            string name = offer.Kind == ShopProductKind.Cartridge ? offer.Cartridge.Name
                : "Осколок: " + CraftingPage.ShardName(offer.Shard.Kind) + " · " + offer.Shard.Grade + " ×" + offer.Quantity;
            string rarity = offer.Kind == ShopProductKind.Cartridge ? AmmoItem.RarityName(offer.Cartridge.Rarity) : "Для крафта";
            card.Label.text = name + "\n" + rarity + " · " + (offer.Purchased ? "ПРОДАНО" : offer.Price + " монет");
            card.Button.interactable = !offer.Purchased;
            IntermissionWidgets.Select(card.Button, ReferenceEquals(SelectedOffer, offer));
            if (offer.Kind == ShopProductKind.Cartridge && !offer.Purchased)
            {
                card.Label.color = AmmoItem.RarityColor(offer.Cartridge.Rarity);
            }
        }
    }

    private void OnDestroy()
    {
        _icons.Dispose();
    }
}
