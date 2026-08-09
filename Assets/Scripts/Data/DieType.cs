namespace Rollrate.Data
{
    /// <summary>
    /// The permanent Type of an owned die, assigned at purchase and
    /// changeable only via Fusion at the Furnace node (see design doc
    /// Section 5/7). Unlike the old Slot system, this is a property of
    /// the die itself - dice are never "placed" anywhere.
    ///
    /// Colors (used consistently across all die visuals): Power = red,
    /// Stability = blue, Flow = purple, Echo = green.
    /// </summary>
    public enum DieType
    {
        Power,
        Stability,
        Flow,
        Echo
    }
}
