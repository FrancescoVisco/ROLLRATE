using System.Collections.Generic;
using Rollrate.Data;
using Rollrate.Core;

namespace Rollrate.Combat
{
    /// <summary>
    /// Bundle of everything an enemy ability might need to compute its
    /// effect: the turn's CombatContext, the effective (post-manipulation)
    /// placed values and modules per slot, the full GameState (for pool
    /// composition checks like Inquisitor), and the EnemyController itself
    /// (for abilities that read/write their own persistent state, like
    /// Prism's remembered target slot or Judge's growing inhibited set).
    /// </summary>
    public class EnemyAbilityContext
    {
        public CombatContext Ctx;
        public Dictionary<SlotType, int> PlacedValues;
        public Dictionary<SlotType, ModuleData> InstalledModules;
        public GameState State;
        public EnemyController Enemy;
    }
}
