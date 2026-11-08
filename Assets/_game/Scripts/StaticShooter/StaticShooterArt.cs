using UnityEngine;

[CreateAssetMenu(menuName = "Prototype/Static shooter art")]
public sealed class StaticShooterArt : ScriptableObject
{
    public Sprite Backdrop;
    public Sprite Ground;
    public Sprite SideRuins;
    public Sprite Foreground;
    public Sprite Hero;
    public Sprite Revolver;
    public Sprite Enemy;
    public Material GroundMaterial;
    public GameObject WesternSceneryPrefab;
    public GameObject HeroPrefab;
    public GameObject RevolverPrefab;
    public GameObject ProjectilePrefab;
}
