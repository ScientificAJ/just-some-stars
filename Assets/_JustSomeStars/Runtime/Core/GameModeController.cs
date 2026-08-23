using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Input;
using UnityEngine;

namespace JustSomeStars.Runtime.Core
{
    internal interface IGameModeRuntimeHooks
    {
        ValueTask ApplyAsync(
            GameModeRuntimePolicy policy,
            CancellationToken cancellationToken);
    }

    internal sealed class InputRouterGameModeRuntimeHooks :
        IGameModeRuntimeHooks
    {
        private readonly InputRouter m_Input;

        public InputRouterGameModeRuntimeHooks(InputRouter input)
        {
            m_Input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public event Action<GameCameraPolicy> CameraPolicyChanged;

        internal InputRouter Input => m_Input;

        internal GameModeRuntimePolicy CurrentPolicy { get; private set; }

        public ValueTask ApplyAsync(
            GameModeRuntimePolicy policy,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            m_Input.SetGameplayMode(policy.InputMode);
            CameraPolicyChanged?.Invoke(policy.CameraPolicy);
            CurrentPolicy = policy;
            return default;
        }
    }

    internal sealed class NoOpGameModeRuntimeHooks : IGameModeRuntimeHooks
    {
        public static readonly NoOpGameModeRuntimeHooks Instance =
            new NoOpGameModeRuntimeHooks();

        private NoOpGameModeRuntimeHooks()
        {
        }

        public ValueTask ApplyAsync(
            GameModeRuntimePolicy policy,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return default;
        }
    }

    public sealed class GameModeController : IGameService
    {
        private static readonly IReadOnlyDictionary<GameMode, GameMode[]>
            AllowedTransitions = new Dictionary<GameMode, GameMode[]>
            {
                [GameMode.Frontend] = new[] { GameMode.Customization },
                [GameMode.Customization] = new[]
                {
                    GameMode.Frontend,
                    GameMode.Clubhouse,
                },
                [GameMode.Clubhouse] = new[]
                {
                    GameMode.Customization,
                    GameMode.Flight,
                },
                [GameMode.Flight] = new[]
                {
                    GameMode.Clubhouse,
                    GameMode.Surface,
                },
                [GameMode.Surface] = new[]
                {
                    GameMode.Flight,
                    GameMode.Lens,
                    GameMode.Dialogue,
                    GameMode.Cinematic,
                },
                [GameMode.Lens] = new[] { GameMode.Surface },
                [GameMode.Dialogue] = new[] { GameMode.Surface },
                [GameMode.Cinematic] = new[] { GameMode.Surface },
            };

        private readonly object m_Gate = new object();
        private readonly IGameModeRuntimeHooks m_RuntimeHooks;

        private GameMode m_CurrentMode;
        private GameOverlay m_ActiveOverlay;
        private bool m_IsInitialized;
        private bool m_TransitionInFlight;
        private Task m_ActiveTransitionTask;
        private CancellationTokenSource m_ActiveTransitionCancellation;
        private Task m_ShutdownTask;

        internal GameModeController(
            GameMode initialMode,
            IGameModeRuntimeHooks runtimeHooks)
        {
            ThrowIfInvalidMode(initialMode, nameof(initialMode));
            m_CurrentMode = initialMode;
            m_RuntimeHooks = runtimeHooks ??
                throw new ArgumentNullException(nameof(runtimeHooks));
        }

        public event Action<GameModeRuntimePolicy> StateChanged;

        public GameMode CurrentMode
        {
            get
            {
                lock (m_Gate)
                {
                    return m_CurrentMode;
                }
            }
        }

        public GameOverlay ActiveOverlay
        {
            get
            {
                lock (m_Gate)
                {
                    return m_ActiveOverlay;
                }
            }
        }

        public bool IsInitialized
        {
            get
            {
                lock (m_Gate)
                {
                    return m_IsInitialized;
                }
            }
        }

        internal IGameModeRuntimeHooks RuntimeHooks => m_RuntimeHooks;

        internal static GameModeController CreateForTests(
            GameMode initialMode,
            IGameModeRuntimeHooks runtimeHooks = null)
        {
            return new GameModeController(
                initialMode,
                runtimeHooks ?? NoOpGameModeRuntimeHooks.Instance);
        }

        public bool CanEnter(GameMode destination)
        {
            ThrowIfInvalidMode(destination, nameof(destination));
            lock (m_Gate)
            {
                if (destination == m_CurrentMode)
                {
                    return true;
                }

                return Array.IndexOf(
                    AllowedTransitions[m_CurrentMode],
                    destination) >= 0;
            }
        }

        public bool CanOpenOverlay(GameOverlay overlay)
        {
            ThrowIfInvalidOverlay(overlay, nameof(overlay));
            if (overlay == GameOverlay.None)
            {
                return false;
            }

            lock (m_Gate)
            {
                return IsOverlayAllowed(m_CurrentMode, overlay);
            }
        }

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (m_Gate)
            {
                if (m_IsInitialized)
                {
                    return new ValueTask<StartupResult>(StartupResult.Available());
                }

                if (m_ShutdownTask != null)
                {
                    throw new InvalidOperationException(
                        "GameModeController cannot initialize after shutdown begins.");
                }

                BeginTransitionLocked();
                var ownedCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);
                var task = RunInitializeAsync(
                    CreatePolicy(m_CurrentMode, m_ActiveOverlay),
                    ownedCancellation);
                m_ActiveTransitionCancellation = ownedCancellation;
                m_ActiveTransitionTask = task;
                return new ValueTask<StartupResult>(task);
            }
        }

