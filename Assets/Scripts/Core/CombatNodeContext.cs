using Rollrate.Data;

namespace Rollrate.Core
{
    /// <summary>
    /// Static bridge between the Map and the Combat scene, since they're
    /// separate additively-loaded scenes with no direct object references
    /// to each other. Not persisted/serialized - just a runtime handoff.
    /// </summary>
    public static class CombatNodeContext
    {
        /// <summary>Set by MapController before entering CombatScene; read (and cleared) by EnemyController on Start.</summary>
        public static EnemyData PendingEnemy;

        /// <summary>Set by CombatController right before exiting CombatScene; read by MapController after the scene unloads.</summary>
        public static bool LastFightWasVictory;
    }
}
