using System.Collections.Generic;
using Rollrate.Data;
using Rollrate.Combat.Modules;

namespace Rollrate.Combat
{
    /// <summary>
    /// Single place where every module's logic gets registered.
    /// Add a line here each time you write a new *Logic class.
    /// </summary>
    public static class ModuleLogicRegistry
    {
        private static readonly Dictionary<ModuleId, ModuleLogicBase> _map = new()
        {
            // Power
            { ModuleId.Charge, new ChargeLogic() },
            { ModuleId.Affinity, new AffinityLogic() },
            { ModuleId.Solitary, new SolitaryLogic() },
            { ModuleId.Constitution, new ConstitutionLogic() },
            // Stability
            { ModuleId.Covering, new CoveringLogic() },
            { ModuleId.Changeover, new ChangeoverLogic() },
            { ModuleId.Aiming, new AimingLogic() },
            { ModuleId.Shield, new ShieldLogic() },
            // Flow
            { ModuleId.Mirror, new MirrorLogic() },
            { ModuleId.Shift, new ShiftLogic() },
            { ModuleId.Reverse, new ReverseLogic() },
            { ModuleId.SecondChance, new SecondChanceLogic() },
            // Echo
            { ModuleId.Reinforcements, new ReinforcementsLogic() },
            { ModuleId.Scrap, new ScrapLogic() },
            { ModuleId.Sync, new SyncLogic() },
            { ModuleId.Overload, new OverloadLogic() },
        };

        public static ModuleLogicBase Get(ModuleId id)
        {
            if (_map.TryGetValue(id, out var logic)) return logic;
            throw new System.NotImplementedException($"Logic not yet implemented for {id}");
        }
    }
}
