using System;

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

    public interface IFrontendLifecycle
    {
        event Action BackRequested;

        void RequestExit();
    }
}
