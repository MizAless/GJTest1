using UnityEngine;

public class PixelCanvasWatcher : MonoBehaviour
{
    private void OnTransformChildrenChanged()
    {
        PixelUiTheme.RequestRefresh();
    }

    private void OnEnable()
    {
        PixelUiTheme.RequestRefresh();
    }
}
