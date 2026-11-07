using UnityEngine;
using UnityEngine.Rendering;

namespace ReferenceRange
{
    internal sealed class RifleView
    {
        private static readonly Vector3 HipFraming = new Vector3(-0.032f, 0.032f, -0.089f);
        private static readonly Vector3 ReloadFraming = new Vector3(-0.007f, 0.006f, 0.060f);

        private readonly Transform root;
        private readonly RangeAuthoredRig rig;
        private readonly RangeAnimation motion;
        private readonly RangeGripRetarget gripRetarget;
        private readonly Transform muzzle;
        private readonly Vector3 aimShift;
        private readonly Quaternion aimRotation = Quaternion.identity;
        private readonly MuzzleEffects muzzleEffects;

        public Vector3 MuzzlePosition => muzzle.position;
        public float ReloadSeconds(bool empty) => rig != null ? rig.ReloadSeconds(empty) : 1.82f;
        public Vector3 ReloadCues(bool empty) => rig != null ? rig.ReloadCues(empty) : new Vector3(0.29f, 0.63f, empty ? 0.83f : -1f);
        public float ChargeSeconds => rig != null ? rig.ChargeSeconds : 0.75f;
        public float ChargeCue => rig != null ? rig.ChargeCue : 0.64f;

        public RifleView(Transform camera, GameObject prefab)
        {
            root = new GameObject("Viewmodel").transform;
            root.SetParent(camera, false);
            if (prefab != null)
            {
                GameObject model = Object.Instantiate(prefab, root, false);
                foreach (Camera imported in model.GetComponentsInChildren<Camera>(true))
                    imported.enabled = false;
                foreach (AudioListener listener in model.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
                foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
                    part.gameObject.layer = 2;
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                rig = model.GetComponentInChildren<RangeAuthoredRig>(true);
                gripRetarget = model.GetComponentInChildren<RangeGripRetarget>(true);
                motion = new RangeAnimation(rig);
                motion.Sample(0f, 0f, 0f, -1f, 0.13f, -1f, false, -1f);
            }

            if (rig != null && rig.Muzzle != null)
            {
                muzzle = rig.Muzzle;
                muzzleEffects = new MuzzleEffects(root);
            }
            else
            {
                muzzle = new GameObject("Muzzle Anchor").transform;
                muzzle.SetParent(root, false);
                muzzle.localPosition = new Vector3(0.16f, -0.08f, 0.55f);
            }

            if (rig != null && rig.Sight != null && !motion.HasAim)
            {
                Vector3 sightPosition = root.InverseTransformPoint(rig.Sight.position);
                Vector3 forward = root.InverseTransformDirection(rig.Sight.forward);
                if (Vector3.Dot(forward, Vector3.forward) > 0.50f)
                    aimRotation = Quaternion.FromToRotation(forward, Vector3.forward);
                Vector3 centered = aimRotation * sightPosition;
                aimShift = new Vector3(-centered.x, -centered.y, -0.035f);
            }
        }

        public void Flash()
        {
            muzzleEffects?.Emit(muzzle.position, muzzle.forward);
        }

        public bool TryGetDroppedMagazine(out Vector3 position, out bool landed)
        {
            if (gripRetarget != null)
                return gripRetarget.TryGetDroppedMagazine(out position, out landed);
            position = default;
            landed = false;
            return false;
        }

        public void Pose(float aim, float walk, float step, float swayX, float swayY, float kick,
            float shotAge, float shotInterval, float reload, bool emptyReload, float charge, float glance = 0f)
        {
            motion?.Sample(Time.time, walk, aim, shotAge, shotInterval, reload, emptyReload, charge);
            if (gripRetarget != null)
            {
                float progress = reload >= 0f ? reload :
                    charge >= 0f && rig != null && rig.Charge == null ? Mathf.Lerp(rig.ChargeSliceStart, 1f, charge) : -1f;
                gripRetarget.Apply(progress, reload >= 0f);
            }
            muzzleEffects?.Update();

            float movement = walk * Mathf.Lerp(1f, 0.17f, aim);
            if (motion != null && motion.HasWalk)
                movement *= 0.24f;
            if (reload >= 0f || charge >= 0f)
                movement *= 0.28f;

            float sway = 1f - aim * 0.94f;
            float cameraKick = kick * (motion != null && motion.HasShot ? 0.20f : 1f);
            // Give the support hand space on screen while it lowers the rifle and retrieves the magazine.
            float reloadDip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.18f, reload))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.56f, 0.90f, reload)));
            float revealMagazine = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 0.40f, reload))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.50f, 0.58f, reload)));
            Vector3 framing = Vector3.Lerp(HipFraming, aimShift, aim) + ReloadFraming * reloadDip
                + new Vector3(-0.037f, 0.058f, -0.025f) * revealMagazine
                + new Vector3(0.11f, -0.095f, 0.045f) * glance;
            root.localPosition = framing + new Vector3(Mathf.Cos(step) * movement * 0.010f - swayX * sway * 0.07f,
                Mathf.Sin(step * 2f) * movement * 0.009f - swayY * sway * 0.048f,
                -cameraKick * 0.043f);
            float slap = Mathf.Sin(Mathf.PI * Mathf.InverseLerp(0.925f, 0.995f, reload));
            float pitch = -cameraKick * 4.5f + swayY * 8f * sway + reloadDip * 3f - revealMagazine * 2.6f + slap * 1.3f
                - revealMagazine * 5.5f + glance * 9f;
            Quaternion motionRotation = Quaternion.Euler(pitch,
                swayX * 12f * sway, Mathf.Cos(step) * movement * 1.4f - slap * 1.7f - revealMagazine * 10f);
            root.localRotation = Quaternion.Slerp(Quaternion.identity, aimRotation, aim) * motionRotation;
        }

        public static Material CreateUnlit(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader);
            material.color = color;
            return material;
        }

        public void Dispose()
        {
            motion?.Dispose();
            muzzleEffects?.Dispose();
        }
    }
}
