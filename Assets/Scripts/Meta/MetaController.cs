using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rollrate.Data;
using Rollrate.Shop;

namespace Rollrate.Meta
{
    /// <summary>
    /// Logic for the Meta screen: lists every module/die across all 5
    /// Grade pools (deduplicated), lets the player spend Frammenti Residui
    /// to push one Grade earlier at a time. Does NOT depend on RunManager/
    /// GameState at all - meta-progression persists outside any run, via
    /// MetaProgressionManager's PlayerPrefs storage.
    /// </summary>
    public class MetaController : MonoBehaviour
    {
        [SerializeField] private ShopOfferPools offerPools;

        public int ResidualFragments => MetaProgressionManager.ResidualFragments;

        public List<DieData> GetAllDice()
        {
            var all = new List<DieData>();
            foreach (GradeOfferPool pool in AllPools())
            {
                foreach (DieData d in pool.dice)
                {
                    if (d != null && !all.Contains(d)) all.Add(d);
                }
            }
            return all.OrderBy(d => d.grade).ToList();
        }

        public List<ModuleData> GetAllModules()
        {
            var all = new List<ModuleData>();
            foreach (GradeOfferPool pool in AllPools())
            {
                foreach (ModuleData m in pool.modules)
                {
                    if (m != null && !all.Contains(m)) all.Add(m);
                }
            }
            return all.OrderBy(m => m.grade).ToList();
        }

        private IEnumerable<GradeOfferPool> AllPools()
        {
            yield return offerPools.gradeI;
            yield return offerPools.gradeII;
            yield return offerPools.gradeIII;
            yield return offerPools.gradeIV;
            yield return offerPools.gradeV;
        }
    }
}
