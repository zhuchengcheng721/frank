using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ReferenceRange
{
    internal sealed class RangeAnimation
    {
        private enum Pose { Idle, Walk, Aim, Shot, Reload, EmptyReload, Charge, Count }

        private readonly RangeAuthoredRig rig;
        private readonly PlayableGraph graph;
        private readonly AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable[] poses = new AnimationClipPlayable[(int)Pose.Count];

        public RangeAnimation(RangeAuthoredRig rig)
        {
            this.rig = rig;
            if (rig == null || rig.Animator == null || rig.Idle == null)
                return;

            graph = PlayableGraph.Create("Imported first-person motion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            Animator animator = rig.Animator;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.fireEvents = false;
            animator.runtimeAnimatorController = null;
            mixer = AnimationMixerPlayable.Create(graph, (int)Pose.Count);
            AnimationPlayableOutput.Create(graph, "Viewmodel", animator).SetSourcePlayable(mixer);

            Bind(Pose.Idle, rig.Idle);
            Bind(Pose.Walk, rig.Walk);
            Bind(Pose.Aim, rig.Aim);
            Bind(Pose.Shot, rig.Fire);
            Bind(Pose.Reload, rig.Reload);
            Bind(Pose.EmptyReload, rig.EmptyReload);
            Bind(Pose.Charge, rig.Charge != null ? rig.Charge : rig.EmptyReload);
            graph.Play();
        }

        public bool HasWalk => Has(Pose.Walk);
        public bool HasShot => Has(Pose.Shot);
        public bool HasAim => Has(Pose.Aim);

        public void Sample(float clock, float walk, float aim, float shotAge, float shotInterval,
            float reload, bool emptyReload, float charge)
        {
            if (!graph.IsValid())
                return;

            for (int i = 0; i < poses.Length; i++)
                mixer.SetInputWeight(i, 0f);

            float actionWeight = 0f;
            if (reload >= 0f)
            {
                Pose slot = emptyReload ? Pose.EmptyReload : Pose.Reload;
                actionWeight = SetAction(slot, reload, reload, 0.025f, 0.055f);
            }
            else if (charge >= 0f)
            {
                float sample = rig.Charge != null ? charge : Mathf.Lerp(rig.ChargeSliceStart, 1f, charge);
                actionWeight = SetAction(Pose.Charge, charge, sample, 0.08f, 0.12f);
            }
            else if (HasShot && shotAge >= 0f && shotAge < shotInterval)
            {
                float shotTime = Mathf.Clamp01(shotAge / shotInterval);
                actionWeight = SetAction(Pose.Shot, shotTime, shotTime, 0.035f, 0.12f);
            }

            float free = 1f - actionWeight;
            float aimWeight = HasAim ? aim : 0f;
            float walkWeight = HasWalk ? Mathf.Clamp01(walk) * (1f - aim * 0.88f) : 0f;
            SetLoop(Pose.Idle, rig.HasAuthoredIdle ? clock : 0f, free * (1f - aimWeight) * (1f - walkWeight));
            SetLoop(Pose.Walk, clock * 1.04f, free * (1f - aimWeight) * walkWeight);
            SetLoop(Pose.Aim, clock, free * aimWeight);
            graph.Evaluate(0f);
        }

        private void Bind(Pose pose, AnimationClip clip)
        {
            if (clip == null || clip.length <= 0f)
                return;

            int index = (int)pose;
            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetSpeed(0f);
            poses[index] = playable;
            graph.Connect(playable, 0, mixer, index);
        }

        private bool Has(Pose pose)
        {
            return graph.IsValid() && poses[(int)pose].IsValid();
        }

        private float SetAction(Pose pose, float progress, float time, float attack, float release)
        {
            if (!Has(pose))
                return 0f;

            float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / attack))
                * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - progress) / release));
            AnimationClipPlayable playable = poses[(int)pose];
            playable.SetTime(Mathf.Clamp01(time) * playable.GetAnimationClip().length);
            mixer.SetInputWeight((int)pose, weight);
            return weight;
        }

        private void SetLoop(Pose pose, float clock, float weight)
        {
            if (!Has(pose) || weight <= 0f)
                return;

            AnimationClipPlayable playable = poses[(int)pose];
            playable.SetTime(Mathf.Repeat(clock, playable.GetAnimationClip().length));
            mixer.SetInputWeight((int)pose, weight);
        }

        public void Dispose()
        {
            if (graph.IsValid())
                graph.Destroy();
        }
    }
}
