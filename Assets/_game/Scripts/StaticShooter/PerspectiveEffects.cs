using UnityEngine;

// Orient the existing editable particle systems for the perspective stage.
public static class PerspectiveEffects
{
    public static void Prepare(ParticleSystem effect, Vector3 origin, float scale = 1)
    {
        if (StaticShooterStage.Instance == null || effect == null)
        {
            return;
        }
        effect.transform.position = origin;
        if (effect.GetComponent<PhysicalBlood>() != null)
        {
            return;
        }
        effect.transform.rotation = Camera.main.transform.rotation;
        effect.transform.localScale *= scale;
        foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 160;
            if (renderer.renderMode == ParticleSystemRenderMode.Billboard)
            {
                renderer.alignment = ParticleSystemRenderSpace.View;
            }
        }
    }
}
