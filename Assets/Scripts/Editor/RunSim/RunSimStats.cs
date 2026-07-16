using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rollrate.Simulation
{
    /// <summary>
    /// Accumulates every statistic requested across an arbitrary number of
    /// simulated campaigns: runs-needed-to-win, purchase counts, module
    /// usage, Meta unlocks bought, Archive Test win rates, dismantle count,
    /// Collection swap count.
    /// </summary>
    public class RunSimStats
    {
        public List<int> RunsPerCampaign = new List<int>();
        public int AbandonedCampaigns; // hit maxRunsPerCampaign without winning

        public Dictionary<string, int> ShopPurchases = new Dictionary<string, int>();
        public Dictionary<string, int> ModuleUsageCount = new Dictionary<string, int>(); // incremented once per fight the module was equipped for
        public Dictionary<string, int> MetaUnlockPurchases = new Dictionary<string, int>();

        public int ArchiveResonanceWins, ArchiveResonanceTotal;
        public int ArchiveTributeWins, ArchiveTributeTotal;
        public int ArchiveAmbitionWins, ArchiveAmbitionTotal;

        public int DismantleCount;
        public int CollectionSwapCount;

        public void RecordPurchase(string itemName)
        {
            ShopPurchases.TryGetValue(itemName, out int c);
            ShopPurchases[itemName] = c + 1;
        }

        public void RecordModuleUsage(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName)) return;
            ModuleUsageCount.TryGetValue(moduleName, out int c);
            ModuleUsageCount[moduleName] = c + 1;
        }

        public void RecordMetaUnlock(string itemName)
        {
            MetaUnlockPurchases.TryGetValue(itemName, out int c);
            MetaUnlockPurchases[itemName] = c + 1;
        }

        public string FormatSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Campagne simulate: {RunsPerCampaign.Count} (abbandonate per limite sicurezza: {AbandonedCampaigns}) ===");

            if (RunsPerCampaign.Count > 0)
            {
                double avg = RunsPerCampaign.Average();
                int min = RunsPerCampaign.Min();
                int max = RunsPerCampaign.Max();
                sb.AppendLine($"Run necessarie per vincere: media {avg:F2}, min {min}, max {max}");
            }

            sb.AppendLine();
            sb.AppendLine("--- Acquisti Shop (dado/modulo: volte comprato) ---");
            foreach (var kvp in ShopPurchases.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            sb.AppendLine();
            sb.AppendLine("--- Moduli piu usati (fights equipaggiato) ---");
            foreach (var kvp in ModuleUsageCount.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            sb.AppendLine();
            sb.AppendLine("--- Sblocchi Meta acquistati ---");
            foreach (var kvp in MetaUnlockPurchases.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            sb.AppendLine();
            sb.AppendLine("--- Test di Archivio (win rate) ---");
            sb.AppendLine($"  Risonanza: {(ArchiveResonanceTotal > 0 ? (float)ArchiveResonanceWins / ArchiveResonanceTotal : 0):P1} ({ArchiveResonanceWins}/{ArchiveResonanceTotal})");
            sb.AppendLine($"  Tributo:   {(ArchiveTributeTotal > 0 ? (float)ArchiveTributeWins / ArchiveTributeTotal : 0):P1} ({ArchiveTributeWins}/{ArchiveTributeTotal})");
            sb.AppendLine($"  Ambizione: {(ArchiveAmbitionTotal > 0 ? (float)ArchiveAmbitionWins / ArchiveAmbitionTotal : 0):P1} ({ArchiveAmbitionWins}/{ArchiveAmbitionTotal})");

            sb.AppendLine();
            sb.AppendLine($"--- Smantellamenti totali: {DismantleCount} ---");
            sb.AppendLine($"--- Cambi modulo in Collezione/Falo totali: {CollectionSwapCount} ---");

            return sb.ToString();
        }
    }
}
