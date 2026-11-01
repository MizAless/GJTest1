using System;
using UnityEngine;

// An archetype owns its intrinsic. Rolled affixes remain on the AmmoItem instance.
[Serializable]
public sealed class AmmoArchetype
{
    public string Name;
    [Min(0)] public float Weight = 100;
    public bool AllowStarter = true;
    [Range(0, 8)] public int VisualIndex;
    [TextArea] public string IntrinsicDescription;
    public AmmoProperties SelfProperties = new AmmoProperties();
    public AmmoTrigger IntrinsicTrigger;
    public AmmoProperties TriggerProperties = new AmmoProperties();
}
