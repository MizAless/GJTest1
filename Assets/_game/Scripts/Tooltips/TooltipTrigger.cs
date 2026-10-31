using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TooltipContent _content = new TooltipContent();
    private Func<TooltipContent> _provider;
    private TooltipPresenter _presenter;

    public void Bind(Func<TooltipContent> provider)
    {
        _provider = provider;
    }

    public void SetContent(TooltipContent content)
    {
        _content = content;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        _presenter = TooltipPresenter.For(canvas);
        _presenter.Show(this, _provider == null ? _content : _provider(), eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Hide();
    }

    private void OnDisable()
    {
        Hide();
    }

    private void Hide()
    {
        if (_presenter != null)
        {
            _presenter.Hide(this);
        }
    }
}
