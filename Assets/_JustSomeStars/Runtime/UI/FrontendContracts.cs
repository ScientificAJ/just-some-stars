using System;
using JustSomeStars.Runtime.Atlas;

namespace JustSomeStars.Runtime.UI
{
    public interface IFrontendView
    {
        bool IsReady { get; }

        event Action ContinueRequested;

        event Action SettingsRequested;

        event Action CreditsRequested;

        event Action PrivacyRequested;

        event Action CloseRequested;

        void PresentVersion(string versionText);

        void PresentContinue(bool interactable, string explanation);

        void ShowPanel(string title, string body);

        void HidePanel();
    }

    public enum FrontendContinueState
    {
        NoSave = 0,
        Ready = 1,
        RecoveredBackup = 2,
        Unreadable = 3,
        StorageUnavailable = 4,
        ContentUnavailable = 5,
        Loading = 6,
    }

    public readonly struct FrontendLaunchPresentation
    {
        public FrontendLaunchPresentation(
            bool newGameInteractable,
            string newGameExplanation,
            bool continueInteractable,
            string continueState,
            string continueExplanation,
            FrontendContinueState state)
        {
            NewGameInteractable = newGameInteractable;
            NewGameExplanation = newGameExplanation ?? string.Empty;
            ContinueInteractable = continueInteractable;
            ContinueState = continueState ?? string.Empty;
            ContinueExplanation = continueExplanation ?? string.Empty;
            State = state;
        }

        public bool NewGameInteractable { get; }
        public string NewGameExplanation { get; }
        public bool ContinueInteractable { get; }
        public string ContinueState { get; }
        public string ContinueExplanation { get; }
        public FrontendContinueState State { get; }
    }

    public interface IFrontendLaunchView
    {
        event Action NewGameRequested;

        void PresentLocalizedChrome(LocalizedEnglishCatalog english);

        void PresentLaunch(FrontendLaunchPresentation presentation);
    }

    public interface IFrontendLifecycle
    {
        event Action BackRequested;

        void RequestExit();
    }

    public interface IFrontendSettingsPanel
    {
        bool IsReady { get; }

        bool IsConfigured { get; }

        FrontendDependencies Dependencies { get; }

        void SetLocalization(LocalizedEnglishCatalog english);

        void Configure(FrontendDependencies dependencies);

        void Release(FrontendDependencies dependencies);

        void Show();

        void Hide();
    }
}
