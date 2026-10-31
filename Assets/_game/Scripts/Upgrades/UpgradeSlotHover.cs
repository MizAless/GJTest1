using UnityEngine;
using UnityEngine.EventSystems;

public class UpgradeSlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RunDirector _run;
    private StackedUpgradeTooltip _tooltips;
    private int _slot;

    public void Initialize(RunDirector run, StackedUpgradeTooltip tooltips, int slot)
    {
        _run = run;
        _tooltips = tooltips;
        _slot = slot;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _tooltips.Show(this, _run.Drum.Cartridges[_slot], _run.Drum.Sockets[_slot]);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tooltips.Hide(this);
    }

    private void OnDisable()
    {
        if (_tooltips != null)
        {
            _tooltips.Hide(this);
        }
    }
}