        public ValueTask ShutdownAsync()
        {
            CancellationTokenSource cancellation = null;
            Task shutdown;
            lock (m_Gate)
            {
                if (m_ShutdownTask == null)
                {
                    cancellation = m_ActiveTransitionCancellation;
                    m_ShutdownTask = RunShutdownAsync(m_ActiveTransitionTask);
                }

                shutdown = m_ShutdownTask;
            }

            TryCancel(cancellation);
            return new ValueTask(shutdown);
        }

        public ValueTask<GameModeTransitionResult> EnterAsync(
            GameMode destination,
            CancellationToken cancellationToken)
        {
            ThrowIfInvalidMode(destination, nameof(destination));
            return ChangeBaseModeAsync(
                destination,
                enforceTransitionTable: true,
                cancellationToken);
        }

        internal ValueTask<GameModeTransitionResult> RecoverAsync(
            GameMode destination,
            CancellationToken cancellationToken)
        {
            ThrowIfInvalidMode(destination, nameof(destination));
            return ChangeBaseModeAsync(
                destination,
                enforceTransitionTable: false,
                cancellationToken);
        }

        public ValueTask<GameModeTransitionResult> OpenOverlayAsync(
            GameOverlay overlay,
            CancellationToken cancellationToken)
        {
            ThrowIfInvalidOverlay(overlay, nameof(overlay));
            if (overlay == GameOverlay.None)
            {
                throw new ArgumentException(
                    "GameOverlay.None cannot be opened.",
                    nameof(overlay));
            }

            GameMode mode;
            GameOverlay previousOverlay;
            lock (m_Gate)
            {
                EnsureOperationalLocked();
                if (m_ActiveOverlay == overlay)
                {
                    return new ValueTask<GameModeTransitionResult>(
                        GameModeTransitionResult.Unchanged);
                }

                if (m_ActiveOverlay != GameOverlay.None)
                {
                    throw new InvalidOperationException(
                        "Game overlays cannot be nested or replaced.");
                }

                if (!IsOverlayAllowed(m_CurrentMode, overlay))
                {
                    throw new InvalidOperationException(
                        $"Overlay '{overlay}' is not allowed in mode '{m_CurrentMode}'.");
                }

                mode = m_CurrentMode;
                previousOverlay = m_ActiveOverlay;
                return BeginStateTransitionLocked(
                    mode,
                    previousOverlay,
                    mode,
                    overlay,
                    cancellationToken);
            }
        }

        public ValueTask<GameModeTransitionResult> CloseOverlayAsync(
            CancellationToken cancellationToken)
        {
            GameMode mode;
            GameOverlay previousOverlay;
            lock (m_Gate)
            {
                EnsureOperationalLocked();
                if (m_ActiveOverlay == GameOverlay.None)
                {
                    return new ValueTask<GameModeTransitionResult>(
                        GameModeTransitionResult.Unchanged);
                }

                mode = m_CurrentMode;
                previousOverlay = m_ActiveOverlay;
                return BeginStateTransitionLocked(
                    mode,
                    previousOverlay,
                    mode,
                    GameOverlay.None,
                    cancellationToken);
            }
        }

