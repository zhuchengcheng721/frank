using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RangePrototype.Editor
{
    internal static class RangeModelBuilder
    {
        private const string Root = "Assets/RangePrototype";
        private const string Source = Root + "/Models/Delthor/";
        private const string Wr = Root + "/Models/WRAD/";
        private const string PrefabFolder = Root + "/Resources/RangeModels";

        [MenuItem("Tools/Range Prototype/Bind Downloaded Models")]
        public static void Build()
        {
            var body = Required(Source + "VectorBody.obj");
            var mag = Required(Source + "VectorMagazine.obj");
            var armsAsset = Required(Wr + "arms.fbx");
            var rig = new GameObject("FirstPersonRig");
            rig.hideFlags = HideFlags.HideInHierarchy;
            try
            {
                var root = rig.transform;
                var weapon = Node(root, "Weapon", Vector3.zero);
                var receiver = InstantiateModel(body, weapon, "Vector body");
                var magazineMesh = InstantiateModel(mag, weapon, "Vector magazine");
                magazineMesh.localPosition = new Vector3(0f, -.06391f, .18914f);
                var handguard = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "VectorTrim.obj");
                var trim = handguard != null ? InstantiateModel(handguard, weapon, "Vector trim") : null;
                var chargeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "VectorHandle.obj");
                var handle = chargeAsset != null ? InstantiateModel(chargeAsset, weapon, "Vector charging hardware") : null;

                ApplyMaterial(receiver, MaterialAt("Vector satin receiver", "#716B60", .34f, .31f));
                ApplyMaterial(magazineMesh, MaterialAt("Vector magazine", "#333438", .20f, .26f));
                if (trim != null)
                    ApplyMaterial(trim, MaterialAt("Vector graphite hardware", "#323538", .27f, .26f));
                if (handle != null)
                    ApplyMaterial(handle, MaterialAt("Vector graphite hardware", "#323538", .27f, .26f));

                var weaponBounds = BoundsOf(weapon);
                if (weaponBounds.size.z <= 0.001f)
                    throw new InvalidOperationException("The downloaded Vector parts have invalid dimensions.");

                float scale = .57f / weaponBounds.size.z;
                foreach (Transform part in weapon)
                {
                    part.localPosition *= scale;
                    part.localScale *= scale;
                }

                weaponBounds = BoundsOf(weapon);
                var magBounds = BoundsOf(magazineMesh);
                Vector3 centerOffset = new Vector3(-weaponBounds.center.x, -weaponBounds.max.y + .076f,
                    -magBounds.center.z + .150f);
                foreach (Transform part in weapon)
                    part.localPosition += centerOffset;

                weaponBounds = BoundsOf(weapon);
                magBounds = BoundsOf(magazineMesh);
                var magPivot = Node(weapon, "Magazine", new Vector3(magBounds.center.x, magBounds.max.y, magBounds.center.z));
                magazineMesh.SetParent(magPivot, true);
                if (handle != null)
                {
                    var handlePivot = Node(weapon, "ChargingHandle", BoundsOf(handle).center);
                    handle.SetParent(handlePivot, true);
                }

                Node(weapon, "Muzzle", new Vector3(0f, .0173f, .4256f) * scale + centerOffset);
                Node(weapon, "Sight", new Vector3(0f, weaponBounds.max.y + .014f, weaponBounds.center.z - .040f));
                var leftWrist = Node(root, "LeftArm", new Vector3(-.029f, -.018f, .265f) * scale + centerOffset);
                var rightWrist = Node(root, "RightArm", new Vector3(.032f, -.045f, .035f) * scale + centerOffset);
                var magGrip = Node(weapon, "MagazineGrip",
                    new Vector3(magBounds.min.x - .014f, magBounds.center.y + .021f, magBounds.center.z));
                Vector3 chargingContact = handle != null
                    ? BoundsOf(handle).center + new Vector3(-.018f, .008f, 0f)
                    : new Vector3(-.042f, weaponBounds.max.y - .055f, magBounds.center.z + .099f);
                var chargeGrip = Node(weapon, "ChargingGrip", chargingContact);

                var arms = Node(root, "ArmsRig", new Vector3(-.205f, -.153f, -.285f));
                var hands = InstantiateModel(armsAsset, arms, "WRAD arms");
                hands.localRotation = Quaternion.Euler(0f, 180f, 0f) * hands.localRotation;
                var bones = hands.GetComponentsInChildren<Transform>(true);
                var upper = Bone(bones, "bicep.l");
                var elbow = Bone(bones, "forearm.l");
                var wrist = Bone(bones, "wrist.l");
                float reach = Vector3.Distance(upper.position, elbow.position) + Vector3.Distance(elbow.position, wrist.position);
                hands.localScale *= .645f / reach;
                foreach (var animator in hands.GetComponentsInChildren<Animator>(true))
                    animator.enabled = false;

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Wr + "arm_albedo_sleeves.png")
                    ?? AssetDatabase.LoadAssetAtPath<Texture2D>(Wr + "arm_albedo_pale.png");
                ApplyMaterial(hands, MaterialAt("WRAD sleeves and skin", "#FFFFFF", 0f, .34f, texture));
                foreach (var renderer in hands.GetComponentsInChildren<SkinnedMeshRenderer>())
                    renderer.updateWhenOffscreen = true;

                Quaternion leftOffset = BindHand(bones, leftWrist, "l", Node(arms, "Left elbow hint", new Vector3(-.29f, -.23f, .16f)),
                    new Vector3(.57f, .12f, .81f), Vector3.up);
                BindHand(bones, rightWrist, "r", Node(arms, "Right elbow hint", new Vector3(.32f, -.24f, .12f)),
                    new Vector3(-.25f, .16f, .95f), Vector3.left);
                magGrip.rotation = Quaternion.LookRotation(new Vector3(.10f, .13f, .98f), Vector3.right) * leftOffset;
                chargeGrip.rotation = Quaternion.LookRotation(new Vector3(.51f, -.51f, .67f), Vector3.right) * leftOffset;

                if (!AssetDatabase.IsValidFolder(PrefabFolder))
                    AssetDatabase.CreateFolder(Root + "/Resources", "RangeModels");

                rig.hideFlags = HideFlags.None;
                var prefab = PrefabUtility.SaveAsPrefabAsset(rig, PrefabFolder + "/FirstPersonRig.prefab");
                if (prefab == null)
                    throw new InvalidOperationException("Unity could not save the downloaded weapon and arms rig.");
                AssetDatabase.SaveAssets();
                Debug.Log("Bound Delthor Vector and WRAD arms in " + PrefabFolder);
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        private static GameObject Required(string path)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(path)
                ?? throw new InvalidOperationException("Required model is missing at " + path);
        }

        private static Transform InstantiateModel(GameObject asset, Transform parent, string name)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = name;
            return model.transform;
        }

        private static Quaternion BindHand(Transform[] bones, Transform target, string side, Transform hint, Vector3 finger, Vector3 palm)
        {
            var upper = Bone(bones, "bicep." + side);
            var elbow = Bone(bones, "forearm." + side);
            var wrist = Bone(bones, "wrist." + side);
            var socket = Bone(bones, "socket." + side);
            var middle = Bone(bones, "finger_middle1." + side);
            Vector3 originalFinger = (middle.position - wrist.position).normalized;
            Vector3 originalPalm = Vector3.ProjectOnPlane(socket.position - wrist.position, originalFinger).normalized;
            Quaternion offset = Quaternion.Inverse(Quaternion.LookRotation(originalFinger, originalPalm)) * socket.rotation;
            target.rotation = Quaternion.LookRotation(finger, palm) * offset;
            target.gameObject.AddComponent<RangeArmRig>().Configure(upper, elbow, wrist, hint, socket);
            return offset;
        }

        private static Transform Bone(Transform[] bones, string name)
        {
            return Array.Find(bones, bone => bone.name == name)
                ?? throw new InvalidOperationException("Downloaded arms missing bone " + name);
        }

        private static Transform Node(Transform parent, string name, Vector3 position)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = position;
            return node;
        }

        private static Bounds BoundsOf(Transform node)
        {
            var renderers = node.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void ApplyMaterial(Transform part, Material material)
        {
            foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
        }

        private static Material MaterialAt(string name, string hex, float metallic, float smoothness, Texture2D texture = null)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            ColorUtility.TryParseHtmlString(hex, out var color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
