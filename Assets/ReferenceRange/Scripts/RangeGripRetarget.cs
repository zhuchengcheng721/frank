using UnityEngine;

namespace ReferenceRange
{
    /// <summary>Corrects the support hand for the dimensions of a replacement weapon after sampling its imported clip.</summary>
    [DisallowMultipleComponent]
    public sealed class RangeGripRetarget : MonoBehaviour
    {
        [SerializeField] private Transform upperArm;
        [SerializeField] private Transform forearm;
        [SerializeField] private Transform wrist;
        [SerializeField] private Transform donorMagazine;
        [SerializeField] private Transform mountedMagazine;
        [SerializeField] private Transform donorCharge;
        [SerializeField] private Transform mountedCharge;
        [SerializeField] private Transform chargeIndex;
        [SerializeField] private Transform chargeThumb;
        [SerializeField] private Transform chargeMotion;
        [SerializeField] private Vector3 chargeHome;
        [SerializeField] private Vector3 chargeTravel;
        [SerializeField] private Transform magazine;
        [SerializeField] private Vector3 magazineHome;
        [SerializeField] private Transform slapSurface;
        [SerializeField] private Transform slapKnuckle;
        private bool releasedMagazine;
        private Rigidbody droppedBody;
        private BoxCollider droppedCollider;
        private bool magazineLanded;

        public void Configure(Transform upper, Transform lower, Transform hand, Transform originalMagazine,
            Transform replacementMagazine, Transform originalCharge = null, Transform replacementCharge = null)
        {
            upperArm = upper;
            forearm = lower;
            wrist = hand;
            donorMagazine = originalMagazine;
            mountedMagazine = replacementMagazine;
            donorCharge = originalCharge;
            mountedCharge = replacementCharge;
        }

        public void SetChargeContact(Transform indexTip, Transform thumbTip)
        {
            chargeIndex = indexTip;
            chargeThumb = thumbTip;
        }

        public void SetChargeTravel(Transform handleMotion, Vector3 localTravel)
        {
            chargeMotion = handleMotion;
            chargeHome = handleMotion != null ? handleMotion.localPosition : Vector3.zero;
            chargeTravel = localTravel;
        }

        public void SetReloadPresentation(Transform mountedMag, Transform slapPoint, Transform middleKnuckle)
        {
            magazine = mountedMag;
            magazineHome = mountedMag.localPosition;
            slapSurface = slapPoint;
            slapKnuckle = middleKnuckle;
        }

        internal void Apply(float clipProgress, bool reloading)
        {
            if (chargeMotion != null)
            {
                float travel = Ease(clipProgress, 0.72f, 0.77f) * (1f - Ease(clipProgress, 0.81f, 0.86f));
                chargeMotion.localPosition = chargeHome + chargeTravel * travel;
            }

            UpdateMagazine(clipProgress, reloading);
            if (clipProgress < 0f || upperArm == null || forearm == null || wrist == null)
                return;

            Vector3 correction = Vector3.zero;
            if (donorMagazine != null && mountedMagazine != null)
            {
                float removalGrip = Ease(clipProgress, 0.21f, 0.25f) * (1f - Ease(clipProgress, 0.28f, 0.32f));
                float insertionGrip = Ease(clipProgress, 0.33f, 0.38f) * (1f - Ease(clipProgress, 0.51f, 0.59f));
                float contact = Mathf.Max(removalGrip, insertionGrip);
                correction += (mountedMagazine.position - donorMagazine.position) * contact;
            }
            bool hasFingerContact = chargeIndex != null && chargeThumb != null;
            if (mountedCharge != null && (hasFingerContact || donorCharge != null))
            {
                // Close on the real tab before the authored carrier moves, stay on during
                // its travel and spring return, then release back to the imported pose.
                float contact = Ease(clipProgress, 0.65f, 0.71f) * (1f - Ease(clipProgress, 0.86f, 0.94f));
                Vector3 handContact = hasFingerContact
                    ? (chargeIndex.position + chargeThumb.position) * 0.5f
                    : donorCharge.position;
                correction += (mountedCharge.position - handContact) * contact;
            }
            if (reloading && slapSurface != null && slapKnuckle != null)
            {
                float slap = Ease(clipProgress, 0.885f, 0.929f) * (1f - Ease(clipProgress, 0.944f, 0.988f));
                Vector3 palm = Vector3.Lerp(wrist.position, slapKnuckle.position, 0.64f);
                correction = Vector3.Lerp(correction, slapSurface.position - palm, slap);
            }
            if (correction.sqrMagnitude < 0.0000001f)
                return;

            Solve(wrist.position + correction);
        }

