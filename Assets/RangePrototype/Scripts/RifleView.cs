using UnityEngine;
using UnityEngine.Rendering;

namespace RangePrototype
{
    internal sealed class RifleView
    {
        private static readonly Vector3 HipPosition = new Vector3(0.205f, -0.212f, 0.305f);
        private static readonly Vector3 MagazineTravel = new Vector3(-0.049f, -0.283f, -0.035f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly Transform root;
        private readonly Transform weapon;
        private readonly Transform magazine;
        private readonly Transform chargingHandle;
        private readonly Transform bolt;
        private readonly Transform supportArm;
        private readonly RangeArmRig leftRig;
        private readonly RangeArmRig rightRig;
        private readonly Transform magazineGrip;
        private readonly Transform chargingGrip;
        private readonly Transform flash;
        private readonly Transform muzzle;
        private readonly Vector3 aimPosition;
        private readonly Vector3 magazineHome;
        private readonly Vector3 handleHome;
        private readonly Vector3 boltHome;
        private readonly Vector3 supportHome;
        private readonly Quaternion magazineHomeRotation;
        private readonly Quaternion supportHomeRotation;
        private readonly Material reticleMaterial;
        private readonly Material flashHot;
        private readonly Material flashGlow;
        private float leftCurl = 0.63f;
        private float triggerCurl = 0.48f;

        public Vector3 MuzzlePosition => muzzle.position;

        public RifleView(Transform camera, GameObject model)
        {
            if (model == null)
            {
                muzzle = new GameObject("Muzzle Anchor").transform;
                muzzle.SetParent(camera, false);
                muzzle.localPosition = new Vector3(0.16f, -0.08f, 0.55f);
                Debug.LogWarning("Assign a downloaded first-person rig to FpsRange, or place it at Resources/RangeModels/FirstPersonRig.");
                return;
            }

            root = Object.Instantiate(model, camera, false).transform;
            root.name = "First Person Rig";
            root.localPosition = HipPosition;
            weapon = root.Find("Weapon") ?? root;
            magazine = weapon.Find("Magazine");
            chargingHandle = weapon.Find("ChargingHandle");
            bolt = weapon.Find("Bolt");
            supportArm = root.Find("LeftArm");
            Transform firingArm = root.Find("RightArm");
            leftRig = supportArm != null ? supportArm.GetComponent<RangeArmRig>() : null;
            rightRig = firingArm != null ? firingArm.GetComponent<RangeArmRig>() : null;
            magazineGrip = weapon.Find("MagazineGrip");
            chargingGrip = weapon.Find("ChargingGrip");

            if (magazine != null)
            {
                magazineHome = magazine.localPosition;
                magazineHomeRotation = magazine.localRotation;
            }
            if (chargingHandle != null)
                handleHome = chargingHandle.localPosition;
            if (bolt != null)
                boltHome = bolt.localPosition;
            if (supportArm != null)
            {
                supportHome = supportArm.localPosition;
                supportHomeRotation = supportArm.localRotation;
            }

            foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
                part.gameObject.layer = 2;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            Transform arms = root.Find("ArmsRig");
            if (arms != null)
                arms.SetParent(camera, true);
            leftRig?.Initialize();
            rightRig?.Initialize();

            Transform sight = weapon.Find("Sight");
            Vector3 sightPosition = sight != null ? root.InverseTransformPoint(sight.position) : new Vector3(0f, 0.167f, 0.17f);
            aimPosition = new Vector3(-sightPosition.x, -sightPosition.y, 0.237f);
            if (sight != null && sight.GetComponentInChildren<Renderer>() == null)
            {
                reticleMaterial = CreateUnlit(new Color(2.4f, 0.26f, 0.085f));
                EffectSphere(sight, "Red Dot", Vector3.zero, Vector3.one * 0.0022f, reticleMaterial);
            }

            muzzle = weapon.Find("Muzzle");
            if (muzzle == null)
            {
                muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(weapon, false);
                muzzle.localPosition = new Vector3(0f, 0.058f, 0.687f);
            }
            flash = new GameObject("Muzzle Flash").transform;
            flash.SetParent(muzzle, false);
            flashHot = CreateUnlit(new Color(1.45f, 1.05f, 0.48f));
            flashGlow = CreateUnlit(new Color(1.55f, 0.48f, 0.10f));
            EffectSphere(flash, "Flash Core", new Vector3(0f, 0f, 0.042f), new Vector3(0.047f, 0.046f, 0.145f), flashHot);
            EffectSphere(flash, "Flash Streak", new Vector3(0f, 0f, 0.031f), new Vector3(0.092f, 0.009f, 0.071f), flashGlow);
            Transform wing = EffectSphere(flash, "Flash Streak", new Vector3(0f, 0f, 0.031f),
                new Vector3(0.086f, 0.009f, 0.078f), flashGlow);
            wing.localRotation = Quaternion.Euler(0f, 0f, 67f);
            flash.gameObject.SetActive(false);
        }

        public void UpdateArmRigs()
        {
            leftRig?.Solve(leftCurl, leftCurl);
            rightRig?.Solve(1f, triggerCurl);
        }

        public void Flash()
        {
            if (flash == null)
                return;

            flash.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            flash.localScale = Vector3.one * Random.Range(0.72f, 1.32f);
        }

        public void Pose(float aim, float walk, float step, float swayX, float swayY, float kick,
            float reload, float charge, bool showFlash)
        {
            if (root == null)
                return;

            float movement = walk * Mathf.Lerp(1f, 0.16f, aim);
            float idle = Mathf.Sin(Time.time * 2.5f) * Mathf.Lerp(0.0027f, 0.0004f, aim);
            float reloadPose = Blend(reload, 0f, 0.23f) * (1f - Blend(reload, 1.00f, 1.66f));
            float chargePose = Blend(charge, 0f, 0.20f) * (1f - Blend(charge, 0.44f, 0.72f));
            float swayAmount = 1f - aim * 0.93f;
            root.localPosition = Vector3.Lerp(HipPosition, aimPosition, aim)
                + new Vector3(Mathf.Cos(step) * 0.014f * movement - swayX * 0.075f * swayAmount,
                    Mathf.Sin(step * 2f) * 0.010f * movement - swayY * 0.059f * swayAmount + idle,
                    -kick * 0.060f)
                + new Vector3(0.047f, -0.051f, 0.063f) * reloadPose
                + new Vector3(0.029f, -0.016f, 0.035f) * chargePose;
            root.localRotation = Quaternion.Euler(-kick * Mathf.Lerp(7f, 4f, aim) + swayY * 11f * swayAmount - reloadPose * 13f,
                swayX * 14f * swayAmount - reloadPose * 21f - chargePose * 8f,
                Mathf.Cos(step) * movement * 2.8f - reloadPose * 29f - chargePose * 13f);

            float withdrawn = Blend(reload, FpsRange.MagazineRemoved, 0.61f) * (1f - Blend(reload, 0.72f, FpsRange.MagazineSeated));
            Vector3 magazineOffset = MagazineTravel * withdrawn;
            if (magazine != null)
            {
                magazine.localPosition = magazineHome + magazineOffset;
                magazine.localRotation = Quaternion.Euler(-16f * withdrawn, 11f * withdrawn, -13f * withdrawn) * magazineHomeRotation;
                magazine.gameObject.SetActive(reload < 0.62f || reload >= 0.70f);
            }

            float pull = Blend(charge, 0.22f, 0.38f) * (1f - Blend(charge, 0.43f, 0.53f));
            if (chargingHandle != null)
                chargingHandle.localPosition = handleHome - Vector3.forward * (pull * 0.105f);
            if (bolt != null)
                bolt.localPosition = boltHome - Vector3.forward * (Mathf.Max(pull, Mathf.Clamp01(kick)) * 0.073f);

            triggerCurl = Mathf.Lerp(0.48f, 0.83f, Mathf.Clamp01(kick));
            PoseSupportHand(reload, charge, magazineOffset, pull);
            flash.gameObject.SetActive(showFlash);
        }

        private void PoseSupportHand(float reload, float charge, Vector3 magazineOffset, float pull)
        {
            if (supportArm == null)
                return;

            float grasp = Blend(reload, 0f, 0.26f) * (1f - Blend(reload, 1.00f, 1.30f));
            float reach = Blend(charge, 0f, 0.22f) * (1f - Blend(charge, 0.54f, 0.73f));
            leftCurl = Mathf.Lerp(Mathf.Lerp(0.63f, 0.92f, grasp), 0.76f, reach);
            Vector3 magazinePosition = RootPosition(magazineGrip, new Vector3(-0.054f, -0.133f, 0.193f));
            Vector3 chargePosition = RootPosition(chargingGrip, new Vector3(-0.085f, 0.048f, 0.075f));
            Quaternion magazineRotation = RootRotation(magazineGrip, Quaternion.Euler(14f, 13f, -18f));
            Quaternion chargeRotation = RootRotation(chargingGrip, Quaternion.Euler(56f, -22f, 72f));
            Vector3 handPosition = Vector3.Lerp(supportHome, magazinePosition + magazineOffset, grasp);
            Quaternion handRotation = Quaternion.Slerp(supportHomeRotation, magazineRotation, grasp);
            supportArm.localPosition = Vector3.Lerp(handPosition, chargePosition - Vector3.forward * (pull * 0.105f), reach);
            supportArm.localRotation = Quaternion.Slerp(handRotation, chargeRotation, reach);
        }

        private Vector3 RootPosition(Transform marker, Vector3 fallback)
        {
            return marker == null ? fallback : root.InverseTransformPoint(marker.position);
        }

        private Quaternion RootRotation(Transform marker, Quaternion fallback)
        {
            return marker == null ? fallback : Quaternion.Inverse(root.rotation) * marker.rotation;
        }

        public static Material CreateUnlit(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader);
            if (material.HasProperty(BaseColor))
                material.SetColor(BaseColor, color);
            else
                material.color = color;
            return material;
        }

        private static Transform EffectSphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = name;
            obj.layer = 2;
            Collider collider = obj.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Transform shape = obj.transform;
            shape.SetParent(parent, false);
            shape.localPosition = position;
            shape.localScale = scale;
            return shape;
        }

        private static float Blend(float value, float from, float to)
        {
            float progress = Mathf.InverseLerp(from, to, value);
            return progress * progress * (3f - 2f * progress);
        }

        public void Dispose()
        {
            Object.Destroy(reticleMaterial);
            Object.Destroy(flashHot);
            Object.Destroy(flashGlow);
        }
    }
}
