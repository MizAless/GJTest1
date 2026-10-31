using UnityEngine;

public class StackedUpgradeTooltip : MonoBehaviour
{
    private TooltipView _bullet;
    private TooltipView _module;
    private RectTransform _group;
    private RectTransform _canvasRect;
    private Canvas _canvas;
    private Vector2 _naturalSize;
    private object _owner;
    public bool IsVisible => _group != null && _group.gameObject.activeSelf;
    public bool HasModule => IsVisible && _module.gameObject.activeSelf;

    public void Initialize(Canvas canvas)
    {
        _canvas = canvas;
        _canvasRect = (RectTransform)canvas.transform;
        _group = new GameObject("Slot tooltips", typeof(RectTransform)).GetComponent<RectTransform>();
        _group.SetParent(_canvasRect, false);
        _group.anchorMin = _group.anchorMax = _canvasRect.pivot;
        _group.pivot = new Vector2(0.5f, 0.5f);
        TooltipView prefab = Resources.Load<TooltipView>("UI/ItemTooltip");
        _bullet = Instantiate(prefab, _group);
        _module = Instantiate(prefab, _group);
        _group.gameObject.SetActive(false);
    }

    public void Show(object owner, AmmoItem bullet, AmmoItem module)
    {
        TooltipPresenter.For(_canvas).HideAll();
        _owner = owner;
        _group.gameObject.SetActive(true);
        _group.SetAsLastSibling();
        _bullet.SetContent(AmmoTooltipContent.Create(bullet));
        _module.gameObject.SetActive(module != null);
        if (module != null)
        {
            _module.SetContent(AmmoTooltipContent.Create(module));
        }
        float width = _bullet.Rect.rect.width;
        float height = _bullet.Rect.rect.height;
        Place(_bullet.Rect, 0);
        if (module != null)
        {
            width = Mathf.Max(width, _module.Rect.rect.width);
            Place(_module.Rect, -height - 8);
            height += _module.Rect.rect.height + 8;
        }
        _naturalSize = new Vector2(width, height);
        _group.sizeDelta = _naturalSize;
        Follow(true);
    }

    private static void Place(RectTransform panel, float y)
    {
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1);
        panel.pivot = new Vector2(0.5f, 1);
        panel.anchoredPosition = new Vector2(0, y);
    }

    public void Hide(object owner)
    {
        if (ReferenceEquals(_owner, owner))
        {
            HideAll();
        }
    }

    public void HideAll()
    {
        _owner = null;
        if (_group != null)
        {
            _group.gameObject.SetActive(false);
        }
    }

    private void Follow(bool snap)
    {
        float height = _bullet.Rect.rect.height;
        float width = _bullet.Rect.rect.width;
        if (_module.gameObject.activeSelf)
        {
            Place(_module.Rect, -height - 8);
            height += _module.Rect.rect.height + 8;
            width = Mathf.Max(width, _module.Rect.rect.width);
        }
        _naturalSize = new Vector2(width, height);
        _group.sizeDelta = _naturalSize;
        Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, Input.mousePosition, camera, out Vector2 pointer);
        Rect bounds = _canvasRect.rect;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, Screen.safeArea.min, camera, out Vector2 safeMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, Screen.safeArea.max, camera, out Vector2 safeMax);
        bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin, safeMin.x) + 12, Mathf.Max(bounds.yMin, safeMin.y) + 12,
            Mathf.Min(bounds.xMax, safeMax.x) - 12, Mathf.Min(bounds.yMax, safeMax.y) - 12);
        float scale = Mathf.Min(1, bounds.width / _naturalSize.x, bounds.height / _naturalSize.y);
        _group.localScale = Vector3.one * scale;
        Vector2 size = _naturalSize * scale;
        Vector2 target = TooltipPresenter.CalculateTarget(pointer, size, bounds, 20);
        float blend = 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime);
        _group.anchoredPosition = snap ? target : Vector2.Lerp(_group.anchoredPosition, target, blend);
        Vector2 center = _group.anchoredPosition;
        center.x = Mathf.Clamp(center.x, bounds.xMin + size.x / 2, bounds.xMax - size.x / 2);
        center.y = Mathf.Clamp(center.y, bounds.yMin + size.y / 2, bounds.yMax - size.y / 2);
        _group.anchoredPosition = center;
    }

    private void LateUpdate()
    {
        RefreshPosition();
    }

    public void RefreshPosition()
    {
        if (IsVisible)
        {
            Follow(false);
        }
    }

    private void OnDestroy()
    {
        if (_group != null)
        {
            Destroy(_group.gameObject);
        }
    }
}
