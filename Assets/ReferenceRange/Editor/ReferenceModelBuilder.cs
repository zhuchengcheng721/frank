using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ReferenceRange.Editor
{
    internal static partial class ReferenceModelBuilder
    {
        private const string Root = "Assets/ReferenceRange";
        private const string DriverFolder = Root + "/Models/AnimationSource/";
        private const string DriverPath = DriverFolder + "SMG45.fbx";
        private const string OpticFolder = Root + "/Models/Optic/";
        private const string ResourcesFolder = Root + "/Resources/ReferenceModels";
        private const string PrefabPath = ResourcesFolder + "/FirstPersonRig.prefab";

        [MenuItem("Tools/Reference Range/Bind First Person Models")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(DriverPath);
            var reload = AssetDatabase.LoadAllAssetsAtPath(DriverPath).OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == "SMG45_Reload");
            if (source == null || reload == null)
                throw new InvalidOperationException("The downloaded first-person rig or its authored reload clip is missing.");

            var rig = new GameObject("FirstPersonRig");
            try
            {
                var model = Unpack(source, rig.transform);
                var animator = model.GetComponent<Animator>();
                if (animator == null)
                    animator = model.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (animator.avatar == null)
                    animator.avatar = AssetDatabase.LoadAllAssetsAtPath(DriverPath).OfType<Avatar>().FirstOrDefault();

                var armsMat = SourceMaterial("Operator skin and cloth", "Image_0", "Image_2", "ArmsMetalSmooth", "ArmsOcclusion",
                    new Color(.86f, .85f, .82f));
                var gunMat = SourceMaterial("SMG steel", "Image_3", "Image_5", "WeaponMetalSmooth", "WeaponOcclusion", Color.white);
                var arm = Find(model.transform, "AuthoredArms").GetComponent<SkinnedMeshRenderer>();
                arm.sharedMaterials = new[] { armsMat, armsMat };
                arm.updateWhenOffscreen = true;
                foreach (var part in model.GetComponentsInChildren<MeshRenderer>(true))
                    part.sharedMaterials = Enumerable.Repeat(gunMat, part.sharedMaterials.Length).ToArray();

                reload.SampleAnimation(model, 0f);
                ReplaceOperatorArms(arm);
                var bore = Find(model.transform, "smg45");
                var body = Find(bore, "base");
                var sourceMuzzle = Find(body, "Muzzle");
                Transform muzzle = Marker(body, "Shot muzzle", sourceMuzzle.position, Vector3.forward);
                Transform sight = Marker(body, "Aim center", new Vector3(0f, .344f, .28f), Vector3.forward);
                AttachStoreWeapon(model.transform, body, ref muzzle, ref sight);

                model.transform.localScale *= .625f;
                model.transform.localPosition += new Vector3(.177f, -.303f, .368f);
                var binding = rig.AddComponent<RangeAuthoredRig>();
                binding.Configure(animator, null, null, reload, reload, sightAnchor: sight, muzzleAnchor: muzzle);
                binding.SetReloadTiming(2.26f, new Vector3(.295f, .508f, .852f), 2.44f, new Vector3(.295f, .508f, .852f));
                binding.SetChargeTiming(.95f, .65f, .59f);

                if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                    AssetDatabase.CreateFolder(Root + "/Resources", "ReferenceModels");

                if (PrefabUtility.SaveAsPrefabAsset(rig, PrefabPath) == null)
                    throw new InvalidOperationException("Unity could not save the assembled first-person rig.");
                AssetDatabase.SaveAssets();
                Debug.Log("First-person model bound to source animation: " + PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        // The store model is optional until Package Manager completes the download.
        static partial void AttachStoreWeapon(Transform driver, Transform body, ref Transform muzzle, ref Transform sight);

        private static GameObject Unpack(GameObject asset, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return instance;
        }

        private static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(part => part.name == name)
                ?? throw new InvalidOperationException("Missing part " + name);
        }

        private static Transform Marker(Transform parent, string name, Vector3 point, Vector3 forward)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.SetPositionAndRotation(point, Quaternion.LookRotation(forward, Vector3.up));
            return marker;
        }

        private static Material SourceMaterial(string name, string albedo, string normal, string metalSmooth, string occlusion, Color tint)
        {
            var textureRoot = DriverFolder + "Textures/";
            var normalPath = textureRoot + normal + ".png";
            var metalPath = textureRoot + metalSmooth + ".png";
            var aoPath = textureRoot + occlusion + ".png";
            SetTextureKind(normalPath, normal: true);
            SetTextureKind(metalPath, normal: false);
            SetTextureKind(aoPath, normal: false);

            var path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", tint);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot + albedo + ".png"));
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            mat.SetFloat("_BumpScale", .86f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(metalPath));
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Smoothness", .82f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath));
            mat.SetFloat("_OcclusionStrength", .73f);
            mat.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetTextureKind(string path, bool normal)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new InvalidOperationException("Missing PBR texture " + path);

            if (normal)
            {
                if (importer.textureType == TextureImporterType.NormalMap)
                    return;
                importer.textureType = TextureImporterType.NormalMap;
            }
            else
            {
                if (!importer.sRGBTexture)
                    return;
                importer.sRGBTexture = false;
            }

            importer.SaveAndReimport();
        }
    }
}
