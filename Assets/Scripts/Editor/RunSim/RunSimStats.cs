using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rollrate.Simulation
{
    /// <summary>
    /// Accumulates every statistic requested across an arbitrary number of
    /// simulated campaigns.
    /// </summary>
    public class RunSimStats
    {
        public int TotalCampaigns;
        public List<int> RunsPerCampaign = new List<int>(); // only for campaigns that actually won
        public int Victories;
        public int AbandonedCampaigns; // hit maxRunsPerCampaign without winning

        public Dictionary<string, int> ShopPurchases = new Dictionary<string, int>();
        public Dictionary<string, int> ModuleUsageCount = new Dictionary<string, int>(); // incremented once per fight the module was equipped for
        public Dictionary<string, int> MetaUnlockPurchases = new Dictionary<string, int>();
        public HashSet<string> AllKnownModules = new HashSet<string>(); // populated once from config, to find modules that NEVER got used

        public int ArchiveResonanceWins, ArchiveResonanceTotal;
        public int ArchiveTributeWins, ArchiveTributeTotal;
        public int ArchiveAmbitionWins, ArchiveAmbitionTotal;

        public int DismantleCount;
        public int CollectionSwapCount;

        public long TotalTurnsInWonFights;
        public int WonFightsCount;

        // --- Where/why/how runs end ---
        public Dictionary<int, int> DeathsByGrade = new Dictionary<int, int>();
        public int DeathsFromCombat;
        public int DeathsFromAmbizione;
        public Dictionary<string, int> DeathsByEnemyName = new Dictionary<string, int>();

        // --- Full Resonance frequency across ALL real fights (not just won ones) ---
        public long TotalTurnsAllFights;
        public long FullResonanceTurnsAllFights;

        // --- Scrap economy ---
        public List<int> ScrapAtRunEnd = new List<int>(); // leftover Scrap whenever a run ends, win or lose

        // --- Core Die evolution reached ---
        public Dictionary<string, int> CoreGradeAtCampaignEnd = new Dictionary<string, int>();

        // --- How far a run gets before dying ---
        public List<int> NodesResolvedBeforeDeath = new List<int>();

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

        public void RecordDeathByGrade(int grade)
        {
            DeathsByGrade.TryGetValue(grade, out int c);
            DeathsByGrade[grade] = c + 1;
        }

        public void RecordDeathByEnemy(string enemyName)
        {
            if (string.IsNullOrEmpty(enemyName)) return;
            DeathsByEnemyName.TryGetValue(enemyName, out int c);
            DeathsByEnemyName[enemyName] = c + 1;
        }

        public void RecordCoreGradeAtCampaignEnd(string coreDieName)
        {
            CoreGradeAtCampaignEnd.TryGetValue(coreDieName, out int c);
            CoreGradeAtCampaignEnd[coreDieName] = c + 1;
        }

        public string FormatSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Campagne simulate: {TotalCampaigns} ===");
            sb.AppendLine($"Vittorie: {Victories}/{TotalCampaigns} campagne ({(TotalCampaigns > 0 ? (float)Victories / TotalCampaigns : 0):P1})");
            sb.AppendLine($"Campagne abbandonate per limite di sicurezza (mai vinte): {AbandonedCampaigns}");

            if (RunsPerCampaign.Count > 0)
            {
                double avg = RunsPerCampaign.Average();
                int min = RunsPerCampaign.Min();
                int max = RunsPerCampaign.Max();
                sb.AppendLine($"Run per campagna vinta (solo tra le campagne vinte): media {avg:F2}, min {min}, max {max}");
            }

            sb.AppendLine($"Turni medi per combattimento vinto: {(WonFightsCount > 0 ? (float)TotalTurnsInWonFights / WonFightsCount : 0):F2} ({WonFightsCount} combattimenti vinti totali)");

            sb.AppendLine();
            sb.AppendLine("--- A che Grado si muore (tutte le sconfitte, per Grado) ---");
            foreach (var kvp in DeathsByGrade.OrderBy(k => k.Key))
                sb.AppendLine($"  Grado {kvp.Key}: {kvp.Value}");

            int totalDeaths = DeathsFromCombat + DeathsFromAmbizione;
            sb.AppendLine();
            sb.AppendLine("--- Causa della sconfitta ---");
            sb.AppendLine($"  Combattimento: {DeathsFromCombat} ({(totalDeaths > 0 ? (float)DeathsFromCombat / totalDeaths : 0):P1})");
            sb.AppendLine($"  Test di Ambizione: {DeathsFromAmbizione} ({(totalDeaths > 0 ? (float)DeathsFromAmbizione / totalDeaths : 0):P1})");

            sb.AppendLine();
            sb.AppendLine("--- Nemico che uccide di piu ---");
            foreach (var kvp in DeathsByEnemyName.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            sb.AppendLine();
            sb.AppendLine($"--- Frequenza Risonanza Totale (tutti i combattimenti reali, vinti o persi): {(TotalTurnsAllFights > 0 ? (float)FullResonanceTurnsAllFights / TotalTurnsAllFights : 0):P2} ({FullResonanceTurnsAllFights}/{TotalTurnsAllFights} turni) ---");

            sb.AppendLine();
            if (ScrapAtRunEnd.Count > 0)
            {
                sb.AppendLine($"--- Scrap residuo a fine run (media {ScrapAtRunEnd.Average():F1}, min {ScrapAtRunEnd.Min()}, max {ScrapAtRunEnd.Max()}) ---");
            }

            sb.AppendLine();
            if (NodesResolvedBeforeDeath.Count > 0)
            {
                sb.AppendLine($"--- Nodi superati prima di morire (solo run terminate in sconfitta): media {NodesResolvedBeforeDeath.Average():F2}, min {NodesResolvedBeforeDeath.Min()}, max {NodesResolvedBeforeDeath.Max()} ---");
            }

            sb.AppendLine();
            sb.AppendLine("--- Grado del Core a fine campagna (vittoria o abbandono) ---");
            foreach (var kvp in CoreGradeAtCampaignEnd.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            sb.AppendLine();
            sb.AppendLine("--- Acquisti Shop (dado/modulo: volte comprato) ---");
            foreach (var kvp in ShopPurchases.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            sb.AppendLine();
            sb.AppendLine("--- Moduli piu usati (fights equipaggiato) ---");
            foreach (var kvp in ModuleUsageCount.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            var neverUsed = AllKnownModules.Where(m => !ModuleUsageCount.ContainsKey(m)).ToList();
            sb.AppendLine();
            sb.AppendLine("--- Moduli MAI equipaggiati in un combattimento ---");
            if (neverUsed.Count == 0) sb.AppendLine("  (nessuno - tutti i moduli conosciuti sono stati usati almeno una volta)");
            foreach (string name in neverUsed) sb.AppendLine($"  {name}");

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
