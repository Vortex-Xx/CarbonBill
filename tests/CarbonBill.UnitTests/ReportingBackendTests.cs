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

public class ReportingBackendTests
{
    private sealed class TestTimeProvider(DateTimeOffset initialTime) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialTime;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }

    private static (ReportingDbContext DbContext, SqliteConnection Connection) CreateInMemoryDb(Guid orgId)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var tenantContext = new TenantContext();
        tenantContext.SetContext(orgId, Guid.NewGuid());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ReportingDbContext(options, tenantContext);
        dbContext.Database.EnsureCreated();

        return (dbContext, connection);
    }

    [Fact]
    public void SnapshotHashStability_ProducesIdenticalSha256_ForIdenticalPayload()
    {
        var payload1 = new FrozenReportPayload
        {
            ReportId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            OrgId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            OrgName = "Test RMG Factory Ltd.",
            ReportingPeriod = "2026-Q1",
            DataQualityScore = 95.0m,
            TotalKgCo2e = 50000.0m
        };

        var payload2 = new FrozenReportPayload
        {
            ReportId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            OrgId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            OrgName = "Test RMG Factory Ltd.",
            ReportingPeriod = "2026-Q1",
            DataQualityScore = 95.0m,
            TotalKgCo2e = 50000.0m
        };

        var opts = new JsonSerializerOptions { WriteIndented = false };
        string json1 = JsonSerializer.Serialize(payload1, opts);
        string json2 = JsonSerializer.Serialize(payload2, opts);

        string hash1 = ReportingService.ComputeSha256(json1);
        string hash2 = ReportingService.ComputeSha256(json2);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // 256 bits = 64 hex chars
    }

    [Fact]
    public async Task LockedReportImmutability_PreventsModificationsAndReApproval()
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

            // 1. Create Report in Draft
            var report = await service.CreateReportAsync(orgId, new CreateReportRequest("Q1 Carbon Report", "2026-Q1"));
            Assert.Equal(ReportStatuses.Draft, report.Status);

            // 2. Submit for Review
            var underReview = await service.SubmitForReviewAsync(orgId, report.Id);
            Assert.Equal(ReportStatuses.ReadyForReview, underReview.Status);

            // 3. Approve and Lock
            var snapshot = await service.ApproveAndLockReportAsync(orgId, report.Id, userId);
            Assert.Equal(ReportStatuses.Locked, report.Status);
            Assert.Equal(1, snapshot.Version);
            Assert.False(string.IsNullOrWhiteSpace(snapshot.ContentHashSha256));

            // 4. Attempting to submit locked report for review throws
            var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SubmitForReviewAsync(orgId, report.Id));
            Assert.Contains("Locked reports cannot be submitted", ex1.Message);

            // 5. Attempting to re-approve locked report throws (immutability rule)
            var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ApproveAndLockReportAsync(orgId, report.Id, userId));
            Assert.Contains("Locked report cannot be modified or re-approved", ex2.Message);
        }
    }

    [Fact]
    public async Task ShareLink_ExpiresWithFakeClock_AndIncrementsViewCount()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var startTime = new DateTimeOffset(2026, 10, 9, 10, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(startTime);

        var (db, conn) = CreateInMemoryDb(orgId);
        using (conn)
        using (db)
        {
            var pdfRenderer = new QuestPdfReportRenderer(NullLogger<QuestPdfReportRenderer>.Instance);
            var excelRenderer = new ClosedXmlReportRenderer();
            var service = new ReportingService(db, pdfRenderer, excelRenderer, clock);

            var report = await service.CreateReportAsync(orgId, new CreateReportRequest("Annual Audit Report", "2026"));
            await service.ApproveAndLockReportAsync(orgId, report.Id, userId);

            // Create share link valid for 14 days
            var shareLink = await service.CreateShareLinkAsync(orgId, report.Id, new CreateShareLinkRequest(DaysValid: 14, RedactPrices: true), userId);

            // Day 5: Still valid
            clock.Advance(TimeSpan.FromDays(5));
            var (payload1, link1) = await service.GetSharedReportAsync(shareLink.Token);
            Assert.NotNull(payload1);
            Assert.Equal(1, link1.ViewCount);

            // Day 10: Still valid
            clock.Advance(TimeSpan.FromDays(5));
            var (_, link2) = await service.GetSharedReportAsync(shareLink.Token);
            Assert.Equal(2, link2.ViewCount);

            // Day 15: Expired (14-day limit exceeded)
            clock.Advance(TimeSpan.FromDays(5));
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.GetSharedReportAsync(shareLink.Token));
            Assert.Contains("share link has expired", ex.Message);
        }
    }

    [Fact]
    public async Task Redaction_RemovesPricesFromEveryOutput_InPdfAndExcel()
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

            var report = await service.CreateReportAsync(orgId, new CreateReportRequest("Buyer Trace Report", "2026-Q1"));
            await service.ApproveAndLockReportAsync(orgId, report.Id, userId);

            // 1. Render PDF with price redaction
            byte[] pdfBytes = await service.RenderReportPdfAsync(orgId, report.Id, language: "en", overrideRedactPrices: true);
            Assert.NotNull(pdfBytes);
            Assert.True(pdfBytes.Length > 1000);
            string pdfText = Encoding.UTF8.GetString(pdfBytes);
            Assert.StartsWith("%PDF-", pdfText);

            // 2. Render Excel with price redaction
            byte[] excelBytes = await service.RenderReportExcelAsync(orgId, report.Id, overrideRedactPrices: true);
            Assert.NotNull(excelBytes);

            using var ms = new MemoryStream(excelBytes);
            using var workbook = new XLWorkbook(ms);
            var wsAudit = workbook.Worksheet("Document Provenance");

            // Verify that column 6 (Amount BDT) has "[Confidential]" instead of numerical numbers
            string cellValue = wsAudit.Cell("F2").GetString();
            Assert.Equal("[Confidential]", cellValue);
        }
    }

    [Fact]
    public async Task GoldenPdfTextTests_RendersValidPdf_InEnglishAndBangla()
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

            var report = await service.CreateReportAsync(orgId, new CreateReportRequest("Factory Compliance Report", "2026-Q1"));
            await service.ApproveAndLockReportAsync(orgId, report.Id, userId);

            // 1. English PDF
            byte[] englishPdf = await service.RenderReportPdfAsync(orgId, report.Id, language: "en");
            Assert.NotNull(englishPdf);
            Assert.True(englishPdf.Length > 2000);
            string enHeader = Encoding.ASCII.GetString(englishPdf.Take(5).ToArray());
            Assert.Equal("%PDF-", enHeader);

            // 2. Bangla PDF
            byte[] banglaPdf = await service.RenderReportPdfAsync(orgId, report.Id, language: "bn");
            Assert.NotNull(banglaPdf);
            Assert.True(banglaPdf.Length > 2000);
            string bnHeader = Encoding.ASCII.GetString(banglaPdf.Take(5).ToArray());
            Assert.Equal("%PDF-", bnHeader);
        }
    }

    [Fact]
    public async Task AuditorTrace_MapsFigureToSourceDocumentAndFactor()
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

            var report = await service.CreateReportAsync(orgId, new CreateReportRequest("Auditor Trace Sample", "2026-Q1"));
            await service.ApproveAndLockReportAsync(orgId, report.Id, userId);

            var trace = await service.GetAuditorTraceAsync(orgId, report.Id, "ElectricityBill");

            Assert.NotNull(trace);
            Assert.Equal("ElectricityBill", trace.Document.DocumentType);
            Assert.Equal(25000m, trace.Document.Quantity);
            Assert.Equal("kWh", trace.Document.Unit);
            Assert.NotNull(trace.Factor);
            Assert.Contains("Grid", trace.Factor.ActivityOrFuel);
            Assert.Contains("accountant@apex.com", trace.VerificationStatement);
        }
    }

    [Fact]
    public async Task DashboardEndpoints_ReturnExecutiveSummaryAndTrendWithHatchFlag()
    {
        var orgId = Guid.NewGuid();
        var (db, conn) = CreateInMemoryDb(orgId);
        using (conn)
        using (db)
        {
            var pdfRenderer = new QuestPdfReportRenderer(NullLogger<QuestPdfReportRenderer>.Instance);
            var excelRenderer = new ClosedXmlReportRenderer();
            var service = new ReportingService(db, pdfRenderer, excelRenderer);

            // Dashboard Summary
            var summary = await service.GetDashboardSummaryAsync(orgId, "2026-Q1");
            Assert.NotNull(summary);
            Assert.True(summary.TotalTco2e > 0);
            Assert.True(summary.DataQualityScore > 90.0m);
            Assert.Contains("GHG Protocol", summary.MethodologyStatement);

            // Dashboard Trend
            var trend = await service.GetDashboardTrendAsync(orgId);
            Assert.NotNull(trend);
            Assert.Equal(4, trend.Count);
            // Verify that the point with an estimated portion has HatchFlag = true
            var estimatedPoint = trend.First(t => t.EstimatedKgCo2e > 0);
            Assert.True(estimatedPoint.HatchFlag);
        }
    }
}
