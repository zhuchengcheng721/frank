using UnityEngine;

namespace ReferenceRange.Editor
{
    // All frame measurements are local to the transform passed with the frame.
    internal readonly struct WeaponMountFrame
    {
        public Vector3 Grip { get; }
        public Vector3 Forward { get; }
        public Vector3 Up { get; }
        public float GripToMuzzle { get; }

        internal WeaponMountFrame(Vector3 grip, Vector3 forward, Vector3 up, float gripToMuzzle)
        {
            Grip = grip;
            Forward = forward;
            Up = up;
            GripToMuzzle = gripToMuzzle;
        }

        internal static WeaponMountFrame FromMarkers(Transform frame, Transform grip, Transform muzzle, Transform bore)
        {
            Vector3 point = frame.InverseTransformPoint(grip.position);
            Vector3 muzzlePoint = frame.InverseTransformPoint(muzzle.position);
            Vector3 forward = frame.InverseTransformDirection(bore.forward);
            Vector3 up = frame.InverseTransformDirection(bore.up);
            return new WeaponMountFrame(point, forward, up, Mathf.Abs(Vector3.Dot(muzzlePoint - point, forward)));
        }
    }

    internal static class RangeWeaponMount
    {
        /// <summary>Move a model onto a sampled first-person rig using bore alignment and grip contact.</summary>
        internal static void Fit(Transform replacement, WeaponMountFrame source,
            Transform animatedBody, WeaponMountFrame target, bool resize = true)
        {
            if (replacement == null || animatedBody == null || animatedBody.IsChildOf(replacement))
                throw new System.ArgumentException("Expected independent weapon and animated body transforms.");

            replacement.SetParent(animatedBody, true);
            Quaternion sourceFrame = Quaternion.LookRotation(replacement.TransformDirection(source.Forward), replacement.TransformDirection(source.Up));
            Quaternion targetFrame = Quaternion.LookRotation(animatedBody.TransformDirection(target.Forward), animatedBody.TransformDirection(target.Up));
            replacement.rotation = targetFrame * Quaternion.Inverse(sourceFrame) * replacement.rotation;

            if (resize && source.GripToMuzzle > 0.0001f && target.GripToMuzzle > 0.0001f)
            {
                float sourceLength = replacement.TransformVector(source.Forward.normalized * source.GripToMuzzle).magnitude;
                float targetLength = animatedBody.TransformVector(target.Forward.normalized * target.GripToMuzzle).magnitude;
                if (sourceLength > 0.0001f)
                    replacement.localScale *= targetLength / sourceLength;
            }

            replacement.position += animatedBody.TransformPoint(target.Grip) - replacement.TransformPoint(source.Grip);
        }

        /// <summary>Transfer a separate rigid component after fitting the gun at the animation's seated frame.</summary>
        internal static void BindPart(Transform mountedPart, Transform animatedDriver)
        {
            if (mountedPart == null || animatedDriver == null || animatedDriver.IsChildOf(mountedPart))
                throw new System.ArgumentException("Expected an existing weapon component and its independent motion driver.");

            mountedPart.SetParent(animatedDriver, true);
        }

        internal static Transform CreateMarker(Transform parent, string name, Vector3 localPosition, Vector3 localForward, Vector3 localUp)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.localPosition = localPosition;
            marker.localRotation = Quaternion.LookRotation(localForward, localUp);
            return marker;
        }

        /// <summary>Keep imported model hierarchy and animation paths intact while hiding its original geometry.</summary>
        internal static void HideGeometry(Transform importedObject)
        {
            foreach (Renderer renderer in importedObject.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
        }

        /// <summary>Place the authoring camera at the Unity game camera origin without creating a second Camera.</summary>
        internal static void CenterAuthoringCamera(Transform viewModel, Transform cameraMarker)
        {
            Quaternion delta = Quaternion.Inverse(cameraMarker.rotation);
            viewModel.rotation = delta * viewModel.rotation;
            viewModel.position -= cameraMarker.position;
        }
    }
}
