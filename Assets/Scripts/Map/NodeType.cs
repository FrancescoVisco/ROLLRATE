namespace Rollrate.Map
{
    /// <summary>
    /// The 7 regular node types from the design doc (Section 7), plus
    /// Terminal (the forced boss node at the end of Page 3 - not part of
    /// the normal percentage roll, always placed explicitly).
    /// </summary>
    public enum NodeType
    {
        Conflict,       // Nodo Conflitto
        Merchant,       // Nodo Mercante
        Archive,        // Nodo Archivio
        Overload,       // Nodo Sovraccarico (Elite)
        Glitch,         // Nodo Glitch
        Bonfire,        // Nodo Falò
        Dismantle,      // Nodo Smantellamento
        Terminal        // Nodo Terminale (Boss + Ricalibrazione) - forced, not rolled
    }
}
