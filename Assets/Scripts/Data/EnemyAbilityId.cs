namespace Rollrate.Data
{
    /// <summary>
    /// Which unique ability this enemy has (design doc Section 8). One
    /// enum value per named ability - the actual behavior lives in
    /// Combat/EnemyAbilityResolver.cs, not here (this is just the
    /// selector EnemyData uses to say WHICH one applies).
    /// </summary>
    public enum EnemyAbilityId
    {
        None,
        Static,      // Fragment
        Lockdown,    // Compiler
        Clockwork,   // Gatekeeper
        Pressure,    // Tracer
        Jammer,      // Sentinel
        Backlash,    // Eraser (enemy's own ability, unrelated to the player's Stability Effect of the same name)
        Discord,     // Cantor
        Feedback,    // Architect
        Refraction,  // Prism
        Tax,         // Inquisitor
        Stasis,      // Warden
        Sentence,    // Judge
        Void,        // Avatar
        Glitch,      // Null-Pointer
        Delete       // Sovereign
    }
}
