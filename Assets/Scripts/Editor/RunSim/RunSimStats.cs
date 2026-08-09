using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rollrate.Simulation
{
    /// <summary>
    /// Accumulates every statistic requested across an arbitrary number of
    /// simulated campaigns. Updated for the current Dice-Type/Effects
    /// system - no more Modules/Dismantle (removed systems), tracks
    /// Furnace fusions and Archive Test outcomes for the actual 3 Tests
    /// instead.
    /// </summary>
    public class RunSimStats
    {
        public int TotalCampaigns;
        public List<int> RunsPerCampaign = new List<int>(); // only for campaigns that actually won
        public int Victories;
        public int AbandonedCampaigns; // hit maxRunsPerCampaign without winning

        public Dictionary<string, int> ShopDicePurchases = new Dictionary<string, int>(); // key: "Type DFaces", e.g. "Power D8"
        public int ShopMaxHpPurchases;
        public int ShopRerolls;
        public int FurnaceFusions;
        public Dictionary<string, int> MetaUnlockPurchases = new Dictionary<string, int>(); // die kept at the Meta screen, by "Type DFaces"

        public int ArchiveResonanceWins, ArchiveResonanceTotal;
        public int ArchiveTributeWins, ArchiveTributeTotal;
        public int ArchiveAmbitionWins, ArchiveAmbitionTotal;

        public long TotalTurnsInWonFights;
        public int WonFightsCount;

        // --- Where/why/how runs end ---
        public Dictionary<int, int> DeathsByGrade = new Dictionary<int, int>();
        public int DeathsFromCombat;
        public int DeathsFromAmbizione;
        public Dictionary<string, int> DeathsByEnemyName = new Dictionary<string, int>();

        // --- Vibrazione bonus frequency across ALL real fights (not just won ones) ---
        public long TotalTurnsAllFights;
        public long TurnsWithVibrationBonusAllFights;

        // --- Scrap economy ---
        public List<int> ScrapAtRunEnd = new List<int>(); // leftover Scrap whenever a run ends, win or lose

        // --- Core Die evolution reached ---
        public Dictionary<string, int> CoreGradeAtCampaignEnd = new Dictionary<string, int>();

        // --- How far a run gets before dying ---
        public List<int> NodesResolvedBeforeDeath = new List<int>();

        public void RecordDicePurchase(string itemName)
        {
            ShopDicePurchases.TryGetValue(itemName, out int c);
            ShopDicePurchases[itemName] = c + 1;
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
            sb.AppendLine($"--- Frequenza turni con almeno un dado in bonus di Vibrazione (tutti i combattimenti, vinti o persi): {(TotalTurnsAllFights > 0 ? (float)TurnsWithVibrationBonusAllFights / TotalTurnsAllFights : 0):P2} ({TurnsWithVibrationBonusAllFights}/{TotalTurnsAllFights} turni) ---");

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
            sb.AppendLine("--- Dadi comprati allo Shop (Tipo+Taglia: volte) ---");
            foreach (var kvp in ShopDicePurchases.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            sb.AppendLine();
            sb.AppendLine($"--- Aumenti PV comprati: {ShopMaxHpPurchases} | Reroll Shop pagati: {ShopRerolls} | Fusioni alla Furnace: {FurnaceFusions} ---");

            sb.AppendLine();
            sb.AppendLine("--- Dadi salvati alla schermata Meta ---");
            foreach (var kvp in MetaUnlockPurchases.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            sb.AppendLine();
            sb.AppendLine("--- Test di Archivio (win rate) ---");
            sb.AppendLine($"  Risonanza: {(ArchiveResonanceTotal > 0 ? (float)ArchiveResonanceWins / ArchiveResonanceTotal : 0):P1} ({ArchiveResonanceWins}/{ArchiveResonanceTotal})");
            sb.AppendLine($"  Tributo:   {(ArchiveTributeTotal > 0 ? (float)ArchiveTributeWins / ArchiveTributeTotal : 0):P1} ({ArchiveTributeWins}/{ArchiveTributeTotal})");
            sb.AppendLine($"  Ambizione: {(ArchiveAmbitionTotal > 0 ? (float)ArchiveAmbitionWins / ArchiveAmbitionTotal : 0):P1} ({ArchiveAmbitionWins}/{ArchiveAmbitionTotal})");

            return sb.ToString();
        }
    }
}
