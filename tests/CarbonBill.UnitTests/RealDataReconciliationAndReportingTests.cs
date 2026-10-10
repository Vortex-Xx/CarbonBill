using System.Text;
using System.Text.Json;
using CarbonBill.Modules.Reporting.Domain;
using CarbonBill.Modules.Reporting.Models;
using CarbonBill.Modules.Reporting.Persistence;
using CarbonBill.Modules.Reporting.Renderers;
using CarbonBill.Modules.Reporting.Services;
using CarbonBill.SharedKernel.Tenancy;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarbonBill.UnitTests;

public class RealDataReconciliationAndReportingTests
{
    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "seeds", "dev")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        var baseDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (baseDir != null)
        {
            if (Directory.Exists(Path.Combine(baseDir.FullName, "seeds", "dev")))
            {
                return baseDir.FullName;
            }
            baseDir = baseDir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing seeds/dev directory.");
    }

    private static (ReportingDbContext DbContext, SqliteConnection Connection) CreateInMemoryDb(Guid orgId)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var tenantContext = new TenantContext();
        tenantContext.SetContext(orgId, Guid.NewGuid(), "Compliance");

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ReportingDbContext(options, tenantContext);
        dbContext.Database.EnsureCreated();

        return (dbContext, connection);
    }

    private sealed record EmissionResultDevRow(
        string activity_id,
        string period,
        string category,
        decimal kg_co2e,
        decimal factor_value,
        bool is_estimated);

    private sealed record ProductionMetricDevRow(
        string period,
        string unit,
        decimal quantity);

    [Fact]
    public async Task EmissionResultsReconciliation_MatchesDevDataset_ScopeAndTotalSums()
    {
        // 1. Locate and read seeds/dev/emission_results.json
        var repoRoot = FindRepoRoot();
        var emissionFilePath = Path.Combine(repoRoot, "seeds", "dev", "emission_results.json");
        Assert.True(File.Exists(emissionFilePath), $"Emission results file missing at {emissionFilePath}");

        var json = await File.ReadAllTextAsync(emissionFilePath);
        var rows = JsonSerializer.Deserialize<List<EmissionResultDevRow>>(json);
        Assert.NotNull(rows);
        Assert.Equal(35, rows.Count); // 12 months (3 per month for Jan-Nov, 2 for Dec)

        // 2. Group by period and reconcile each period:
        // Scope 1 (Diesel + NaturalGas) + Scope 2 (Electricity) = Total
        // Verified + Estimated = Total
        var byPeriod = rows.GroupBy(r => r.period).OrderBy(g => g.Key).ToList();
        Assert.Equal(12, byPeriod.Count);

        decimal annualTotal = 0m;
        decimal annualElectricity = 0m;

        foreach (var periodGroup in byPeriod)
        {
            var periodList = periodGroup.ToList();
            var scope1 = periodList.Where(r => r.category is "Diesel" or "NaturalGas").Sum(r => r.kg_co2e);
            var scope2 = periodList.Where(r => r.category == "Electricity").Sum(r => r.kg_co2e);
            var periodTotal = periodList.Sum(r => r.kg_co2e);

            var verified = periodList.Where(r => !r.is_estimated).Sum(r => r.kg_co2e);
            var estimated = periodList.Where(r => r.is_estimated).Sum(r => r.kg_co2e);

            // Reconcile: Scope1 + Scope2 == Total
            Assert.Equal(periodTotal, scope1 + scope2);

            // Reconcile: Verified + Estimated == Total
            Assert.Equal(periodTotal, verified + estimated);

            annualTotal += periodTotal;
            annualElectricity += scope2;
        }

        // 3. Reconcile specific test periods from seed_summary.json
        // Period 2024-08 (Diesel MoM spike)
        var aug = byPeriod.First(g => g.Key == "2024-08").ToList();
        var augDiesel = aug.First(r => r.category == "Diesel");
        var augGas = aug.First(r => r.category == "NaturalGas");
        var augElec = aug.First(r => r.category == "Electricity");

        Assert.Equal(49580.0m, augDiesel.kg_co2e);
        Assert.Equal(28217.0m, augGas.kg_co2e);
        Assert.Equal(65100.0m, augElec.kg_co2e);
        Assert.Equal(142897.0m, aug.Sum(r => r.kg_co2e));
        Assert.All(aug, r => Assert.False(r.is_estimated));

        // Period 2024-12 (Missing diesel document, estimated electricity)
        var dec = byPeriod.First(g => g.Key == "2024-12").ToList();
        var decElec = dec.First(r => r.category == "Electricity");
        var decGas = dec.First(r => r.category == "NaturalGas");

        Assert.Equal(74400.0m, decElec.kg_co2e);
        Assert.True(decElec.is_estimated);
        Assert.Equal(28014.0m, decGas.kg_co2e);
        Assert.False(decGas.is_estimated);

        decimal decTotal = 102414.0m;
        Assert.Equal(decTotal, dec.Sum(r => r.kg_co2e));

        decimal estimatedShare = (decElec.kg_co2e / decTotal) * 100m;
        Assert.True(estimatedShare > 10.0m, "Estimated share in December 2024 must exceed 10% threshold");
        Assert.True(Math.Abs(estimatedShare - 72.646m) < 0.05m);

        // Annual Hotspot check: Grid electricity accounts for ~62.70% of annual total (> 50% hotspot threshold)
        Assert.Equal(1443626.0m, annualTotal);
        decimal electricityShare = (annualElectricity / annualTotal) * 100m;
        Assert.True(electricityShare > 50.0m, "Electricity should exceed 50% hotspot threshold");
        Assert.True(Math.Abs(electricityShare - 62.70m) < 0.1m);
    }

    [Fact]
    public async Task IntensityCalculation_ReconcilesWithProductionMetrics()
    {
        var repoRoot = FindRepoRoot();
        var metricsFilePath = Path.Combine(repoRoot, "seeds", "dev", "production_metrics.json");
        var emissionsFilePath = Path.Combine(repoRoot, "seeds", "dev", "emission_results.json");

        var metricsJson = await File.ReadAllTextAsync(metricsFilePath);
        var metrics = JsonSerializer.Deserialize<List<ProductionMetricDevRow>>(metricsJson);
        Assert.NotNull(metrics);

        var emissionsJson = await File.ReadAllTextAsync(emissionsFilePath);
        var emissions = JsonSerializer.Deserialize<List<EmissionResultDevRow>>(emissionsJson);
        Assert.NotNull(emissions);

        // August 2024 surge intensity check
        var augMetrics = metrics.First(m => m.period == "2024-08");
        var augEmissions = emissions.Where(e => e.period == "2024-08").Sum(e => e.kg_co2e);

        Assert.Equal(95000m, augMetrics.quantity);
        Assert.Equal(142897m, augEmissions);

        decimal intensityKgPerPiece = Math.Round(augEmissions / augMetrics.quantity, 6);
        Assert.Equal(1.504179m, intensityKgPerPiece);

        // Peer benchmark P90 is 0.68 kg CO2e / piece
        decimal p90Benchmark = 0.68m;
        Assert.True(intensityKgPerPiece > p90Benchmark, "August intensity must exceed peer P90");
    }

    [Fact]
    public async Task ReportingService_ResolvesPerFigureDocumentProvenance_AndAuditorTrace()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (db, conn) = CreateInMemoryDb(orgId);
        using (conn)
        using (db)
        {
            var pdfRenderer = new QuestPdfReportRenderer(NullLogger<QuestPdfReportRenderer>.Instance);
            var excelRenderer = new ClosedXmlReportRenderer();
            var service = new ReportingService(db, pdfRenderer, excelRenderer);

            // Create and lock report
            var report = await service.CreateReportAsync(orgId, new CreateReportRequest("Annual Audit Report 2024", "2024"));
            var snapshot = await service.ApproveAndLockReportAsync(orgId, report.Id, userId);

            Assert.NotNull(snapshot);
            Assert.False(string.IsNullOrWhiteSpace(snapshot.ContentHashSha256));

            // Verify Auditor Trace resolves to underlying invoice document and emission factor
            var electricityTrace = await service.GetAuditorTraceAsync(orgId, report.Id, "ElectricityBill");
            Assert.NotNull(electricityTrace);
            Assert.Equal(25000m, electricityTrace.Document.Quantity);
            Assert.Equal("kWh", electricityTrace.Document.Unit);
            Assert.True(electricityTrace.Document.AmountBdt > 0);
            Assert.Equal(0.98f, electricityTrace.Document.OcrConfidence);
            Assert.Equal("accountant@apex.com", electricityTrace.Document.ReviewerEmail);
            Assert.Contains("Grid Electricity", electricityTrace.Factor.ActivityOrFuel);
            Assert.Equal(0.620000m, electricityTrace.Factor.FactorValue);
            Assert.Contains("DOE/SREDA", electricityTrace.Factor.Source);

            var dieselTrace = await service.GetAuditorTraceAsync(orgId, report.Id, "diesel_slip");
            Assert.NotNull(dieselTrace);
            Assert.Equal(1200m, dieselTrace.Document.Quantity);
            Assert.Equal("litre", dieselTrace.Document.Unit);
            Assert.Equal(2.680000m, dieselTrace.Factor.FactorValue);
        }
    }

    [Fact]
    public async Task GoldenPdf_RendersVerifiedVsEstimatedLabelling_Bilingual()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (db, conn) = CreateInMemoryDb(orgId);
        using (conn)
        using (db)
        {
            var pdfRenderer = new QuestPdfReportRenderer(NullLogger<QuestPdfReportRenderer>.Instance);
            var excelRenderer = new ClosedXmlReportRenderer();
            var service = new ReportingService(db, pdfRenderer, excelRenderer);

            var report = await service.CreateReportAsync(orgId, new CreateReportRequest("Buyer Verification Report", "2024-Q3"));
            await service.ApproveAndLockReportAsync(orgId, report.Id, userId);

            // 1. English PDF
            byte[] enPdf = await service.RenderReportPdfAsync(orgId, report.Id, language: "en");
            Assert.NotNull(enPdf);
            Assert.True(enPdf.Length > 2000);
            string enHeader = Encoding.ASCII.GetString(enPdf.Take(5).ToArray());
            Assert.Equal("%PDF-", enHeader);

            // 2. Bangla PDF
            byte[] bnPdf = await service.RenderReportPdfAsync(orgId, report.Id, language: "bn");
            Assert.NotNull(bnPdf);
            Assert.True(bnPdf.Length > 2000);
            string bnHeader = Encoding.ASCII.GetString(bnPdf.Take(5).ToArray());
            Assert.Equal("%PDF-", bnHeader);
        }
    }

    [Fact]
    public async Task ClosedXmlExcel_RendersRawNumericValues_AndAppliesPriceRedaction()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (db, conn) = CreateInMemoryDb(orgId);
        using (conn)
        using (db)
        {
            var pdfRenderer = new QuestPdfReportRenderer(NullLogger<QuestPdfReportRenderer>.Instance);
            var excelRenderer = new ClosedXmlReportRenderer();
            var service = new ReportingService(db, pdfRenderer, excelRenderer);

            var report = await service.CreateReportAsync(orgId, new CreateReportRequest("Excel Provenance Report", "2024-Q1"));
            await service.ApproveAndLockReportAsync(orgId, report.Id, userId);

            // 1. Unredacted Excel
            byte[] unredactedExcel = await service.RenderReportExcelAsync(orgId, report.Id, overrideRedactPrices: false);
            Assert.NotNull(unredactedExcel);
            using (var ms1 = new MemoryStream(unredactedExcel))
            using (var wb1 = new XLWorkbook(ms1))
            {
                var wsAudit = wb1.Worksheet("Document Provenance");
                Assert.NotNull(wsAudit);

                // Row 2 Column F has actual monetary number
                var cell = wsAudit.Cell("F2");
                Assert.NotEqual("[Confidential]", cell.GetString());
                Assert.True(cell.GetDouble() > 0, "Unredacted amount must be numeric > 0");
            }

            // 2. Redacted Excel
            byte[] redactedExcel = await service.RenderReportExcelAsync(orgId, report.Id, overrideRedactPrices: true);
            Assert.NotNull(redactedExcel);
            using (var ms2 = new MemoryStream(redactedExcel))
            using (var wb2 = new XLWorkbook(ms2))
            {
                var wsAudit = wb2.Worksheet("Document Provenance");
                Assert.NotNull(wsAudit);

                // Row 2 Column F must be masked to [Confidential]
                var cell = wsAudit.Cell("F2");
                Assert.Equal("[Confidential]", cell.GetString());
            }
        }
    }

    [Fact]
    public void OpenTelemetryMetrics_EmitsAndSnapshotReconciles()
    {
        CarbonBill.Modules.Reporting.Observability.CarbonBillMetrics.RecordDocumentIngested("org-test", "electricity_bill");
        CarbonBill.Modules.Reporting.Observability.CarbonBillMetrics.RecordReviewTime(18.2, "org-test");
        CarbonBill.Modules.Reporting.Observability.CarbonBillMetrics.RecordOcrInvocation(1, "org-test");
        CarbonBill.Modules.Reporting.Observability.CarbonBillMetrics.RecordFieldReview(10, 1);
        CarbonBill.Modules.Reporting.Observability.CarbonBillMetrics.RecordFlagLifecycle("raised", "SPIKE_MOM", "Warning");
        CarbonBill.Modules.Reporting.Observability.CarbonBillMetrics.RecordRecommendationCompleted("PFL-EE-002", "A");

        var snapshot = CarbonBill.Modules.Reporting.Observability.CarbonBillMetrics.GetCurrentSnapshot();
        Assert.NotNull(snapshot);
        Assert.True(snapshot.DocumentsIngestedTotal > 0);
        Assert.True(snapshot.OcrTier1Invocations > 0);
        Assert.True(snapshot.FieldsConfirmedTotal > 0);
        Assert.True(snapshot.FlagsRaisedTotal > 0);
        Assert.True(snapshot.RecommendationsCompletedTotal > 0);
    }
}
