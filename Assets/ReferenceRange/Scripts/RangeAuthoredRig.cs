using UnityEngine;

namespace ReferenceRange
{
    /// <summary>Animation bindings on an imported first-person model prefab.</summary>
    [DisallowMultipleComponent]
    public sealed class RangeAuthoredRig : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip idle;
        [SerializeField] private AnimationClip fire;
        [SerializeField] private AnimationClip reload;
        [SerializeField] private AnimationClip emptyReload;
        [SerializeField] private AnimationClip walk;
        [SerializeField] private AnimationClip charge;
        [SerializeField] private AnimationClip aim;
        [SerializeField] private Transform sight;
        [SerializeField] private Transform muzzle;
        [SerializeField, Min(0.3f)] private float reloadSeconds = 1.82f;
        [SerializeField, Min(0.3f)] private float emptyReloadSeconds = 2.17f;
        [SerializeField, Min(0.3f)] private float chargeSeconds = 0.94f;
        [SerializeField] private Vector3 reloadCues = new Vector3(0.46f, 0.70f, -1f);
        [SerializeField] private Vector3 emptyReloadCues = new Vector3(0.35f, 0.53f, 0.75f);
        [SerializeField, Range(0f, 1f)] private float chargeSliceStart = 0.58f;
        [SerializeField, Range(0f, 1f)] private float chargeCue = 0.44f;

        internal Animator Animator => animator;
        internal AnimationClip Idle => idle != null ? idle : Reload;
        internal bool HasAuthoredIdle => idle != null;
        internal AnimationClip Fire => fire;
        internal AnimationClip Reload => reload != null ? reload : emptyReload;
        internal AnimationClip EmptyReload => emptyReload != null ? emptyReload : reload;
        internal AnimationClip Walk => walk;
        internal AnimationClip Charge => charge;
        internal AnimationClip Aim => aim;
        internal Transform Sight => sight;
        internal Transform Muzzle => muzzle;
        internal float ChargeSliceStart => chargeSliceStart;
        internal float ChargeCue => chargeCue;
        internal float ChargeSeconds => chargeSeconds;

        public void Configure(Animator source, AnimationClip idleClip, AnimationClip fireClip, AnimationClip reloadClip,
            AnimationClip emptyClip, AnimationClip walkClip = null, AnimationClip chargeClip = null, AnimationClip aimClip = null,
            Transform sightAnchor = null, Transform muzzleAnchor = null)
        {
            animator = source;
            idle = idleClip;
            fire = fireClip;
            reload = reloadClip;
            emptyReload = emptyClip;
            walk = walkClip;
            charge = chargeClip;
            aim = aimClip;
            sight = sightAnchor;
            muzzle = muzzleAnchor;
        }

        /// <summary>Normalized mag-out, mag-in, and charging cue times. Set Z to -1 for no charging gesture.</summary>
        public void SetReloadTiming(float tacticalDuration, Vector3 tacticalCues, float emptyDuration, Vector3 emptyCues)
        {
            reloadSeconds = Mathf.Max(0.3f, tacticalDuration);
            emptyReloadSeconds = Mathf.Max(0.3f, emptyDuration);
            reloadCues = tacticalCues;
            emptyReloadCues = emptyCues;
        }

        public void SetChargeTiming(float seconds, float sliceStart, float cue)
        {
            chargeSeconds = Mathf.Max(0.3f, seconds);
            chargeSliceStart = Mathf.Clamp01(sliceStart);
            chargeCue = Mathf.Clamp01(cue);
        }

        internal float ReloadSeconds(bool empty)
        {
            return empty ? emptyReloadSeconds : reloadSeconds;
        }

        internal Vector3 ReloadCues(bool empty)
        {
            return empty ? emptyReloadCues : reloadCues;
        }
    }
}
