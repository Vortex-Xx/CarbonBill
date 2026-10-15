using System.Globalization;
using System.Text.Json;
using CarbonBill.Modules.Reporting.Models;
using CarbonBill.SharedKernel.Providers;
using ClosedXML.Excel;

namespace CarbonBill.Modules.Reporting.Renderers;

public class ClosedXmlReportRenderer : IReportRenderer
{
    public string OutputFormat => "xlsx";

    public Task<byte[]> RenderAsync(ReportRenderRequest request, CancellationToken cancellationToken = default)
    {
        FrozenReportPayload payload;
        if (request.ModelData is FrozenReportPayload p)
        {
            payload = p;
        }
        else if (request.ModelData is string json)
        {
            payload = JsonSerializer.Deserialize<FrozenReportPayload>(json)
                ?? throw new InvalidOperationException("Invalid FrozenReportPayload JSON.");
        }
        else
        {
            var serialized = JsonSerializer.Serialize(request.ModelData);
            payload = JsonSerializer.Deserialize<FrozenReportPayload>(serialized)
                ?? throw new InvalidOperationException("Could not deserialize ModelData to FrozenReportPayload.");
        }

        using var workbook = new XLWorkbook();

        // 1. Sheet: Executive Summary & Metrics
        var wsSummary = workbook.Worksheets.Add("Summary");
        wsSummary.Cell("A1").Value = "CarbonBill GHG Corporate Inventory";
        wsSummary.Cell("A1").Style.Font.Bold = true;
        wsSummary.Cell("A1").Style.Font.FontSize = 14;

        wsSummary.Cell("A2").Value = $"Organization: {payload.OrgName}";
        wsSummary.Cell("A3").Value = $"Site: {payload.SiteName} ({payload.Location})";
        wsSummary.Cell("A4").Value = $"Reporting Period: {payload.ReportingPeriod}";
        wsSummary.Cell("A5").Value = $"Boundary: {payload.BoundaryDescription}";
        wsSummary.Cell("A6").Value = $"Data Quality Score: {payload.DataQualityScore:F1}%";

        wsSummary.Cell("A8").Value = "Scope";
        wsSummary.Cell("B8").Value = "Emissions (tCO2e)";
        wsSummary.Row(8).Style.Font.Bold = true;

        wsSummary.Cell("A9").Value = "Scope 1 (Direct Fuel & Combustion)";
        wsSummary.Cell("B9").Value = (double)payload.Scope1Tco2e;
        wsSummary.Cell("B9").Style.NumberFormat.Format = "#,##0.000";

        wsSummary.Cell("A10").Value = "Scope 2 (Purchased Grid Electricity)";
        wsSummary.Cell("B10").Value = (double)payload.Scope2Tco2e;
        wsSummary.Cell("B10").Style.NumberFormat.Format = "#,##0.000";

        wsSummary.Cell("A11").Value = "Scope 3 (Upstream Transport)";
        wsSummary.Cell("B11").Value = (double)payload.Scope3Tco2e;
        wsSummary.Cell("B11").Style.NumberFormat.Format = "#,##0.000";

        wsSummary.Cell("A12").Value = "Total GHG Inventory";
        wsSummary.Cell("B12").Value = (double)payload.TotalTco2e;
        wsSummary.Row(12).Style.Font.Bold = true;
        wsSummary.Cell("B12").Style.NumberFormat.Format = "#,##0.000";

        wsSummary.Cell("A14").Value = "Production Volume";
        wsSummary.Cell("B14").Value = (double)payload.ProductionVolume;
        wsSummary.Cell("C14").Value = payload.ProductionUnit;

        wsSummary.Cell("A15").Value = "Emissions Intensity";
        wsSummary.Cell("B15").Value = (double)payload.IntensityKgCo2ePerUnit;
        wsSummary.Cell("C15").Value = $"kg CO2e / {payload.ProductionUnit}";

        wsSummary.Cell("A17").Value = "Methodology Disclaimer:";
        wsSummary.Cell("A18").Value = payload.MethodologyStatement;
        wsSummary.Cell("A19").Value = payload.LimitationsStatement;
        wsSummary.Columns().AdjustToContents();

        // 2. Sheet: Monthly Activities & Emissions
        var wsMonthly = workbook.Worksheets.Add("Monthly Emissions");
        wsMonthly.Cell("A1").Value = "Period";
        wsMonthly.Cell("B1").Value = "Grid (kWh)";
        wsMonthly.Cell("C1").Value = "Diesel (L)";
        wsMonthly.Cell("D1").Value = "Gas (m3)";
        wsMonthly.Cell("E1").Value = "Scope 1 (kg CO2e)";
        wsMonthly.Cell("F1").Value = "Scope 2 (kg CO2e)";
        wsMonthly.Cell("G1").Value = "Scope 3 (kg CO2e)";
        wsMonthly.Cell("H1").Value = "Total (tCO2e)";
        wsMonthly.Cell("I1").Value = "Is Estimated";
        wsMonthly.Row(1).Style.Font.Bold = true;

        int rowIdx = 2;
        foreach (var m in payload.MonthlyActivities)
        {
            wsMonthly.Cell(rowIdx, 1).Value = m.Period;
            wsMonthly.Cell(rowIdx, 2).Value = (double)m.ElectricityKwh;
            wsMonthly.Cell(rowIdx, 3).Value = (double)m.DieselLitres;
            wsMonthly.Cell(rowIdx, 4).Value = (double)m.GasM3;
            wsMonthly.Cell(rowIdx, 5).Value = (double)m.Scope1KgCo2e;
            wsMonthly.Cell(rowIdx, 6).Value = (double)m.Scope2KgCo2e;
            wsMonthly.Cell(rowIdx, 7).Value = (double)m.Scope3KgCo2e;
            wsMonthly.Cell(rowIdx, 8).Value = (double)Math.Round(m.TotalKgCo2e / 1000m, 3);
            wsMonthly.Cell(rowIdx, 9).Value = m.IsEstimated ? "Yes" : "No";

            wsMonthly.Cell(rowIdx, 2).Style.NumberFormat.Format = "#,##0.00";
            wsMonthly.Cell(rowIdx, 3).Style.NumberFormat.Format = "#,##0.00";
            wsMonthly.Cell(rowIdx, 4).Style.NumberFormat.Format = "#,##0.00";
            wsMonthly.Cell(rowIdx, 8).Style.NumberFormat.Format = "#,##0.000";
            rowIdx++;
        }
        wsMonthly.Columns().AdjustToContents();

        // 3. Sheet: Applied Emission Factors
        var wsFactors = workbook.Worksheets.Add("Factor Registry");
        wsFactors.Cell("A1").Value = "Activity / Fuel";
        wsFactors.Cell("B1").Value = "Factor Value";
        wsFactors.Cell("C1").Value = "Unit";
        wsFactors.Cell("D1").Value = "Source Citation";
        wsFactors.Cell("E1").Value = "Publication Year";
        wsFactors.Cell("F1").Value = "GWP Basis";
        wsFactors.Row(1).Style.Font.Bold = true;

        int fRow = 2;
        foreach (var f in payload.AppliedFactors)
        {
            wsFactors.Cell(fRow, 1).Value = f.ActivityOrFuel;
            wsFactors.Cell(fRow, 2).Value = (double)f.FactorValue;
            wsFactors.Cell(fRow, 2).Style.NumberFormat.Format = "0.000000";
            wsFactors.Cell(fRow, 3).Value = f.Unit;
            wsFactors.Cell(fRow, 4).Value = f.Source;
            wsFactors.Cell(fRow, 5).Value = f.PublicationYear;
            wsFactors.Cell(fRow, 6).Value = f.GwpBasis;
            fRow++;
        }
        wsFactors.Columns().AdjustToContents();

        // 4. Sheet: Document Audit Trail & Provenance
        var wsAudit = workbook.Worksheets.Add("Document Provenance");
        wsAudit.Cell("A1").Value = "Document ID";
        wsAudit.Cell("B1").Value = "Document Type";
        wsAudit.Cell("C1").Value = "Period";
        wsAudit.Cell("D1").Value = "Quantity";
        wsAudit.Cell("E1").Value = "Unit";
        wsAudit.Cell("F1").Value = "Amount (BDT)";
        wsAudit.Cell("G1").Value = "OCR Confidence";
        wsAudit.Cell("H1").Value = "Reviewer";
        wsAudit.Cell("I1").Value = "Confirmation Timestamp";
        wsAudit.Row(1).Style.Font.Bold = true;

        int aRow = 2;
        foreach (var d in payload.DocumentAuditTrail)
        {
            wsAudit.Cell(aRow, 1).Value = d.DocumentId.ToString();
            wsAudit.Cell(aRow, 2).Value = d.DocumentType;
            wsAudit.Cell(aRow, 3).Value = d.Period;
            wsAudit.Cell(aRow, 4).Value = (double)d.Quantity;
            wsAudit.Cell(aRow, 5).Value = d.Unit;

            if (payload.RedactPrices)
            {
                wsAudit.Cell(aRow, 6).Value = "[Confidential]";
            }
            else
            {
                if (d.AmountBdt.HasValue)
                {
                    wsAudit.Cell(aRow, 6).Value = (double)d.AmountBdt.Value;
                    wsAudit.Cell(aRow, 6).Style.NumberFormat.Format = "#,##0.00";
                }
                else
                {
                    wsAudit.Cell(aRow, 6).Value = "-";
                }
            }

            wsAudit.Cell(aRow, 7).Value = (double)d.OcrConfidence;
            wsAudit.Cell(aRow, 7).Style.NumberFormat.Format = "0.0%";
            wsAudit.Cell(aRow, 8).Value = d.ReviewerEmail;
            wsAudit.Cell(aRow, 9).Value = d.ConfirmedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            aRow++;
        }
        wsAudit.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return Task.FromResult(memoryStream.ToArray());
    }
}
