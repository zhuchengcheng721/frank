using UnityEngine;
using UnityEngine.Rendering;

namespace RangePrototype
{
    [DisallowMultipleComponent]
    public sealed class RangeImpactEffects : MonoBehaviour
    {
        private const int MarkCapacity = 72;
        private const float SurfaceOffset = 0.0035f;

        private readonly MeshRenderer[] marks = new MeshRenderer[MarkCapacity];
        private int nextMark;
        private Transform markRoot;
        private Mesh markMesh;
        private Texture2D steelMark;
        private Texture2D concreteMark;
        private Texture2D particleMask;
        private Material steelMaterial;
        private Material concreteMaterial;
        private Material sparkMaterial;
        private Material dustMaterial;
        private ParticleSystem sparks;
        private ParticleSystem dust;

        private void Awake()
        {
            var root = new GameObject("Range impact marks");
            markRoot = root.transform;
            markMesh = CreateQuad();
            steelMark = CreateDamageTexture(true);
            concreteMark = CreateDamageTexture(false);
            particleMask = CreateParticleMask();

            Shader markShader = Shader.Find("Universal Render Pipeline/Unlit");
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            steelMaterial = CreateMaterial(markShader, steelMark, false);
            concreteMaterial = CreateMaterial(markShader, concreteMark, false);
            sparkMaterial = CreateMaterial(particleShader, particleMask, true);
            dustMaterial = CreateMaterial(particleShader, particleMask, false);
            sparkMaterial.SetColor("_BaseColor", new Color(2.7f, 2.3f, 1.6f, 1f));

            sparks = CreateParticles("Impact spark streaks", sparkMaterial, true);
            dust = CreateParticles("Impact dust", dustMaterial, false);
        }

        public void Play(RaycastHit hit, Vector3 incoming)
        {
            if (hit.collider == null)
                return;

            bool steel = IsSteel(hit.collider);
            Vector3 normal = hit.normal;
            Vector3 point = hit.point + normal * SurfaceOffset;
            Vector3 surfaceDirection = Vector3.ProjectOnPlane(incoming, normal);
            if (surfaceDirection.sqrMagnitude < 0.001f)
            {
                Vector3 reference = Mathf.Abs(normal.y) > 0.98f ? Vector3.forward : Vector3.up;
                surfaceDirection = Vector3.Cross(normal, reference);
            }
            surfaceDirection.Normalize();

            Stamp(point, normal, surfaceDirection, Mathf.Abs(Vector3.Dot(incoming.normalized, normal)), steel);
            Scatter(point + normal * 0.018f, normal, surfaceDirection, steel);
        }

        private static bool IsSteel(Collider surface)
        {
            if (surface.GetComponentInParent<RangeTarget>() != null)
                return true;

            if (!surface.TryGetComponent(out Renderer renderer) || renderer.sharedMaterial == null)
                return false;

            Material material = renderer.sharedMaterial;
            return material.HasProperty("_Metallic") && material.GetFloat("_Metallic") >= 0.24f;
        }

        private void Stamp(Vector3 point, Vector3 normal, Vector3 surfaceDirection, float incidence, bool steel)
        {
            MeshRenderer renderer = marks[nextMark];
            if (renderer == null)
            {
                var mark = new GameObject("Shot mark");
                mark.transform.SetParent(markRoot, false);
                mark.AddComponent<MeshFilter>().sharedMesh = markMesh;
                renderer = mark.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                marks[nextMark] = renderer;
            }
            nextMark = (nextMark + 1) % MarkCapacity;

            float scale = steel ? Random.Range(0.098f, 0.133f) : Random.Range(0.112f, 0.173f);
            float stretch = Mathf.Lerp(1.65f, 1f, incidence);
            Quaternion rotation = Quaternion.LookRotation(-normal, surfaceDirection);
            float roll = incidence >= 0.8f ? Random.Range(0f, 360f) : Random.Range(-22f, 22f);
            renderer.transform.SetPositionAndRotation(point, rotation * Quaternion.AngleAxis(roll, Vector3.forward));
            renderer.transform.localScale = new Vector3(scale, scale * stretch, 1f);
            renderer.sharedMaterial = steel ? steelMaterial : concreteMaterial;
        }

        private void Scatter(Vector3 point, Vector3 normal, Vector3 tangent, bool steel)
        {
            int sparkCount = steel ? Random.Range(13, 19) : Random.Range(2, 5);
            for (int i = 0; i < sparkCount; i++)
            {
                Vector3 spread = Vector3.ProjectOnPlane(Random.insideUnitSphere, normal);
                Vector3 direction = (normal * Random.Range(0.25f, 0.95f) + tangent * 0.35f + spread).normalized;
                Color color = Color.Lerp(
                    new Color(1f, 0.35f, 0.09f, steel ? 0.94f : 0.46f),
                    new Color(1f, 0.91f, 0.68f, steel ? 1f : 0.64f),
                    Random.value);
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = direction * Random.Range(2.5f, steel ? 8.4f : 4.5f),
                    startLifetime = Random.Range(0.075f, steel ? 0.24f : 0.12f),
                    startSize = Random.Range(0.012f, steel ? 0.028f : 0.018f),
                    startColor = color
                }, 1);
            }