        internal bool TryGetDroppedMagazine(out Vector3 position, out bool landed)
        {
            position = Vector3.zero;
            landed = false;
            if (!releasedMagazine || droppedBody == null || droppedCollider == null)
                return false;

            Bounds bounds = droppedCollider.bounds;
            position = bounds.center;
            if (!magazineLanded && Mathf.Abs(droppedBody.linearVelocity.y) < 0.5f &&
                Physics.Raycast(position, Vector3.down, out RaycastHit floor, bounds.extents.y + 0.035f,
                    ~(1 << 2), QueryTriggerInteraction.Ignore) && floor.normal.y > 0.5f)
            {
                magazineLanded = true;
                droppedBody.useGravity = true;
            }

            landed = magazineLanded;
            return true;
        }

        private void FixedUpdate()
        {
            if (droppedBody != null && !magazineLanded)
                droppedBody.AddForce(Physics.gravity * 0.42f, ForceMode.Acceleration);
        }

        private void UpdateMagazine(float progress, bool reloading)
        {
            if (magazine == null)
                return;

            const float release = 0.283f;
            const float pickup = 0.372f;

            // The source clip moves a single magazine out and back. Leave a separate
            // physical magazine behind so the return stroke reads as a fresh pickup.
            if (!reloading)
                releasedMagazine = false;
            else if (!releasedMagazine && progress >= release)
            {
                DropMagazine();
                releasedMagazine = true;
            }

            bool visible = !reloading || progress < release || progress >= pickup;
            if (magazine.gameObject.activeSelf != visible)
                magazine.gameObject.SetActive(visible);

            float draw = reloading
                ? Ease(progress, 0.34f, 0.39f) * (1f - Ease(progress, 0.41f, 0.484f))
                : 0f;
            Vector3 drawOffset = transform.right * -0.065f + transform.up * 0.095f + transform.forward * 0.038f;
            magazine.localPosition = magazineHome + magazine.parent.InverseTransformVector(drawOffset * draw);
        }

        private void DropMagazine()
        {
            var source = magazine.GetComponent<MeshFilter>();
            var sourceRenderer = magazine.GetComponent<MeshRenderer>();
            if (source == null || source.sharedMesh == null || sourceRenderer == null)
                return;

            var dropped = new GameObject("Discarded magazine");
            dropped.layer = gameObject.layer;
            dropped.transform.SetPositionAndRotation(magazine.position, magazine.rotation);
            dropped.transform.localScale = magazine.lossyScale;
            dropped.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            dropped.AddComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;

            Bounds bounds = source.sharedMesh.bounds;
            var collider = dropped.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
            var player = GetComponentInParent<CharacterController>();
            if (player != null)
                Physics.IgnoreCollision(collider, player);

            if (droppedBody != null)
                droppedBody.useGravity = true;

            droppedCollider = collider;
            droppedBody = dropped.AddComponent<Rigidbody>();
            droppedBody.useGravity = false;
            droppedBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            magazineLanded = false;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 left = Vector3.Cross(forward, Vector3.up);
            Vector3 departure = left * 0.34f + forward * 0.63f + Vector3.down * 0.16f;
            droppedBody.AddForce(departure, ForceMode.VelocityChange);
            droppedBody.AddTorque(forward * 3f + left * 2f, ForceMode.VelocityChange);
            Destroy(dropped, 9f);
        }

        private void Solve(Vector3 target)
        {
            Vector3 shoulder = upperArm.position;
            Vector3 elbow = forearm.position;
            Vector3 originalWrist = wrist.position;
            Vector3 originalReach = originalWrist - shoulder;
            Vector3 reach = target - shoulder;
            float upperLength = Vector3.Distance(shoulder, elbow);
            float lowerLength = Vector3.Distance(elbow, originalWrist);
            if (reach.sqrMagnitude < 0.000001f || upperLength < 0.001f || lowerLength < 0.001f)
                return;

            Vector3 direction = reach.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(elbow - shoulder, originalReach.normalized);
            bend = Vector3.ProjectOnPlane(bend, direction);
            if (bend.sqrMagnitude < 0.000001f)
                bend = Vector3.ProjectOnPlane(-transform.up, direction);
            if (bend.sqrMagnitude < 0.000001f)
                bend = Vector3.ProjectOnPlane(-transform.right, direction);

            float distance = Mathf.Clamp(reach.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.0001f,
                upperLength + lowerLength - 0.0001f);
            float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
            float away = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 desiredElbow = shoulder + direction * along + bend.normalized * away;
            Quaternion authoredWristRotation = wrist.rotation;
            upperArm.rotation = Quaternion.FromToRotation(elbow - shoulder, desiredElbow - shoulder) * upperArm.rotation;
            Vector3 wristGoal = shoulder + direction * distance;
            forearm.rotation = Quaternion.FromToRotation(wrist.position - forearm.position, wristGoal - forearm.position) * forearm.rotation;
            wrist.rotation = authoredWristRotation;
        }

        private static float Ease(float time, float start, float end)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));
        }
    }
}
