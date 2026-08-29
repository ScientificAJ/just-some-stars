namespace JustSomeStars.Runtime.Discovery
{
    public enum LensMode
    {
        Imaging = 0,
        Spectrum = 1,
        Temperature = 2,
        Atmosphere = 3,
        Motion = 4,
        Signal = 5,
    }

    public enum LensFocusBehavior
    {
        Point = 0,
        Track = 1,
        Region = 2,
    }

    public enum LensReticleState
    {
        Inactive = 0,
        Searching = 1,
        Focused = 2,
        Incompatible = 3,
        Scanning = 4,
        Complete = 5,
    }
}