            int dustCount = steel ? 3 : 8;
            for (int i = 0; i < dustCount; i++)
            {
                Vector3 spread = Vector3.ProjectOnPlane(Random.insideUnitSphere, normal);
                Color color = steel ? new Color(0.49f, 0.51f, 0.53f, 0.27f) : new Color(0.66f, 0.64f, 0.58f, 0.47f);
                dust.Emit(new ParticleSystem.EmitParams
                {
                    position = point + spread * 0.028f,
                    velocity = (normal * Random.Range(0.38f, 1.15f) + spread * 0.6f) * (steel ? 0.72f : 1f),
                    startLifetime = Random.Range(0.16f, steel ? 0.28f : 0.48f),
                    startSize = Random.Range(0.055f, steel ? 0.105f : 0.17f),
                    startColor = color
                }, 1);
            }
        }

        private ParticleSystem CreateParticles(string objectName, Material material, bool streak)
        {
            var effect = new GameObject(objectName);
            effect.SetActive(false);
            effect.transform.SetParent(transform, false);
            ParticleSystem particles = effect.AddComponent<ParticleSystem>();

            var main = particles.main;
            main.playOnAwake = false;
            main.maxParticles = streak ? 160 : 88;
            main.gravityModifier = streak ? 0.85f : -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;

            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.23f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(fade);

            if (!streak)
            {
                var size = particles.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.48f, 1f, 1.55f));
            }

            ParticleSystemRenderer renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = streak ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (streak)
            {
                renderer.velocityScale = 0.038f;
                renderer.lengthScale = 1.9f;
            }

            effect.SetActive(true);
            particles.Play();
            return particles;
        }

        private static Material CreateMaterial(Shader shader, Texture2D texture, bool additive)
        {
            Material material = new Material(shader);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.renderQueue = (int)RenderQueue.Transparent + (additive ? 2 : 0);
            material.mainTexture = texture;
            return material;
        }

        private static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "Range impact decal quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f),
                new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D CreateDamageTexture(bool steel)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            Color deep = steel ? new Color(0.021f, 0.024f, 0.026f) : new Color(0.09f, 0.087f, 0.08f);
            Color rim = steel ? new Color(0.49f, 0.45f, 0.39f) : new Color(0.54f, 0.50f, 0.44f);
            Color soot = steel ? new Color(0.07f, 0.066f, 0.062f) : new Color(0.20f, 0.19f, 0.17f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var uv = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float radius = uv.magnitude;
                float angle = Mathf.Atan2(uv.y, uv.x);
                float grain = Mathf.PerlinNoise(23.2f + x * 0.73f, 18.7f + y * 0.73f);
                float chipped = radius + Mathf.Sin(7f * angle + 0.7f) * 0.055f + (grain - 0.5f) * 0.16f;
                float centerRadius = steel ? 0.39f : 0.46f;

                float shadow = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(centerRadius - 0.11f, centerRadius + 0.025f, chipped));
                float edge = Mathf.Clamp01(1f - Mathf.Abs(chipped - centerRadius - 0.055f) / 0.14f);
                float glow = (0.34f + 0.66f * Mathf.Clamp01(uv.y * 1.25f + 0.52f)) * Mathf.Lerp(0.42f, 1f, grain);
                float haze = Mathf.Pow(Mathf.Clamp01(1f - radius), 1.75f) * (0.23f + grain * 0.36f);
                float chipMask = edge * glow * (1f - shadow);
                Color pixel = Color.Lerp(soot, rim, chipMask);
                pixel = Color.Lerp(pixel, deep, shadow);
                pixel.a = Mathf.Max(shadow * 0.98f, Mathf.Max(chipMask * 0.9f, haze));
                pixels[y * size + x] = pixel;
            }

            return CreateTexture(steel ? "Pitted steel" : "Chipped concrete", size, pixels, true);
        }

        private static Texture2D CreateParticleMask()
        {
            const int size = 32;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var uv = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - uv.sqrMagnitude), 2.25f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            return CreateTexture("Impact particle mask", size, pixels, false);
        }

        private static Texture2D CreateTexture(string textureName, int size, Color32[] pixels, bool mipmaps)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipmaps)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(mipmaps, true);
            return texture;
        }

        private void OnDestroy()
        {
            if (markRoot != null)
                Destroy(markRoot.gameObject);
            Destroy(markMesh);
            Destroy(steelMaterial);
            Destroy(concreteMaterial);
            Destroy(sparkMaterial);
            Destroy(dustMaterial);
            Destroy(steelMark);
            Destroy(concreteMark);
            Destroy(particleMask);
        }
    }
}
