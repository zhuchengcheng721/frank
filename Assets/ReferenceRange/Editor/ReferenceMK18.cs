using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ReferenceRange.Editor
{
    internal static partial class ReferenceModelBuilder
    {
        private const string Mk18Folder = Root + "/Models/MK18/";

        private static bool AttachMK18(Transform driver, Transform body, ref Transform muzzle, ref Transform sight)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Mk18Folder + "MK18.fbx");
            if (asset == null)
                return false;

            var donorGun = body.parent;
            var magDriver = Find(donorGun, "clip");
            var boltDriver = Find(donorGun, "bolt");
            var oldMag = PartContact(magDriver, "Original magazine contact",
                BoundsFor(Find(magDriver, "MagazineGeometry")).center, -.027f);
            RangeWeaponMount.HideGeometry(donorGun);

            var weapon = Unpack(asset, driver).transform;
            weapon.name = "MK18 • Nneako";
            var firingHand = Find(weapon, "RightHandGrip");
            var end = Find(weapon, "Muzzle");
            var rear = Find(weapon, "BoreRear");
            var rail = Find(weapon, "SightMount");
            Vector3 forward = weapon.InverseTransformDirection((end.position - rear.position).normalized);
            Vector3 railHeight = weapon.InverseTransformDirection(rail.position - firingHand.position);
            Vector3 up = Vector3.ProjectOnPlane(railHeight, forward).normalized;
            Vector3 firingGrip = weapon.InverseTransformPoint(firingHand.position);
            float reach = Vector3.Dot(weapon.InverseTransformPoint(end.position) - firingGrip, forward);
            var marker = Marker(body, "Firing hand reference", new Vector3(0f, .038f, .075f), Vector3.forward);
            var receiverFrame = WeaponMountFrame.FromMarkers(body, marker, muzzle, muzzle);
            RangeWeaponMount.Fit(weapon, new WeaponMountFrame(firingGrip, forward, up, reach), body, receiverFrame);

            foreach (string unused in new[] { "Handguard_Secondary", "RailCover", "Cartridge", "Casing" })
                Find(weapon, unused).gameObject.SetActive(false);
            ApplyMk18Materials(weapon);

            var magazine = Find(weapon, "Magazine");
            var magContact = Find(magazine, "MagazineHandGrip");
            RangeWeaponMount.BindPart(magazine, magDriver);
            RangeWeaponMount.BindPart(Find(weapon, "Bolt"), boltDriver);
            var charge = Find(weapon, "ChargingHandle");
            var chargeContact = Find(charge, "ChargingHandGrip");

            muzzle = RangeWeaponMount.CreateMarker(weapon, "Shot muzzle", weapon.InverseTransformPoint(end.position), forward, up);
            Find(weapon, "Stock").position += muzzle.forward * .106f;
            sight = RangeWeaponMount.CreateMarker(weapon, "MK18 optic center", weapon.InverseTransformPoint(rail.position), forward, up);
            MountMk18Optic(sight, weapon);

            var grip = driver.parent.gameObject.AddComponent<RangeGripRetarget>();
            grip.Configure(Find(driver, "L_arm_01"), Find(driver, "L_elbow_02"), Find(driver, "L_wrist_03"),
                oldMag, magContact, replacementCharge: chargeContact);
            grip.SetChargeContact(Find(driver, "L_point3_09"), Find(driver, "L_thumb3_00"));
            grip.SetChargeTravel(charge, charge.parent.InverseTransformVector(-muzzle.forward * .102f));
            grip.SetReloadPresentation(magazine, Find(weapon, "Slap"), Find(driver, "L_middle1_011"));
            return true;
        }

        private static void ApplyMk18Materials(Transform weapon)
        {
            var materials = new Dictionary<string, Material>();
            foreach (var renderer in weapon.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    string name = source.name;
                    if (!materials.TryGetValue(name, out var material))
                    {
                        var texture = Mk18Folder + "Textures/" + name;
                        material = Lit("MK18 " + name, Color.white);
                        material.SetTexture("_BaseMap", LoadWeaponTexture(texture + "_Albedo.png", sRGB: true));
                        material.SetTexture("_BumpMap", LoadWeaponTexture(texture + "_Normal.png", normal: true));
                        material.SetFloat("_BumpScale", 1f);
                        material.EnableKeyword("_NORMALMAP");
                        material.SetTexture("_MetallicGlossMap", LoadWeaponTexture(texture + "_MetallicSmoothness.png", packedSmoothness: true));
                        material.SetFloat("_Metallic", 1f);
                        material.SetFloat("_Smoothness", .90f);
                        material.EnableKeyword("_METALLICSPECGLOSSMAP");
                        material.SetTexture("_OcclusionMap", LoadWeaponTexture(texture + "_Occlusion.png"));
                        material.SetFloat("_OcclusionStrength", .84f);
                        material.EnableKeyword("_OCCLUSIONMAP");
                        EditorUtility.SetDirty(material);
                        materials.Add(name, material);
                    }
                    return material;
                }).ToArray();
            }
        }

        private static void MountMk18Optic(Transform aim, Transform weapon)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(OpticFolder + "VectorReflexOptic.obj");
            aim.position += aim.up * .038f;
            if (asset == null)
                return;

            Find(weapon, "RearIronSight").gameObject.SetActive(false);
            Find(weapon, "FrontIronSight").gameObject.SetActive(false);
            var optic = Unpack(asset, aim).transform;
            optic.name = "Low-profile reflex sight";
            var bounds = BoundsFor(optic);
            float scale = .080f / Mathf.Max(.001f, bounds.size.x);
            optic.localScale = Vector3.Scale(optic.localScale, new Vector3(scale, scale, scale * .46f));
            FinishOptic(optic);
        }
    }
}
