using JustSomeStars.Runtime.Input;

namespace JustSomeStars.Runtime.Core
{
    public enum GameMode
    {
        Frontend = 0,
        Customization = 1,
        Clubhouse = 2,
        Flight = 3,
        Surface = 4,
        Lens = 5,
        Dialogue = 6,
        Cinematic = 7,
    }

    public enum GameOverlay
    {
        None = 0,
        Pause = 1,
        PhotoMode = 2,
        Settings = 3,
    }

    public enum GameCameraPolicy
    {
        Frontend = 0,
        Customization = 1,
        Clubhouse = 2,
        Flight = 3,
        Surface = 4,
        Lens = 5,
        Dialogue = 6,
        Cinematic = 7,
        Paused = 8,
        PhotoMode = 9,
        Settings = 10,
    }

    public enum GameModeTransitionResult
    {
        Unchanged = 0,
        Changed = 1,
    }

    public readonly struct GameModeRuntimePolicy
    {
        public GameModeRuntimePolicy(
            GameMode mode,
            GameOverlay overlay,
            GameplayInputMode inputMode,
            GameCameraPolicy cameraPolicy)
        {
            Mode = mode;
            Overlay = overlay;
            InputMode = inputMode;
            CameraPolicy = cameraPolicy;
        }

        public GameMode Mode { get; }

        public GameOverlay Overlay { get; }

        public GameplayInputMode InputMode { get; }

        public GameCameraPolicy CameraPolicy { get; }
    }
}
