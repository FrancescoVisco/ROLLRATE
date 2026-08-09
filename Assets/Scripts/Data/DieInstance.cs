using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollrate.Data
{
    /// <summary>
    /// One OWNED die: which DieData "kind" it is (D4, D6, ... - shared,
    /// static data: faces, ranges, Grade), its permanent DieType (Power/
    /// Stability/Flow/Echo - assigned at purchase, changed only by Fusion
    /// at the Furnace), and the list of Effects attached to THIS specific
    /// die (never a shared/generic list - two owned D6 Power dice can
    /// carry completely different Effects).
    ///
    /// Replaces the old flat "List&lt;DieData&gt;" pool representation:
    /// previously every die of the same kind was identical and
    /// interchangeable, since only Modules (attached to Slots, not dice)
    /// carried any extra state. Now the die itself is the unit that
    /// carries state, so the pool needs to track instances, not just kinds.
    /// </summary>
    [Serializable]
    public class DieInstance
    {
        public DieData data;
        public DieType type;
        public List<EffectData> effects = new List<EffectData>();

        public DieInstance() { }

        public DieInstance(DieData data, DieType type)
        {
            this.data = data;
            this.type = type;
        }

        /// <summary>True if this die already carries the given Effect (a die should never carry the same Effect twice - see Fusion's "no duplicates" rule).</summary>
        public bool HasEffect(EffectId id)
        {
            foreach (var e in effects)
            {
                if (e != null && e.id == id) return true;
            }
            return false;
        }

        /// <summary>Adds an Effect if this die doesn't already carry it. Returns false if it was already present (no-op).</summary>
        public bool AddEffect(EffectData effect)
        {
            if (effect == null || HasEffect(effect.id)) return false;
            effects.Add(effect);
            return true;
        }
    }
}
