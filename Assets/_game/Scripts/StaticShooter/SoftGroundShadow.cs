using UnityEngine;

public static class SoftGroundShadow
{
    private static Sprite _sprite;

    public static SpriteRenderer Create(Transform parent, Vector3 position, Vector2 size, int order)
    {
        if (_sprite == null)
        {
            const int resolution = 64;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.name = "Soft radial ground shadow";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Vector2 point = new Vector2((x + 0.5f) / resolution, (y + 0.5f) / resolution);
                    float distance = (point - Vector2.one * 0.5f).magnitude * 2;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1 - distance), 1.4f) * 0.65f;
                    texture.SetPixel(x, y, new Color(0.07f, 0.035f, 0.025f, alpha));
                }
            }
            texture.Apply();
            _sprite = Sprite.Create(texture, new Rect(0, 0, resolution, resolution), Vector2.one * 0.5f, resolution);
        }
        GameObject root = new GameObject("Soft ground shadow", typeof(SpriteRenderer));
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        root.transform.localScale = new Vector3(size.x, size.y, 1);
        SpriteRenderer shadow = root.GetComponent<SpriteRenderer>();
        shadow.sprite = _sprite;
        shadow.sortingOrder = order;
        return shadow;
    }
}
