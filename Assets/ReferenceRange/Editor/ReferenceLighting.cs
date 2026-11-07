using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ReferenceRange.Editor
{
    internal static class ReferenceLighting
    {
        private const string Root = ReferenceSceneBuilder.Root;

        [MenuItem("Tools/Reference Range/Apply Reference Daylight")]
        private static void ApplyToOpenRange()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ReferenceSceneBuilder.ScenePath)
                throw new System.InvalidOperationException("Open ConcreteRange before applying its daylight.");
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        internal static void Apply(Scene scene)
        {
            var sun = RenderSettings.sun;
            if (sun == null)
                sun = new GameObject("Afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40f, 214f, 0f);
            sun.color = new Color(1f, .963f, .897f);
            sun.intensity = 2.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .96f;
            sun.shadowBias = .025f;
            sun.shadowNormalBias = .17f;
            RenderSettings.sun = sun;

            var panorama = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Environment/ReferenceDaylight.hdr");
            var sky = RenderSettings.skybox;
            if (panorama != null && sky != null)
            {
                sky.SetTexture("_MainTex", panorama);
                sky.SetFloat("_Exposure", 1.53f);
                sky.SetFloat("_Rotation", 91.7f);
                EditorUtility.SetDirty(sky);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.45f, .54f, .67f);
            RenderSettings.ambientEquatorColor = new Color(.30f, .36f, .45f);
            RenderSettings.ambientGroundColor = new Color(.21f, .20f, .18f);
            RenderSettings.reflectionIntensity = .86f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.67f, .73f, .77f);
            RenderSettings.fogStartDistance = 72f;
            RenderSettings.fogEndDistance = 150f;

            var roots = scene.GetRootGameObjects();
            var bounce = roots.FirstOrDefault(root => root.name == "Cool skylight bounce")
                ?? new GameObject("Cool skylight bounce");
            var fill = bounce.GetComponent<Light>() ?? bounce.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(16f, -17f, 0f);
            fill.color = new Color(.78f, .85f, 1f);
            fill.intensity = .20f;
            fill.shadows = LightShadows.None;

            var walls = roots.First(root => root.name == "Test walls").transform;
            var left = walls.Find("Near left block");
            left.localPosition = new Vector3(4.35f, 2.08f, 11f);
            left.localScale = new Vector3(left.localScale.x, left.localScale.y, .23f);
            var right = walls.Find("Near right block");
            right.localPosition = new Vector3(10.01f, 2.08f, 11f);
            right.localScale = new Vector3(right.localScale.x, right.localScale.y, .23f);
            var renderer = right.GetComponent<Renderer>();
            const string wallPath = Root + "/Materials/Shadow wall concrete.mat";
            var shadeWall = AssetDatabase.LoadAssetAtPath<Material>(wallPath);
            if (shadeWall == null)
            {
                shadeWall = new Material(renderer.sharedMaterial);
                AssetDatabase.CreateAsset(shadeWall, wallPath);
            }
            shadeWall.SetColor("_BaseColor", new Color(.23f, .255f, .29f));
            renderer.sharedMaterial = shadeWall;
            EditorUtility.SetDirty(shadeWall);

            var daylight = roots.FirstOrDefault(root => root.name == "Reference daylight grade")
                ?? new GameObject("Reference daylight grade");
            var volume = daylight.GetComponent<Volume>() ?? daylight.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = DaylightProfile();
            DynamicGI.UpdateEnvironment();
        }

        private static VolumeProfile DaylightProfile()
        {
            const string path = Root + "/Environment/ReferenceDaylightProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.AddObjectToAsset(profile.Add<Tonemapping>(), profile);
                AssetDatabase.AddObjectToAsset(profile.Add<ColorAdjustments>(), profile);
                AssetDatabase.AddObjectToAsset(profile.Add<Bloom>(), profile);
            }

            profile.TryGet(out Tonemapping tone);
            tone.mode.Override(TonemappingMode.Neutral);
            profile.TryGet(out ColorAdjustments grade);
            grade.postExposure.Override(-.15f);
            grade.contrast.Override(8f);
            grade.saturation.Override(-4f);
            profile.TryGet(out Bloom bloom);
            bloom.threshold.Override(1.02f);
            bloom.intensity.Override(.17f);
            bloom.scatter.Override(.58f);
            EditorUtility.SetDirty(tone);
            EditorUtility.SetDirty(grade);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
