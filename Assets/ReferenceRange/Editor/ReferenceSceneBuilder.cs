using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ReferenceRange.Editor
{
    internal static class ReferenceSceneBuilder
    {
        internal const string Root = "Assets/ReferenceRange";
        internal const string ScenePath = Root + "/ConcreteRange.unity";
        private const string Materials = Root + "/Materials";

        [MenuItem("Tools/Reference Range/Create Concrete Range")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return;

            if (!AssetDatabase.IsValidFolder(Materials))
                AssetDatabase.CreateFolder(Root, "Materials");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            ConfigureSky();

            var pad = new GameObject("Concrete pad").transform;
            var walls = new GameObject("Test walls").transform;
            var props = new GameObject("Steel targets").transform;

            var smooth = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Environment/ConcreteColor.jpg");
            var normal = NormalMap(Root + "/Environment/ConcreteNormal.jpg");
            var ground = MaterialFor("Slab concrete", new Color(.73f, .75f, .78f), .08f, smooth, normal, new Vector2(2.5f, 2.5f));
            var wall = MaterialFor("Wall concrete", new Color(.66f, .68f, .69f), .13f, smooth, normal, new Vector2(3f, 2.2f));
            var buff = MaterialFor("Ochre concrete", new Color(.80f, .67f, .38f), .15f, smooth, normal, new Vector2(2.8f, 2.3f));
            var seams = MaterialFor("Slab joints", new Color(.27f, .30f, .31f), .08f);
            var steel = MaterialFor("Blasted steel", new Color(.39f, .43f, .46f), .44f, metal: .72f);
            var darkSteel = MaterialFor("Steel stand", new Color(.18f, .19f, .21f), .33f, metal: .68f);
            var plateFace = MaterialFor("Off-white target paint", new Color(.75f, .72f, .64f), .31f, metal: .34f);
            var ring = MaterialFor("Target ring", new Color(.22f, .25f, .26f), .23f, metal: .32f);
            var red = MaterialFor("Target center", new Color(.60f, .19f, .12f), .22f, metal: .31f);

            Box(pad, "Pad footing", new Vector3(0, -.36f, 10.8f), new Vector3(43.3f, .63f, 39.8f), seams, true);
            const float tile = 4.3f;
            for (int ix = -4; ix <= 4; ix++)
            for (int iz = -2; iz <= 6; iz++)
            {
                float x = ix * tile;
                float z = iz * tile;
                var slab = Box(pad, "Concrete paving", new Vector3(x, -.06f, z),
                    new Vector3(tile - .019f, .12f, tile - .019f), ground, true);
                slab.GetComponent<MeshRenderer>().lightProbeUsage = LightProbeUsage.Off;
            }

            Box(walls, "Near left block", new Vector3(-3.9f, 2.08f, 10.4f), new Vector3(5.25f, 4.16f, .68f), wall, true);
            Box(walls, "Near right block", new Vector3(3.75f, 2.08f, 10.65f), new Vector3(5.25f, 4.16f, .68f), wall, true);
            Box(walls, "Buff test wall", new Vector3(8.9f, 1.95f, 15.6f), new Vector3(5.25f, 3.90f, .61f), buff, true);
            Box(walls, "Far concrete", new Vector3(-.3f, 2.28f, 24.6f), new Vector3(18.4f, 4.56f, .69f), wall, true);

            TestPlate(props, "Center bullseye", 0f, 1.59f, 14.35f, .77f, steel, darkSteel, plateFace, ring, red);
            TestPlate(props, "Left bullseye", -6.75f, 1.46f, 8.8f, .65f, steel, darkSteel, plateFace, ring, red);
            TestPlate(props, "Right bullseye", 6.9f, 1.78f, 12.5f, .85f, steel, darkSteel, plateFace, ring, red);
            TestPlate(props, "Long plate", 1.75f, 1.84f, 23.1f, .88f, steel, darkSteel, plateFace, ring, red);
            Box(props, "Steel penetration test face", new Vector3(-10.1f, 1.27f, 11.9f),
                new Vector3(1.32f, 2.26f, .08f), steel, true);

            ReferenceLighting.Apply(scene);

            var player = new GameObject("First-person operator");
            player.AddComponent<RangeImpactEffects>();
            player.AddComponent<FpsRange>();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save the concrete shooting range.");
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureSky()
        {
            var panorama = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Environment/OutdoorSky.hdr");
            var sky = new Material(Shader.Find(panorama != null ? "Skybox/Panoramic" : "Skybox/Procedural"));
            if (panorama != null)
            {
                sky.SetTexture("_MainTex", panorama);
                sky.SetFloat("_Exposure", .77f);
                sky.SetFloat("_Rotation", 125f);
            }
            else
            {
                sky.SetColor("_SkyTint", new Color(.48f, .57f, .70f));
                sky.SetColor("_GroundColor", new Color(.37f, .38f, .38f));
                sky.SetFloat("_AtmosphereThickness", .82f);
            }

            AssetDatabase.CreateAsset(sky, Materials + "/Afternoon sky.mat");
            RenderSettings.skybox = sky;
        }

        private static Texture2D NormalMap(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer
                && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material MaterialFor(string name, Color color, float roughSpec,
            Texture2D map = null, Texture2D normal = null, Vector2 tiling = default, float metal = 0f)
        {
            var result = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            result.SetColor("_BaseColor", color);
            result.SetFloat("_Metallic", metal);
            result.SetFloat("_Smoothness", roughSpec);
            if (map != null)
            {
                result.SetTexture("_BaseMap", map);
                result.SetTextureScale("_BaseMap", tiling == default ? Vector2.one : tiling);
            }

            if (normal != null)
            {
                result.SetTexture("_BumpMap", normal);
                result.SetFloat("_BumpScale", .42f);
                result.EnableKeyword("_NORMALMAP");
            }

            AssetDatabase.CreateAsset(result, Materials + "/" + name + ".mat");
            return result;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 dimensions, Material material,
            bool collider)
        {
            return Shape(parent, PrimitiveType.Cube, name, position, dimensions, material, collider);
        }

        private static GameObject Shape(Transform parent, PrimitiveType type, string name, Vector3 position,
            Vector3 dimensions, Material material, bool collision)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = dimensions;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision)
                Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        private static void TestPlate(Transform parent, string name, float x, float y, float z, float diameter,
            Material steel, Material stand, Material face, Material ring, Material red)
        {
            Box(parent, "Target stem", new Vector3(x, .5f * y, z + .14f),
                new Vector3(.078f, y, .095f), stand, true);
            Box(parent, "Target foot", new Vector3(x, .054f, z + .16f),
                new Vector3(.74f, .108f, .53f), stand, true);

            var target = new GameObject(name).transform;
            target.SetParent(parent, false);
            target.localPosition = new Vector3(x, y, z);
            Disc(target, "Steel silhouette", 0, diameter, .066f, steel);
            Disc(target, "Painted face", -.036f, diameter * .92f, .010f, face);
            Disc(target, "Outer ring", -.043f, diameter * .70f, .006f, ring);
            Disc(target, "Inner paint", -.048f, diameter * .57f, .005f, face);
            Disc(target, "Scoring ring", -.052f, diameter * .37f, .004f, ring);
            Disc(target, "Red center", -.055f, diameter * .21f, .004f, red);
            var hit = Disc(target, "Front hit surface", -.058f, diameter, .003f, steel);
            hit.GetComponent<Renderer>().enabled = false;
            hit.AddComponent<MeshCollider>().sharedMesh = hit.GetComponent<MeshFilter>().sharedMesh;
            target.gameObject.AddComponent<RangeTarget>();
        }

        private static GameObject Disc(Transform parent, string name, float z, float diameter, float depth, Material material)
        {
            var obj = Shape(parent, PrimitiveType.Cylinder, name, new Vector3(0, 0, z),
                new Vector3(diameter, depth * .5f, diameter), material, false);
            obj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return obj;
        }
    }
}
