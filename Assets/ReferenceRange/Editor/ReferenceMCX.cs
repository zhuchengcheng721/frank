using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ReferenceRange.Editor
{
    internal static partial class ReferenceModelBuilder
    {
        private const string MCXRoot = "Assets/DelthorGames/MCX/";
        private const string MCXPrefab = MCXRoot + "Prefabs/MCX.prefab";

        static partial void AttachStoreWeapon(Transform driver, Transform body, ref Transform muzzle, ref Transform sight)
        {
            if (AttachMK18(driver, body, ref muzzle, ref sight))
                return;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MCXPrefab);
            if (asset == null)
                return;

            var donorGun = body.parent;
            var magDriver = Find(donorGun, "clip");
            var boltDriver = Find(donorGun, "bolt");
            var oldMag = PartContact(magDriver, "Original magazine contact",
                BoundsFor(Find(magDriver, "MagazineGeometry")).center, -.027f);
            RangeWeaponMount.HideGeometry(donorGun);

            var mcx = Unpack(asset, driver).transform;
            mcx.name = "MCX • downloaded model";
            var marker = Marker(body, "Firing hand reference", new Vector3(0f, .038f, .075f), Vector3.forward);
            var receiverFrame = WeaponMountFrame.FromMarkers(body, marker, muzzle, muzzle);
            var weaponFrame = new WeaponMountFrame(new Vector3(0f, -.76f, -3.44f), Vector3.forward, Vector3.up, 10.44f);
            RangeWeaponMount.Fit(mcx, weaponFrame, body, receiverFrame);
            var weaponMat = MCXMaterial();
            foreach (var renderer in mcx.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = Enumerable.Repeat(weaponMat, renderer.sharedMaterials.Length).ToArray();

            var mag = Find(mcx, "Cube.002_low");
            var magContact = PartContact(mag, "MCX magazine contact", BoundsFor(mag).center, -.019f);
            RangeWeaponMount.BindPart(mag, magDriver);

            var charge = Find(mcx, "Cube.010_low");
            Bounds handleBounds = BoundsFor(charge);
            var handle = new GameObject("MCX charging handle").transform;
            handle.SetParent(mcx, false);
            var latchPosition = new Vector3(handleBounds.min.x + .014f,
                handleBounds.center.y + .003f, handleBounds.min.z + .018f);
            var chargeContact = Marker(handle, "MCX left latch grip", latchPosition, Vector3.forward);
            foreach (var name in new[] { "Cube.010_low", "Cube.011_low", "Cube.012_low" })
                RangeWeaponMount.BindPart(Find(mcx, name), handle);
            RangeWeaponMount.BindPart(Find(mcx, "Cylinder.007_low"), boltDriver);

            muzzle = RangeWeaponMount.CreateMarker(mcx, "MCX muzzle", new Vector3(0f, 1.4f, 7.02f), Vector3.forward, Vector3.up);
            sight = MountOptic(mcx);

            var grip = driver.parent.gameObject.AddComponent<RangeGripRetarget>();
            grip.Configure(Find(driver, "L_arm_01"), Find(driver, "L_elbow_02"), Find(driver, "L_wrist_03"),
                oldMag, magContact, replacementCharge: chargeContact);
            grip.SetChargeContact(Find(driver, "L_point3_09"), Find(driver, "L_thumb3_00"));
            grip.SetChargeTravel(handle, Vector3.back * 1.70f);
            var slap = RangeWeaponMount.CreateMarker(mcx, "MCX handguard slap",
                new Vector3(-.67f, 1.68f, 2.75f), Vector3.forward, Vector3.up);
            grip.SetReloadPresentation(mag, slap, Find(driver, "L_middle1_011"));
        }

        private static Transform MountOptic(Transform mcx)
        {
            const float opticLength = .112f;
            var opticAsset = AssetDatabase.LoadAssetAtPath<GameObject>(OpticFolder + "VectorReflexOptic.obj");
            if (opticAsset == null)
                return RangeWeaponMount.CreateMarker(mcx, "Iron sight", new Vector3(0, 2.74f, -1.08f), Vector3.forward, Vector3.up);

            var optic = Unpack(opticAsset, mcx).transform;
            optic.name = "Micro reflex sight";
            var bounds = BoundsFor(optic);
            optic.localScale *= opticLength / Mathf.Max(.01f, bounds.size.z);
            // The asset has its bore at local origin. Raise it until its mount meets the receiver rail.
            optic.localPosition = new Vector3(0f, 3.22f, -1.16f);
            FinishOptic(optic);
            return RangeWeaponMount.CreateMarker(optic, "Optic center", Vector3.zero, Vector3.forward, Vector3.up);
        }

        private static void FinishOptic(Transform optic)
        {
            var opticMat = Lit("Reflex sight", Color.white);
            opticMat.SetTexture("_BaseMap",
                AssetDatabase.LoadAssetAtPath<Texture2D>(OpticFolder + "VectorReflexOptic_BaseColor.png"));
            opticMat.SetFloat("_Metallic", .24f);
            opticMat.SetFloat("_Smoothness", .46f);
            foreach (var renderer in optic.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = Enumerable.Repeat(opticMat, renderer.sharedMaterials.Length).ToArray();
            EditorUtility.SetDirty(opticMat);
        }

        private static Material MCXMaterial()
        {
            var textures = MCXRoot + "Textures/";
            var albedo = LoadWeaponTexture(textures + "MCX_MatBlack_AlbedoTransparency.png", sRGB: true);
            var normal = LoadWeaponTexture(textures + "MCX_Mat_NormalOpenGL.png", normal: true);
            var metal = LoadWeaponTexture(textures + "MCX_Mat_MetallicSmoothness.png", packedSmoothness: true);
            var occlusion = LoadWeaponTexture(textures + "MCX_Mat_AO.png");
            var height = LoadWeaponTexture(textures + "MCX_Mat_Height.png");

            var mat = Lit("MCX graphite PBR", new Color(.98f, .97f, .96f));
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1.08f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", metal);
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Smoothness", .84f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetTexture("_OcclusionMap", occlusion);
            mat.SetFloat("_OcclusionStrength", .88f);
            mat.EnableKeyword("_OCCLUSIONMAP");
            mat.SetTexture("_ParallaxMap", height);
            mat.SetFloat("_Parallax", .006f);
            mat.EnableKeyword("_PARALLAXMAP");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Texture2D LoadWeaponTexture(string path, bool sRGB = false, bool normal = false, bool packedSmoothness = false)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                var alpha = packedSmoothness ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                if (importer.textureType != type || importer.sRGBTexture != sRGB || importer.alphaSource != alpha
                    || importer.filterMode != FilterMode.Trilinear || importer.anisoLevel != 8
                    || importer.textureCompression != TextureImporterCompression.CompressedHQ)
                {
                    importer.textureType = type;
                    importer.sRGBTexture = sRGB;
                    importer.alphaSource = alpha;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 8;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material Lit(string name, Color tint)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", tint);
            return mat;
        }

        private static Bounds BoundsFor(Transform part)
        {
            var surfaces = part.GetComponentsInChildren<Renderer>(true);
            var result = surfaces[0].bounds;
            foreach (var surface in surfaces)
                result.Encapsulate(surface.bounds);
            return result;
        }

        private static Transform PartContact(Transform parent, string name, Vector3 worldCenter, float xOffset)
        {
            return Marker(parent, name, worldCenter + new Vector3(xOffset, 0f, 0f), Vector3.forward);
        }
    }
}