        private ValueTask<GameModeTransitionResult> ChangeBaseModeAsync(
            GameMode destination,
            bool enforceTransitionTable,
            CancellationToken cancellationToken)
        {
            GameMode previousMode;
            GameOverlay previousOverlay;
            lock (m_Gate)
            {
                EnsureOperationalLocked();
                if (enforceTransitionTable &&
                    m_ActiveOverlay != GameOverlay.None)
                {
                    throw new InvalidOperationException(
                        "Base mode cannot change while an overlay is open.");
                }

                if (destination == m_CurrentMode &&
                    m_ActiveOverlay == GameOverlay.None)
                {
                    return new ValueTask<GameModeTransitionResult>(
                        GameModeTransitionResult.Unchanged);
                }

                if (enforceTransitionTable &&
                    Array.IndexOf(AllowedTransitions[m_CurrentMode], destination) < 0)
                {
                    throw new InvalidOperationException(
                        $"Game mode transition '{m_CurrentMode}' -> " +
                        $"'{destination}' is not allowed.");
                }

                previousMode = m_CurrentMode;
                previousOverlay = m_ActiveOverlay;
                return BeginStateTransitionLocked(
                    previousMode,
                    previousOverlay,
                    destination,
                    GameOverlay.None,
                    cancellationToken);
            }
        }

        private ValueTask<GameModeTransitionResult> BeginStateTransitionLocked(
            GameMode previousMode,
            GameOverlay previousOverlay,
            GameMode nextMode,
            GameOverlay nextOverlay,
            CancellationToken cancellationToken)
        {
            BeginTransitionLocked();
            var ownedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            var task = RunStateTransitionAsync(
                previousMode,
                previousOverlay,
                nextMode,
                nextOverlay,
                ownedCancellation);
            m_ActiveTransitionCancellation = ownedCancellation;
            m_ActiveTransitionTask = task;
            return new ValueTask<GameModeTransitionResult>(task);
        }

        private async Task<StartupResult> RunInitializeAsync(
            GameModeRuntimePolicy policy,
            CancellationTokenSource ownedCancellation)
        {
            await Task.Yield();
            try
            {
                await m_RuntimeHooks.ApplyAsync(
                    policy,
                    ownedCancellation.Token);
                ownedCancellation.Token.ThrowIfCancellationRequested();
                lock (m_Gate)
                {
                    m_IsInitialized = true;
                }

                return StartupResult.Available();
            }
            finally
            {
                CompleteTransition(ownedCancellation);
            }
        }

