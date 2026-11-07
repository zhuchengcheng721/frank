using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RangePrototype.Editor
{
    internal static class RangeSceneBuilder
    {
        private const string RootPath = "Assets/RangePrototype";
        private const string ScenePath = RootPath + "/CarbineRange.unity";

        [MenuItem("Tools/Range Prototype/Create Carbine Range")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.LogWarning("Carbine Range already exists. Open " + ScenePath + " to play.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(RootPath + "/Materials"))
                AssetDatabase.CreateFolder(RootPath, "Materials");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Tone("91B5D2");
            RenderSettings.ambientEquatorColor = Tone("737E8C");
            RenderSettings.ambientGroundColor = Tone("5C554D");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Tone("222D36");
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 58f;

            var shell = new GameObject("01  |  Range shell").transform;
            var equipment = new GameObject("02  |  Lane equipment").transform;
            var targets = new GameObject("03  |  Reactive plates").transform;
            var lighting = new GameObject("04  |  Lighting").transform;

            var floor = MaterialFor("Ground concrete", "535A60", 0.04f, 0.19f);
            var wall = MaterialFor("Painted concrete", "3A4550", 0.05f, 0.27f);
            var panel = MaterialFor("Acoustic baffles", "292E34", 0.02f, 0.20f);
            var frame = MaterialFor("Structural steel", "202932", 0.28f, 0.36f);
            var steel = MaterialFor("Brushed steel", "5C6971", 0.27f, 0.43f);
            var brass = MaterialFor("Signal amber", "E6AE51", 0.13f, 0.27f, 0.21f);
            var chalk = MaterialFor("Lane paint", "BDC8C6", 0.02f, 0.30f, 0.08f);
            var aqua = MaterialFor("Signal cyan", "63D8DE", 0.1f, 0.35f, 0.65f);
            var lamp = MaterialFor("Light diffuser", "D5F5FF", 0f, 0.34f, 2.3f);
            var rubber = MaterialFor("Rubber floor mat", "282F34", 0f, 0.13f);

            Box(shell, "Main floor", new Vector3(0, -.23f, 13), new Vector3(15.6f, .46f, 36), floor, true);
            Box(shell, "Roof", new Vector3(0, 5.4f, 13), new Vector3(15.6f, .35f, 36), frame, true);
            Box(shell, "Backstop wall", new Vector3(0, 2.6f, 30.4f), new Vector3(15.5f, 5.25f, .6f), wall, true);
            Box(shell, "Entrance wall", new Vector3(0, 2.6f, -4.6f), new Vector3(15.5f, 5.25f, .6f), wall, true);

            for (int side = -1; side <= 1; side += 2)
            {
                Box(shell, "Outer wall", new Vector3(side * 7.65f, 2.6f, 13), new Vector3(.35f, 5.25f, 35), wall, true);
                Box(shell, "Wall marker", new Vector3(side * 7.43f, 1.06f, 13), new Vector3(.04f, .07f, 34), aqua);
                for (int section = 0; section < 7; section++)
                {
                    float z = -2.3f + section * 5.1f;
                    Box(shell, "Wall panel", new Vector3(side * 7.40f, 2.85f, z + 2.35f), new Vector3(.12f, 3.2f, 4.67f), panel, true);
                    Box(shell, "Vertical rib", new Vector3(side * 7.28f, 2.65f, z), new Vector3(.30f, 5.3f, .22f), steel, true);
                    Box(shell, "Ceiling truss", new Vector3(side * 3.66f, 4.91f, z), new Vector3(7.32f, .27f, .23f), frame);
                    Box(shell, "Cable gutter", new Vector3(side * 6.96f, 4.12f, z + 2.55f), new Vector3(.30f, .15f, 5.12f), steel);
                }
            }

            for (int lane = -1; lane <= 1; lane++)
            {
                float x = 4.1f * lane;
                Box(equipment, "Shooting mat", new Vector3(x, .009f, .7f), new Vector3(2.36f, .018f, 3.8f), rubber, true);
                Box(equipment, "Lane edge L", new Vector3(x - 1.28f, .006f, 13.4f), new Vector3(.045f, .012f, 30f), chalk);
                Box(equipment, "Lane edge R", new Vector3(x + 1.28f, .006f, 13.4f), new Vector3(.045f, .012f, 30f), chalk);
                Box(equipment, "Lane sign frame", new Vector3(x, 3.6f, 5.6f), new Vector3(2.27f, .53f, .15f), frame);
                Box(equipment, "Lane sign trim", new Vector3(x, 3.33f, 5.51f), new Vector3(2.27f, .028f, .022f), aqua);
                Label(equipment, $"LANE  0{lane + 2}", new Vector3(x, 3.6f, 5.505f), .10f, Tone("B6F5F3"));

                for (int distance = 6; distance <= 24; distance += 6)
                {
                    Box(equipment, "Distance mark", new Vector3(x, .011f, distance), new Vector3(2.58f, .021f, .055f), distance == 6 ? brass : chalk);
                }
            }

            for (int baffle = -6; baffle <= 6; baffle++)
            {
                var backPanel = Box(shell, "Backstop deflector", new Vector3(baffle * 1.13f, 2.3f, 29.9f),
                    new Vector3(1.10f, 4.6f, .36f), baffle % 2 == 0 ? panel : frame, true);
                backPanel.localRotation = Quaternion.Euler(0f, baffle % 2 == 0 ? 12f : -12f, 0f);
            }

            Box(equipment, "Safety stripe", new Vector3(0f, .022f, 3.4f), new Vector3(14.2f, .014f, .16f), brass);
            for (int mark = -17; mark <= 17; mark++)
            {
                var dash = Box(equipment, "Stripe chevron", new Vector3(mark * .40f, .031f, 3.4f),
                    new Vector3(.10f, .006f, .22f), frame);
                dash.localRotation = Quaternion.Euler(0f, 35f, 0f);
            }

            Box(equipment, "Range identity panel", new Vector3(0f, 3.85f, 28.9f), new Vector3(8.7f, 1.07f, .17f), frame, true);
            Box(equipment, "Sign lower trim", new Vector3(0f, 3.30f, 28.80f), new Vector3(8.7f, .04f, .03f), brass);
            Label(equipment, "CARBINE   /   RANGE 02", new Vector3(0f, 3.91f, 28.80f), .23f, Tone("D2E7ED"));

            Plate(targets, 1, 0f, 1.58f, 11.5f, .92f, frame, steel, aqua, chalk);
            Plate(targets, 2, -3.9f, 1.46f, 15.7f, 1.08f, frame, steel, brass, chalk);
            Plate(targets, 3, 4.0f, 1.72f, 18.8f, 1.17f, frame, steel, aqua, chalk);
            Plate(targets, 4, 0f, 1.98f, 24.0f, 1.13f, frame, steel, brass, chalk);
            Plate(targets, 5, -3.9f, 1.95f, 26.8f, .85f, frame, steel, aqua, chalk);
            Plate(targets, 6, 4.0f, 1.50f, 26.3f, .85f, frame, steel, brass, chalk);

            for (int row = 0; row < 6; row++)
            {
                float z = -1.2f + row * 5.25f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 4.35f;
                    Box(lighting, "Fixture housing", new Vector3(x, 4.88f, z), new Vector3(2.9f, .20f, .40f), steel);
                    Box(lighting, "Diffuser", new Vector3(x, 4.76f, z), new Vector3(2.67f, .04f, .24f), lamp);
                    Point(lighting, "Ceiling fill", new Vector3(x, 4.54f, z), Tone(side == 1 ? "FFF1DB" : "B1DDFF"), 13f, 10.5f);
                }
            }

            Point(lighting, "Firing line key", new Vector3(.7f, 2.72f, 1f), Tone("D8EFFF"), 10f, 9f);
            Point(lighting, "Backstop wash", new Vector3(0f, 3.3f, 26.9f), Tone("FFE5B7"), 17f, 12f);

            var player = new GameObject("Player  |  FPS controller");
            player.AddComponent<RangeImpactEffects>();
            player.AddComponent<FpsRange>();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Unity could not save the range scene.");

            AssetDatabase.SaveAssets();
            Debug.Log("Carbine Range created at " + ScenePath);
        }

        private static Color Tone(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        private static Material MaterialFor(string name, string hex, float metallic, float smoothness, float emission = 0f)
        {
            var path = RootPath + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var color = Tone(hex);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color.linear * emission);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Transform Primitive(Transform parent, PrimitiveType type, string name, Vector3 position,
            Vector3 scale, Material material, bool collision)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision)
                Object.DestroyImmediate(primitive.GetComponent<Collider>());

            return primitive.transform;
        }

        private static Transform Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material,
            bool collision = false)
        {
            return Primitive(parent, PrimitiveType.Cube, name, position, scale, material, collision);
        }

        private static void Plate(Transform parent, int number, float x, float y, float z, float diameter,
            Material frame, Material steel, Material signal, Material chalk)
        {
            var root = new GameObject($"Target {number:00}").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(x, y, z);
            Box(parent, "Target base", new Vector3(x, .11f, z + .16f), new Vector3(1.07f, .22f, .74f), frame, true);
            Box(parent, "Target stand", new Vector3(x, y * .49f, z + .15f), new Vector3(.13f, y * .98f, .16f), steel, true);
            Box(parent, "Number tab", new Vector3(x, y - diameter * .5f - .18f, z + .02f), new Vector3(.38f, .24f, .06f), frame);
            Label(parent, number.ToString("00"), new Vector3(x, y - diameter * .5f - .18f, z - .02f), .08f, Tone("DAECEF"));

            Disc(root, "Steel plate", 0f, diameter, .092f, steel);
            var hitSurface = Disc(root, "Hit surface", -.099f, diameter, .008f, steel);
            hitSurface.GetComponent<Renderer>().enabled = false;
            var collider = hitSurface.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = hitSurface.GetComponent<MeshFilter>().sharedMesh;
            Disc(root, "Outer contrast band", -.054f, diameter * .91f, .015f, frame);
            Disc(root, "Painted face", -.066f, diameter * .81f, .012f, chalk);
            Disc(root, "Inner ring", -.077f, diameter * .57f, .010f, frame);
            Disc(root, "Bullseye", -.087f, diameter * .37f, .010f, signal);
            Disc(root, "Center", -.096f, diameter * .11f, .008f, frame);
            root.gameObject.AddComponent<RangeTarget>();
        }

        private static Transform Disc(Transform parent, string name, float z, float diameter, float depth,
            Material material)
        {
            var disc = Primitive(parent, PrimitiveType.Cylinder, name, new Vector3(0f, 0f, z),
                new Vector3(diameter, depth * .5f, diameter), material, false);
            disc.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return disc;
        }

        private static void Label(Transform parent, string value, Vector3 position, float size, Color color)
        {
            var label = new GameObject(value).AddComponent<TextMesh>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = position;
            label.text = value;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 56;
            label.characterSize = size;
            label.color = color;
        }

        private static void Point(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }
    }
}
