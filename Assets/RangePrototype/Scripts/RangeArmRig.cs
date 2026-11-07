using System.Collections.Generic;
using UnityEngine;

namespace RangePrototype
{
    [DisallowMultipleComponent]
    public sealed class RangeArmRig : MonoBehaviour
    {
        private static readonly string[] Digits = { "pinky", "ring", "middle", "index", "thumb" };
        private static readonly float[] JointAngles = { 22f, 39f, 24f };

        private struct FingerJoint
        {
            public Transform Bone;
            public Quaternion Rest;
            public Vector3 Axis;
            public float Angle;
            public bool IsIndex;
        }

        [SerializeField] private Transform upperArm;
        [SerializeField] private Transform forearm;
        [SerializeField] private Transform wrist;
        [SerializeField] private Transform bendHint;
        [SerializeField] private Transform gripSocket;

        private Quaternion upperRest;
        private Quaternion forearmRest;
        private Quaternion wristOffset;
        private Vector3 wristToGrip;
        private Vector3 localBend;
        private float upperLength;
        private float forearmLength;
        private bool ready;
        private FingerJoint[] fingers;

        public void Configure(Transform upper, Transform lower, Transform hand, Transform hint = null, Transform socket = null)
        {
            upperArm = upper;
            forearm = lower;
            wrist = hand;
            bendHint = hint;
            gripSocket = socket;
        }

        internal void Initialize()
        {
            if (upperArm == null || forearm == null || wrist == null)
                return;

            upperRest = upperArm.localRotation;
            forearmRest = forearm.localRotation;
            Transform reference = gripSocket != null ? gripSocket : transform;
            wristOffset = Quaternion.Inverse(reference.rotation) * wrist.rotation;
            if (gripSocket != null)
            {
                wristToGrip = wrist.InverseTransformDirection(gripSocket.position - wrist.position);
                fingers = BindFingerJoints();
            }

            Vector3 upper = forearm.position - upperArm.position;
            Vector3 lower = wrist.position - forearm.position;
            upperLength = upper.magnitude;
            forearmLength = lower.magnitude;
            if (upperLength < 0.001f || forearmLength < 0.001f)
                return;

            Vector3 bend = Vector3.ProjectOnPlane(upper, (upper + lower).normalized);
            if (bend.sqrMagnitude < 0.00001f)
                bend = Vector3.down;

            localBend = upperArm.parent.InverseTransformDirection(bend.normalized);
            ready = true;
        }

        private FingerJoint[] BindFingerJoints()
        {
            int suffix = wrist.name.LastIndexOf('.');
            if (suffix < 0)
                return System.Array.Empty<FingerJoint>();

            string side = wrist.name.Substring(suffix);
            var joints = new List<FingerJoint>(15);
            foreach (string digit in Digits)
            {
                Transform bone = wrist.Find("finger_" + digit + "1" + side);
                if (bone == null || bone.childCount == 0)
                    continue;

                Vector3 direction = (bone.GetChild(0).position - bone.position).normalized;
                Vector3 palm = (gripSocket.position - bone.position).normalized;
                Vector3 hinge = Vector3.Cross(direction, palm).normalized;
                if (hinge.sqrMagnitude < 0.01f)
                    continue;

                for (int joint = 0; joint < JointAngles.Length && bone != null; joint++)
                {
                    joints.Add(new FingerJoint
                    {
                        Bone = bone,
                        Rest = bone.localRotation,
                        Axis = bone.InverseTransformDirection(hinge),
                        Angle = JointAngles[joint] * (digit == "thumb" ? 0.48f : 1f),
                        IsIndex = digit == "index"
                    });
                    bone = bone.childCount == 0 ? null : bone.GetChild(0);
                }
            }
            return joints.ToArray();
        }

        internal void Solve(float gripCurl, float indexCurl)
        {
            if (!ready)
                return;

            upperArm.localRotation = upperRest;
            forearm.localRotation = forearmRest;

            Vector3 shoulder = upperArm.position;
            Quaternion desiredWristRotation = transform.rotation * wristOffset;
            Vector3 target = transform.position - desiredWristRotation * wristToGrip - shoulder;
            Vector3 direction = target.sqrMagnitude > 0.000001f ? target.normalized : (wrist.position - shoulder).normalized;
            float distance = Mathf.Clamp(target.magnitude, Mathf.Abs(upperLength - forearmLength) + 0.0001f,
                upperLength + forearmLength - 0.0001f);
            Vector3 bendDirection = bendHint == null ? upperArm.parent.TransformDirection(localBend) : bendHint.position - shoulder;
            Vector3 bend = Vector3.ProjectOnPlane(bendDirection, direction);
            if (bend.sqrMagnitude < 0.000001f)
                bend = Vector3.Cross(direction, upperArm.parent.right);
            if (bend.sqrMagnitude < 0.000001f)
                bend = Vector3.Cross(direction, upperArm.parent.up);

            float along = (upperLength * upperLength + distance * distance - forearmLength * forearmLength) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 elbow = shoulder + direction * along + bend.normalized * height;
            upperArm.rotation = Quaternion.FromToRotation(forearm.position - shoulder, elbow - shoulder) * upperArm.rotation;

            Vector3 wristGoal = shoulder + direction * distance;
            Vector3 forearmStart = forearm.position;
            forearm.rotation = Quaternion.FromToRotation(wrist.position - forearmStart, wristGoal - forearmStart) * forearm.rotation;
            wrist.rotation = desiredWristRotation;
            if (fingers == null)
                return;

            foreach (FingerJoint finger in fingers)
            {
                float curl = finger.IsIndex ? indexCurl : gripCurl;
                finger.Bone.localRotation = finger.Rest * Quaternion.AngleAxis(finger.Angle * curl, finger.Axis);
            }
        }
    }
}
