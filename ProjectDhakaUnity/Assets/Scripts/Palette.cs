using UnityEngine;

namespace ProjectDhaka
{
    /// <summary>
    /// Single source of truth for the Project Dhaka monsoon-dusk art bible palette.
    /// Every material, light, sky, particle, and UI color reads from here.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Floor = Hex("#1C2331");            // Wet Dhaka asphalt
        public static readonly Color FloorLane = Hex("#2E384D");        // Road lane / curb stone
        public static readonly Color Bounds = Hex("#2D3546");           // Monsoon concrete flyover/curb
        public static readonly Color WallsPrimary = Hex("#4B5568");     // Mid-rise concrete plaster
        public static readonly Color WallsTerracotta = Hex("#8C4A38");  // Old Dhaka heritage brick
        public static readonly Color CharacterPrimary = Hex("#2B5B84"); // Courier indigo jacket
        public static readonly Color CharacterSecondary = Hex("#1E9E6B"); // Dhaka CNG emerald
        public static readonly Color CharacterTrim = Hex("#EDE3D2");    // Warm collar/vis trim
        public static readonly Color Props = Hex("#425A66");            // Steel-teal stalls & Shapla base
        public static readonly Color Shadow = Hex("#111622");           // Deep monsoon dusk blue
        public static readonly Color Accent = Hex("#F5B731");           // Rickshaw marigold / intel glow
        public static readonly Color AccentBright = Hex("#FFD866");     // High-energy pickup bloom
        public static readonly Color Danger = Hex("#E84A43");           // Patrol / hazard crimson
        public static readonly Color KeyLight = Hex("#FFE2B8");         // Warm dusk break sun
        public static readonly Color SkyZenith = Hex("#1D2E4A");        // Dusk indigo zenith
        public static readonly Color SkyHorizon = Hex("#4B5972");       // Monsoon haze horizon
        public static readonly Color SkyGround = Hex("#151924");        // Deep dusk nadir
        public static readonly Color AmbientSky = Hex("#364D73");       // Trilight sky bounce
        public static readonly Color AmbientEquator = Hex("#475266");   // Trilight equator bounce
        public static readonly Color AmbientGround = Hex("#1A1E29");    // Trilight ground bounce
        public static readonly Color Fog = Hex("#232D3F");              // Monsoon evening haze
        public static readonly Color TextLight = Hex("#F5EFE6");        // Warm chalk HUD text
        public static readonly Color TextMuted = Hex("#B8C0CE");        // Secondary HUD text
        public static readonly Color Scrim = new Color(0.067f, 0.086f, 0.133f, 0.80f); // Shadow @ 80% alpha
        public static readonly Color PanelBg = new Color(0.110f, 0.137f, 0.192f, 0.90f); // Floor @ 90% alpha
        public static readonly Color Clear = new Color(0f, 0f, 0f, 0f); // Transparent tint

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
