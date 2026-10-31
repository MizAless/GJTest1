using UnityEngine;

[DisallowMultipleComponent]
public class TooltipPresenter : MonoBehaviour
{
    [SerializeField] private TooltipView _prefab;
    [SerializeField, Min(0)] private float _cursorGap = 20;
    [SerializeField, Min(0)] private float _screenMargin = 12;
    [SerializeField, Min(1)] private float _followSpeed = 18;

    private TooltipView _view;
    private RectTransform _canvasRect;
    private Canvas _canvas;
    private object _owner;

    public bool IsVisible => _view != null && _view.gameObject.activeSelf;
    public TooltipView View => _view;

    public static TooltipPresenter For(Canvas canvas)
    {
        Canvas root = canvas.rootCanvas;
        TooltipPresenter presenter = root.GetComponent<TooltipPresenter>();
        if (presenter == null)
        {
            presenter = root.gameObject.AddComponent<TooltipPresenter>();
        }

        return presenter;
    }

    public void Show(object owner, TooltipContent content, Vector2 pointer)
    {
        if (content == null)
        {
            Hide(owner);
            return;
        }

        if (!EnsureView())
        {
            return;
        }

        bool snap = !IsVisible;
        _owner = owner;
        _view.gameObject.SetActive(true);
        _view.transform.SetAsLastSibling();
        _view.SetContent(content);
        UpdatePosition(pointer, 0, snap);
    }

    public void Hide(object owner)
    {
        // Exit from an old item must not hide a newer item's tooltip.
        if (!ReferenceEquals(_owner, owner))
        {
            return;
        }

        HideAll();
    }

    public void HideAll()
    {
        _owner = null;
        if (_view != null)
        {
            _view.gameObject.SetActive(false);
        }
    }

    public void UpdatePosition(Vector2 screenPointer, float deltaTime, bool snap = false)
    {
        if (!IsVisible)
        {
            return;
        }

        Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPointer, camera, out Vector2 pointer);
        Rect safe = GetSafeRect(camera);
        Vector2 naturalSize = _view.Rect.rect.size;
        float scale = Mathf.Min(1, safe.width / Mathf.Max(1, naturalSize.x), safe.height / Mathf.Max(1, naturalSize.y));
        _view.Rect.localScale = Vector3.one * scale;
        Vector2 size = naturalSize * scale;
        Vector2 target = CalculateTarget(pointer, size, safe, _cursorGap);
        float blend = 1 - Mathf.Exp(-_followSpeed * Mathf.Max(0, deltaTime));
        Vector2 position = snap ? target : Vector2.Lerp(_view.Rect.anchoredPosition, target, blend);
        // Clamp the interpolated position too, including on resize and changes of content height.
        _view.Rect.anchoredPosition = ClampCenter(position, size, safe);
    }

    public static Vector2 CalculateTarget(Vector2 pointer, Vector2 size, Rect bounds, float gap)
    {
        float x = pointer.x - gap - size.x * 0.5f;
        if (pointer.x - gap - size.x < bounds.xMin)
        {
            x = pointer.x + gap + size.x * 0.5f;
        }

        return ClampCenter(new Vector2(x, pointer.y - size.y * 0.5f + 12), size, bounds);
    }

    private static Vector2 ClampCenter(Vector2 center, Vector2 size, Rect bounds)
    {
        return new Vector2(
            Mathf.Clamp(center.x, bounds.xMin + size.x * 0.5f, bounds.xMax - size.x * 0.5f),
            Mathf.Clamp(center.y, bounds.yMin + size.y * 0.5f, bounds.yMax - size.y * 0.5f));
    }

    private Rect GetSafeRect(Camera camera)
    {
        Rect screenSafe = Screen.safeArea;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenSafe.min, camera, out Vector2 min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenSafe.max, camera, out Vector2 max);
        Rect canvasBounds = _canvasRect.rect;
        min = Vector2.Max(min, canvasBounds.min) + Vector2.one * _screenMargin;
        max = Vector2.Min(max, canvasBounds.max) - Vector2.one * _screenMargin;
        return Rect.MinMaxRect(min.x, min.y, Mathf.Max(min.x + 1, max.x), Mathf.Max(min.y + 1, max.y));
    }

    private bool EnsureView()
    {
        if (_view != null)
        {
            return true;
        }

        _canvas = GetComponent<Canvas>();
        _canvasRect = (RectTransform)_canvas.transform;
        if (_prefab == null)
        {
            _prefab = Resources.Load<TooltipView>("UI/ItemTooltip");
        }

        if (_prefab == null)
        {
            Debug.LogError("ItemTooltip prefab is missing from Resources/UI.", this);
            return false;
        }

        _view = Instantiate(_prefab, _canvasRect);
        _view.Rect.anchorMin = _canvasRect.pivot;
        _view.Rect.anchorMax = _canvasRect.pivot;
        _view.gameObject.SetActive(false);
        return true;
    }

    private void LateUpdate()
    {
        UpdatePosition(Input.mousePosition, Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        HideAll();
    }
}
