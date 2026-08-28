using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Input;
using TMPro;
using UnityEngine;

namespace JustSomeStars.Runtime.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class SurfaceInteractionProbe2D : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string availableText = "INTERACT";
        [SerializeField] private string activatedText = "SIGNAL LINKED";

        private readonly HashSet<Collider2D> nearbyActors =
            new HashSet<Collider2D>();
        private InputRouter inputRouter;

        public bool IsAvailable => nearbyActors.Count > 0;
        public bool IsActivated { get; private set; }

        public void Configure(
            TMP_Text targetLabel,
            string readyText,
            string completedText)
        {
            label = targetLabel != null
                ? targetLabel
                : throw new ArgumentNullException(nameof(targetLabel));
            availableText = !string.IsNullOrWhiteSpace(readyText)
                ? readyText
                : throw new ArgumentException(
                    "An interaction-ready label is required.",
                    nameof(readyText));
            activatedText = !string.IsNullOrWhiteSpace(completedText)
                ? completedText
                : throw new ArgumentException(
                    "An activated interaction label is required.",
                    nameof(completedText));
            RefreshLabel();
        }

        public void BindInput(InputRouter router)
        {
            if (router == null)
            {
                throw new ArgumentNullException(nameof(router));
            }

            if (inputRouter != null)
            {
                if (ReferenceEquals(inputRouter, router))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "SurfaceInteractionProbe2D is already bound to another " +
                    "InputRouter.");
            }

            inputRouter = router;
            inputRouter.GameplayCommandPerformed += OnGameplayCommand;
        }

        public void ReleaseInput(InputRouter router)
        {
            if (router == null)
            {
                throw new ArgumentNullException(nameof(router));
            }

            if (inputRouter == null)
            {
                return;
            }

            if (!ReferenceEquals(inputRouter, router))
            {
                throw new InvalidOperationException(
                    "SurfaceInteractionProbe2D can only release its owning " +
                    "InputRouter.");
            }

            inputRouter.GameplayCommandPerformed -= OnGameplayCommand;
            inputRouter = null;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other != null &&
                other.GetComponentInParent<SurfaceMotor2D>() != null)
            {
                nearbyActors.Add(other);
                RefreshLabel();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != null && nearbyActors.Remove(other))
            {
                RefreshLabel();
            }
        }

        private void OnGameplayCommand(
            GameplayInputMode mode,
            SemanticGameplayCommand command)
        {
            if (mode != GameplayInputMode.Surface ||
                command != SemanticGameplayCommand.Primary ||
                !IsAvailable)
            {
                return;
            }

            IsActivated = !IsActivated;
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (label == null)
            {
                return;
            }

            label.text = IsActivated ? activatedText : availableText;
            label.alpha = IsAvailable || IsActivated ? 1f : 0f;
        }

        private void OnDestroy()
        {
            if (inputRouter != null)
            {
                ReleaseInput(inputRouter);
            }
        }
    }
}
