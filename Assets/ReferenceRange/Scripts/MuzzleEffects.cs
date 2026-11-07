using UnityEngine;
using UnityEngine.Rendering;

namespace ReferenceRange
{
    internal sealed class MuzzleEffects
    {
        private readonly ParticleSystem flash;
        private readonly ParticleSystem smoke;
        private readonly Material flashMaterial;
        private readonly Material smokeMaterial;
        private readonly Texture2D mask;
        private readonly Light burst;
        private float lightUntil = float.NegativeInfinity;

        public MuzzleEffects(Transform parent)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                return;

            mask = CreateMask();
            flashMaterial = CreateMaterial(shader, true, new Color(3f, 2.2f, 1.5f, 1f));
            smokeMaterial = CreateMaterial(shader, false, new Color(0.56f, 0.54f, 0.50f, 0.70f));
            flash = CreateParticles(parent, "Muzzle flash", flashMaterial, true);
            smoke = CreateParticles(parent, "Muzzle smoke", smokeMaterial, false);

            var lightObject = new GameObject("Muzzle illumination");
            lightObject.transform.SetParent(parent, false);
            burst = lightObject.AddComponent<Light>();
            burst.type = LightType.Point;
            burst.color = new Color(1f, 0.66f, 0.36f);
            burst.range = 2.2f;
            burst.intensity = 2.1f;
            burst.enabled = false;
        }

        public void Emit(Vector3 position, Vector3 forward)
        {
            if (flash == null)
                return;

            flash.Emit(new ParticleSystem.EmitParams
            {
                position = position + forward * 0.030f,
                velocity = forward * 3.7f,
                startSize = Random.Range(0.095f, 0.153f),
                startLifetime = Random.Range(0.024f, 0.039f),
                startColor = new Color(1f, 0.74f, 0.42f, 1f)
            }, 1);

            for (int i = 0; i < 3; i++)
            {
                smoke.Emit(new ParticleSystem.EmitParams
                {
                    position = position + forward * (0.032f + 0.018f * i),
                    velocity = forward * Random.Range(0.35f, 1.04f) + Random.insideUnitSphere * 0.16f,
                    startSize = Random.Range(0.041f, 0.072f),
                    startLifetime = Random.Range(0.16f, 0.32f),
                    startColor = new Color(1f, 1f, 1f, Random.Range(0.11f, 0.24f)),
                    rotation = Random.Range(0f, 360f)
                }, 1);
            }

            burst.transform.position = position + forward * 0.06f;
            burst.enabled = true;
            lightUntil = Time.time + 0.042f;
        }

        public void Update()
        {
            if (burst != null && burst.enabled && Time.time >= lightUntil)
                burst.enabled = false;
        }

        private Material CreateMaterial(Shader shader, bool additive, Color color)
        {
            var material = new Material(shader) { renderQueue = (int)RenderQueue.Transparent };
            material.SetTexture("_BaseMap", mask);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return material;
        }

        private static ParticleSystem CreateParticles(Transform parent, string name, Material material, bool stretch)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.maxParticles = 26;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = stretch ? 0f : -0.12f;

            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;

            var colors = particles.colorOverLifetime;
            colors.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.5f, 0.35f), new GradientAlphaKey(0f, 1f) });
            colors.color = fade;

            if (!stretch)
            {
                var size = particles.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.50f, 1f, 2.4f));
            }

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.064f;
                renderer.lengthScale = 1.35f;
            }
            go.SetActive(true);
            particles.Play();
            return particles;
        }

        private static Texture2D CreateMask()
        {
            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Muzzle opacity", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var uv = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - uv.sqrMagnitude), 3.3f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public void Dispose()
        {
            Object.Destroy(mask);
            Object.Destroy(flashMaterial);
            Object.Destroy(smokeMaterial);
        }
    }
}
