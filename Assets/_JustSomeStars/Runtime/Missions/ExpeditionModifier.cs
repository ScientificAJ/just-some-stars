namespace JustSomeStars.Runtime.Missions
{
    public enum ExpeditionModifier
    {
        None = 0,
        ReducedHud = 1,
        CinematicLetterbox = 2,
        SignalEchoes = 3,
        CompanionSpotlight = 4,
        NoDamagePractice = 5,
    }

    public sealed class ExpeditionModifierProfile
    {
        public ExpeditionModifierProfile(
            bool reducedHud,
            bool cinematicLetterbox,
            bool signalEchoes,
            bool companionSpotlight,
            bool damageEnabled)
        {
            ReducedHud = reducedHud;
            CinematicLetterbox = cinematicLetterbox;
            SignalEchoes = signalEchoes;
            CompanionSpotlight = companionSpotlight;
            DamageEnabled = damageEnabled;
        }

        public bool ReducedHud { get; }
        public bool CinematicLetterbox { get; }
        public bool SignalEchoes { get; }
        public bool CompanionSpotlight { get; }
        public bool DamageEnabled { get; }
    }
}
