using UnityEngine;

namespace RangePrototype
{
    public sealed class RangeTarget : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private static readonly Color HitColor = new Color(1f, 0.43f, 0.12f);
        private const float FlashDuration = 0.22f;
        private Renderer[] surfaces;
        private Color[] baseColors;
        private Color[] baseEmissions;
        private MaterialPropertyBlock flash;
        private float hitAt;

        private void Awake()
        {
            surfaces = GetComponentsInChildren<Renderer>();
            baseColors = new Color[surfaces.Length];
            baseEmissions = new Color[surfaces.Length];
            flash = new MaterialPropertyBlock();
            for (int i = 0; i < surfaces.Length; i++)
            {
                Renderer surface = surfaces[i];
                Material material = surface.sharedMaterial;
                if (material == null)
                    continue;

                surface.GetPropertyBlock(flash);
                baseColors[i] = flash.HasColor(BaseColor) ? flash.GetColor(BaseColor) : material.color;
                baseEmissions[i] = flash.HasColor(Emission) ? flash.GetColor(Emission) :
                    material.HasProperty(Emission) ? material.GetColor(Emission) : Color.black;
            }
            enabled = false;
        }

        public void RegisterHit()
        {
            hitAt = Time.time;
            ApplyFlash(1f);
            enabled = true;
        }

        private void Update()
        {
            float amount = 1f - (Time.time - hitAt) / FlashDuration;
            ApplyFlash(Mathf.Max(amount, 0f));
            if (amount <= 0f)
                enabled = false;
        }

        private void ApplyFlash(float amount)
        {
            for (int i = 0; i < surfaces.Length; i++)
            {
                Renderer surface = surfaces[i];
                if (surface == null || !surface.enabled)
                    continue;

                surface.GetPropertyBlock(flash);
                flash.SetColor(BaseColor, Color.Lerp(baseColors[i], HitColor, amount * 0.33f));
                flash.SetColor(Emission, baseEmissions[i] + HitColor * (amount * 0.4f));
                surface.SetPropertyBlock(flash);
            }
        }
    }
}
