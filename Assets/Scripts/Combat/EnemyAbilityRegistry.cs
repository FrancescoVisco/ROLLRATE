using System.Collections.Generic;
using Rollrate.Data;
using Rollrate.Combat.Enemies;

namespace Rollrate.Combat
{
    /// <summary>
    /// Single place where every enemy ability's logic gets registered.
    /// All 15 enemies have real, functional ability logic.
    /// </summary>
    public static class EnemyAbilityRegistry
    {
        private static readonly Dictionary<EnemyAbilityId, EnemyAbilityBase> _map = new()
        {
            // Grade I
            { EnemyAbilityId.Fragment, new FragmentAbility() },
            { EnemyAbilityId.Compiler, new CompilerAbility() },
            { EnemyAbilityId.Gatekeeper, new GatekeeperAbility() },

            // Grade II
            { EnemyAbilityId.Tracer, new TracerAbility() },
            { EnemyAbilityId.Sentinel, new SentinelAbility() },
            { EnemyAbilityId.Eraser, new EraserAbility() },

            // Grade III
            { EnemyAbilityId.Cantor, new CantorAbility() },
            { EnemyAbilityId.Architect, new ArchitectAbility() },
            { EnemyAbilityId.Prism, new PrismAbility() },

            // Grade IV
            { EnemyAbilityId.Inquisitor, new InquisitorAbility() },
            { EnemyAbilityId.Warden, new WardenAbility() },
            { EnemyAbilityId.Judge, new JudgeAbility() },

            // Grade V
            { EnemyAbilityId.Avatar, new AvatarAbility() },
            { EnemyAbilityId.NullPointer, new NullPointerAbility() },
            { EnemyAbilityId.Sovereign, new SovereignAbility() },
        };

        public static EnemyAbilityBase Get(EnemyAbilityId id)
        {
            if (_map.TryGetValue(id, out var logic)) return logic;
            throw new System.NotImplementedException($"Logic not yet implemented for enemy ability {id}");
        }
    }
}