        private async Task<GameModeTransitionResult> RunStateTransitionAsync(
            GameMode previousMode,
            GameOverlay previousOverlay,
            GameMode nextMode,
            GameOverlay nextOverlay,
            CancellationTokenSource ownedCancellation)
        {
            await Task.Yield();
            var cancellationToken = ownedCancellation.Token;
            var previousPolicy = CreatePolicy(previousMode, previousOverlay);
            var nextPolicy = CreatePolicy(nextMode, nextOverlay);
            try
            {
                try
                {
                    await m_RuntimeHooks.ApplyAsync(nextPolicy, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception transitionFailure)
                {
                    try
                    {
                        await m_RuntimeHooks.ApplyAsync(
                            previousPolicy,
                            CancellationToken.None);
                    }
                    catch (Exception rollbackFailure)
                    {
                        throw new AggregateException(
                            "Game mode hook failed and its prior policy could not " +
                            "be restored.",
                            transitionFailure,
                            rollbackFailure);
                    }

                    throw;
                }

                lock (m_Gate)
                {
                    m_CurrentMode = nextMode;
                    m_ActiveOverlay = nextOverlay;
                }

                PublishStateChanged(nextPolicy);
                return GameModeTransitionResult.Changed;
            }
            finally
            {
                CompleteTransition(ownedCancellation);
            }
        }

        private async Task RunShutdownAsync(Task activeTransition)
        {
            await Task.Yield();
            if (activeTransition != null)
            {
                try
                {
                    await activeTransition;
                }
                catch (Exception)
                {
                    // The transition caller observes its own failure. Shutdown still
                    // owns restoring one deterministic, quiescent policy.
                }
            }

            var safePolicy = CreatePolicy(GameMode.Frontend, GameOverlay.None);
            bool shouldApplyPolicy;
            lock (m_Gate)
            {
                shouldApplyPolicy = m_IsInitialized;
                m_TransitionInFlight = true;
            }

            try
            {
                if (shouldApplyPolicy)
                {
                    await m_RuntimeHooks.ApplyAsync(
                        safePolicy,
                        CancellationToken.None);
                }
            }
            finally
            {
                lock (m_Gate)
                {
                    m_CurrentMode = GameMode.Frontend;
                    m_ActiveOverlay = GameOverlay.None;
                    m_IsInitialized = false;
                    m_TransitionInFlight = false;
                }
            }
        }

        private void CompleteTransition(
            CancellationTokenSource ownedCancellation)
        {
            lock (m_Gate)
            {
                if (ReferenceEquals(
                        m_ActiveTransitionCancellation,
                        ownedCancellation))
                {
                    m_ActiveTransitionCancellation = null;
                    m_ActiveTransitionTask = null;
                }

                m_TransitionInFlight = false;
            }

            ownedCancellation.Dispose();
        }

        private static void TryCancel(CancellationTokenSource cancellation)
        {
            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void EnsureOperationalLocked()
        {
            if (!m_IsInitialized)
            {
                throw new InvalidOperationException(
                    "GameModeController must be initialized before changing state.");
            }

            if (m_ShutdownTask != null)
            {
                throw new InvalidOperationException(
                    "GameModeController cannot change state after shutdown begins.");
            }

            if (m_TransitionInFlight)
            {
                throw new InvalidOperationException(
                    "Another game mode or overlay transition is already in flight.");
            }
        }

        private void BeginTransitionLocked()
        {
            if (m_TransitionInFlight)
            {
                throw new InvalidOperationException(
                    "Another game mode or overlay transition is already in flight.");
            }

            m_TransitionInFlight = true;
        }

        private void PublishStateChanged(GameModeRuntimePolicy policy)
        {
            var handlers = StateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<GameModeRuntimePolicy> handler in
                     handlers.GetInvocationList())
            {
                try
                {
                    handler(policy);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static GameModeRuntimePolicy CreatePolicy(
            GameMode mode,
            GameOverlay overlay)
        {
            if (overlay != GameOverlay.None)
            {
                return new GameModeRuntimePolicy(
                    mode,
                    overlay,
                    GameplayInputMode.None,
                    overlay switch
                    {
                        GameOverlay.Pause => GameCameraPolicy.Paused,
                        GameOverlay.PhotoMode => GameCameraPolicy.PhotoMode,
                        GameOverlay.Settings => GameCameraPolicy.Settings,
                        _ => throw new ArgumentOutOfRangeException(nameof(overlay)),
                    });
            }

            var inputMode = mode switch
            {
                GameMode.Flight => GameplayInputMode.Flight,
                GameMode.Surface => GameplayInputMode.Surface,
                GameMode.Lens => GameplayInputMode.Lens,
                _ => GameplayInputMode.None,
            };
            return new GameModeRuntimePolicy(
                mode,
                GameOverlay.None,
                inputMode,
                (GameCameraPolicy)mode);
        }

        private static bool IsOverlayAllowed(GameMode mode, GameOverlay overlay)
        {
            return overlay switch
            {
                GameOverlay.Settings => true,
                GameOverlay.Pause => mode == GameMode.Clubhouse ||
                    mode == GameMode.Flight ||
                    mode == GameMode.Surface ||
                    mode == GameMode.Lens,
                GameOverlay.PhotoMode => mode == GameMode.Clubhouse ||
                    mode == GameMode.Flight ||
                    mode == GameMode.Surface,
                _ => false,
            };
        }

        private static void ThrowIfInvalidMode(GameMode mode, string parameterName)
        {
            if (!Enum.IsDefined(typeof(GameMode), mode))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ThrowIfInvalidOverlay(
            GameOverlay overlay,
            string parameterName)
        {
            if (!Enum.IsDefined(typeof(GameOverlay), overlay))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
