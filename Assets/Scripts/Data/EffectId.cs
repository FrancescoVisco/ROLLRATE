namespace Rollrate.Data
{
    /// <summary>
    /// All 16 Effects (design doc Section 5). Each belongs to exactly
    /// one DieType and one Grade (I-V, Grade I has no Effects - see
    /// EffectData.grade). Replaces the old ModuleId entirely.
    /// </summary>
    public enum EffectId
    {
        // Power
        Overflow, Drain, Overkill, Breach,
        // Stability
        Backlash, Bulwark, Suppress, Cushion,
        // Flow
        Overclock, Reflect, SafetyNet, Cascade,
        // Echo
        Amplify, Cleanse, Reverb, Chain
    }
}
