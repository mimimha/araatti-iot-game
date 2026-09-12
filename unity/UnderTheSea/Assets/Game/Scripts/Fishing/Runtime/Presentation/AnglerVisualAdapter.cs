using System.Collections.Generic;
using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AnglerVisualAdapter : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform rodGripAnchor;

        private readonly HashSet<int> _parameters = new HashSet<int>();
        private FishingPlayerState _lastState;
        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private bool _bound;

        public bool HasRodGrip => rodGripAnchor != null;
        public Vector3 RodGripPosition => rodGripAnchor != null ? rodGripAnchor.position : transform.position;

        public void Configure(Animator value, Transform gripAnchor)
        {
            animator = value;
            rodGripAnchor = gripAnchor;
            _bound = false;
            _parameters.Clear();
        }

        public void AutoBind()
        {
            if (_bound) return;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                AnimatorControllerParameter[] animatorParameters = animator.parameters;
                for (int i = 0; i < animatorParameters.Length; i++)
                {
                    _parameters.Add(animatorParameters[i].nameHash);
                }

                if (rodGripAnchor == null && animator.isHuman)
                {
                    Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    if (rightHand != null)
                    {
                        rodGripAnchor = new GameObject("RodGripAnchor").transform;
                        rodGripAnchor.SetParent(rightHand, false);
                        rodGripAnchor.localPosition = new Vector3(0f, 0.08f, 0.02f);
                        rodGripAnchor.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    }
                }

                if (rodGripAnchor == null)
                {
                    Transform[] children = animator.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < children.Length; i++)
                    {
                        if (!string.Equals(children[i].name, "RodGripAnchor", System.StringComparison.OrdinalIgnoreCase)) continue;
                        rodGripAnchor = children[i];
                        break;
                    }
                }
            }

            _baseLocalPosition = transform.localPosition;
            _baseLocalRotation = transform.localRotation;
            _lastState = FishingPlayerState.Idle;
            _bound = true;
        }

        public void Apply(FishingSnapshot snapshot, float feedbackIntensity)
        {
            if (snapshot == null) return;
            AutoBind();

            float breathe = Mathf.Sin(Time.time * 1.7f) * 0.012f;
            float fight = snapshot.State == FishingPlayerState.Fighting ? feedbackIntensity : 0f;
            float castPitch = snapshot.State == FishingPlayerState.Casting
                ? Mathf.Lerp(-12f, 9f, snapshot.CastPower)
                : 0f;
            float hookSnap = snapshot.State == FishingPlayerState.Hooked
                ? Mathf.Lerp(-15f, 0f, Mathf.Clamp01(snapshot.StateElapsedSeconds * 5f))
                : 0f;

            transform.localPosition = _baseLocalPosition + Vector3.up * breathe;
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(
                castPitch + hookSnap - fight * 7f,
                snapshot.State == FishingPlayerState.Fighting ? snapshot.FightDirection * 4f : 0f,
                snapshot.State == FishingPlayerState.Fighting ? -snapshot.FightDirection * fight * 6f : 0f);

            SetInteger("FishingState", (int)snapshot.State);
            SetFloat("CastPower", snapshot.CastPower);
            SetFloat("FightIntensity", fight);
            SetFloat("FightDirection", snapshot.FightDirection);
            SetBool("Reeling", snapshot.State == FishingPlayerState.Fighting);

            if (snapshot.State != _lastState)
            {
                SetTrigger(snapshot.State.ToString());
                _lastState = snapshot.State;
            }
        }

        private void SetFloat(string parameter, float value)
        {
            int hash = Animator.StringToHash(parameter);
            if (animator != null && _parameters.Contains(hash)) animator.SetFloat(hash, value);
        }

        private void SetInteger(string parameter, int value)
        {
            int hash = Animator.StringToHash(parameter);
            if (animator != null && _parameters.Contains(hash)) animator.SetInteger(hash, value);
        }

        private void SetBool(string parameter, bool value)
        {
            int hash = Animator.StringToHash(parameter);
            if (animator != null && _parameters.Contains(hash)) animator.SetBool(hash, value);
        }

        private void SetTrigger(string parameter)
        {
            int hash = Animator.StringToHash(parameter);
            if (animator != null && _parameters.Contains(hash)) animator.SetTrigger(hash);
        }
    }
}
