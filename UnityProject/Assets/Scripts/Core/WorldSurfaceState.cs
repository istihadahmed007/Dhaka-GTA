namespace DhakaGta.Core
{
    /// <summary>
    /// Decoupled shared environmental telemetry written by HDRP weather drivers and consumed by
    /// vehicle traction/headlight systems without introducing circular assembly dependencies.
    /// </summary>
    public static class WorldSurfaceState
    {
        public static float GlobalRoadWetness { get; set; }
        public static float GlobalRainIntensity { get; set; }
        public static bool IsNightOrLowVisibility { get; set; }
        public static float CurrentTimeOfDayHours { get; set; } = 17.2f;
    }
}
