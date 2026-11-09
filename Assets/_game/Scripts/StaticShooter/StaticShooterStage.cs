using System.Collections.Generic;
using UnityEngine;

// Perspective camera and world-space sprites: gameplay uses real X/Y/Z positions.
[DefaultExecutionOrder(-200)]
public sealed class StaticShooterStage : MonoBehaviour
{
    public static StaticShooterStage Instance { get; private set; }
    [SerializeField] private StaticShooterArt _art;
    [SerializeField] private bool _autoAim = true;
    private Camera _camera;
    private Shooter _shooter;
    private Transform _hero;
    public Transform CollectionTarget { get; private set; }
    private RevolverVisual _weapon;
    private Vector2 _look;
    public Transform GroundSurface { get; private set; }
    public bool AutoAim => _autoAim;
    public Sprite EnemySprite => _art.Enemy;
    public Vector3 ShotOrigin => _weapon.Muzzle.position;
    public RevolverVisual Weapon => _weapon;
    public ProjectileVisual ProjectilePrefab => _art.ProjectilePrefab.GetComponent<ProjectileVisual>();
    public Vector3 AimPoint => AimAtScreen(Input.mousePosition);

    public Vector3 AimAtScreen(Vector2 screenPosition)
    {
        Ray ray = _camera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 150, 1 << 0, QueryTriggerInteraction.Collide))
        {
            return hit.point;
        }
        return ray.GetPoint(75);
    }

    public void SetAutoAim(bool enabled)
    {
        _autoAim = enabled;
    }

    private void Awake()
    {
        Instance = this;
        _camera = Camera.main;
        _camera.orthographic = false;
        _camera.fieldOfView = 50;
        _camera.farClipPlane = 180;
        _camera.transform.SetPositionAndRotation(new Vector3(0, 4, -10),
            Quaternion.LookRotation(new Vector3(0, -2.4f, 32)));
        _camera.backgroundColor = new Color(0.55f, 0.29f, 0.23f);
        foreach (Renderer oldRenderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (oldRenderer is SpriteRenderer || oldRenderer is MeshRenderer)
            {
                oldRenderer.enabled = false;
            }
        }
        _shooter = FindAnyObjectByType<Shooter>();
        _shooter.transform.position = new Vector3(0, 0, -5);
        foreach (Collider2D collider in _shooter.GetComponents<Collider2D>())
        {
            collider.enabled = false;
        }
        CreateSprite("Sunset fortress", _art.Backdrop, new Vector3(0, 38, 100), new Vector2(210, 118), -300);
        if (_art.GroundMaterial != null)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Courtyard ground XZ";
            ground.transform.SetParent(transform, false);
            ground.transform.position = new Vector3(0, -0.02f, 40);
            ground.transform.localScale = new Vector3(12, 1, 12);
            GroundSurface = ground.transform;
            ground.GetComponent<Renderer>().sharedMaterial = _art.GroundMaterial;
            Destroy(ground.GetComponent<Collider>());
        }
        else
        {
            SpriteRenderer ground = CreateSprite("Courtyard ground", _art.Ground,
                new Vector3(0, -0.05f, 40), new Vector2(100, 120), -290);
            ground.transform.rotation = Quaternion.Euler(90, 0, 0);
            GroundSurface = new GameObject("Blood collision plane").transform;
            GroundSurface.SetParent(transform, false);
            GroundSurface.position = ground.transform.position;
        }
        CreateSprite("Side ruins", _art.SideRuins, new Vector3(0, 12.1f, 23), new Vector2(43, 24.2f), -200);
        CreateSprite("Near barrels and fence", _art.Foreground, new Vector3(0, 6.47f, 2), new Vector2(23, 12.94f), 190);
        if (_art.WesternSceneryPrefab != null)
        {
            Instantiate(_art.WesternSceneryPrefab, transform, false);
        }
        _hero = Instantiate(_art.HeroPrefab, _camera.transform).transform;
        _hero.localRotation = Quaternion.identity;
        CollectionTarget = new GameObject("Loot collection target").transform;
        CollectionTarget.SetParent(_hero, false);
        CollectionTarget.localPosition = new Vector3(0.3f, 0.6f, 0);
        GunslingerVisual character = _hero.GetComponent<GunslingerVisual>();
        _weapon = Instantiate(_art.RevolverPrefab, character.WeaponSocket, false).GetComponent<RevolverVisual>();
        AddSceneryShadow(new Vector3(-10, 0, 16), new Vector2(9, 5));
        AddSceneryShadow(new Vector3(10, 0, 16), new Vector2(9, 5));
        AddSceneryShadow(new Vector3(-7, 0, 2), new Vector2(7, 4));
        AddSceneryShadow(new Vector3(7, 0, 2), new Vector2(7, 4));
        AddSceneryShadow(new Vector3(-2, 0, -4), new Vector2(3, 2));
    }

    private void AddSceneryShadow(Vector3 position, Vector2 size)
    {
        SpriteRenderer shadow = SoftGroundShadow.Create(transform, position + Vector3.up * 0.01f, size, -250);
        shadow.transform.rotation = Quaternion.Euler(90, 0, 0);
    }

    private SpriteRenderer CreateSprite(string label, Sprite sprite, Vector3 position, Vector2 size, int order)
    {
        GameObject layer = new GameObject(label, typeof(SpriteRenderer));
        layer.transform.SetParent(transform, false);
        layer.transform.position = position;
        SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        layer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
        return renderer;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            _autoAim = !_autoAim;
        }
        bool combat = RunDirector.Instance != null && RunDirector.Instance.CombatActive;
        Cursor.visible = !combat;
        if (!combat)
        {
            return;
        }
        Vector2 target = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width) - 0.5f,
            Input.mousePosition.y / Mathf.Max(1, Screen.height) - 0.5f);
        _look = Vector2.Lerp(_look, target, 1 - Mathf.Exp(-6 * Time.unscaledDeltaTime));
    }

    private void LateUpdate()
    {
        // A small real camera translation produces depth-dependent parallax naturally.
        _camera.transform.position = new Vector3(_look.x * 0.65f, 4 + _look.y * 0.22f, -10);
        _hero.position = _camera.ViewportToWorldPoint(new Vector3(0.20f, 0.23f, 4.8f));
        Vector3 target = ResolveAimTarget();
        _weapon.AimAt(target, _camera.transform.up);
        _weapon.UpdatePose(Time.unscaledDeltaTime);
    }

    private Vector3 ResolveAimTarget()
    {
        Enemy nearest = _autoAim ? _shooter.GetNearestEnemy() : null;
        return nearest != null ? nearest.AimPosition : AimPoint;
    }

    public Vector3 GetShotDirection()
    {
        Vector3 target = ResolveAimTarget();
        _weapon.AimAt(target, _camera.transform.up);
        return (target - ShotOrigin).normalized;
    }

    public void Recoil()
    {
        _weapon.Fire();
    }

    private void OnGUI()
    {
        GUIStyle text = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperRight };
        GUI.Label(new Rect(Screen.width - 290, 42, 270, 35), _autoAim ? "F · Автонаведение" : "F · Ручной прицел", text);
        if (RunDirector.Instance == null || !RunDirector.Instance.CombatActive)
        {
            return;
        }
        Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        GUI.color = new Color(1, 0.91f, 0.67f);
        GUI.DrawTexture(new Rect(mouse.x - 12, mouse.y, 8, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(mouse.x + 5, mouse.y, 8, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(mouse.x, mouse.y - 12, 2, 8), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(mouse.x, mouse.y + 5, 2, 8), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void OnDestroy()
    {
        Cursor.visible = true;
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
