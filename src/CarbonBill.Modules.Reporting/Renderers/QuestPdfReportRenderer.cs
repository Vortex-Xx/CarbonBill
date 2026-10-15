using System.Globalization;
using System.Text.Json;
using CarbonBill.Modules.Reporting.Models;
using CarbonBill.SharedKernel.Providers;
using Microsoft.Extensions.Logging;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CarbonBill.Modules.Reporting.Renderers;

public class QuestPdfReportRenderer : IReportRenderer
{
    private readonly ILogger<QuestPdfReportRenderer> _logger;
    private static bool _fontsRegistered;
    private static readonly object FontLock = new();

    public string OutputFormat => "pdf";

    public QuestPdfReportRenderer(ILogger<QuestPdfReportRenderer> logger)
    {
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
        EnsureBengaliFontsRegistered();
    }

    private void EnsureBengaliFontsRegistered()
    {
        if (_fontsRegistered) return;

        lock (FontLock)
        {
            if (_fontsRegistered) return;

            try
            {
                var candidatePaths = new[]
                {
                    "/usr/share/fonts/truetype/noto/NotoSansBengali-Regular.ttf",
                    "/usr/share/fonts/truetype/noto/NotoSansBengali-Bold.ttf",
                    "/usr/share/fonts/truetype/noto/NotoSansBengali-Medium.ttf"
                };

                foreach (var path in candidatePaths)
                {
                    if (File.Exists(path))
                    {
                        using var stream = File.OpenRead(path);
                        FontManager.RegisterFont(stream);
                    }
                }

                _fontsRegistered = true;
                _logger.LogInformation("QuestPDF registered system Noto Sans Bengali fonts successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not register system Noto Sans Bengali font; falling back to default.");
            }
        }
    }

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

        bool isBn = request.Language.Equals("bn", StringComparison.OrdinalIgnoreCase);
        string fontFamily = isBn ? "Noto Sans Bengali" : Fonts.Arial;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(35);
                page.DefaultTextStyle(x => x.FontFamily(fontFamily).FontSize(10).FontColor(Colors.Grey.Darken3));

                // Header
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(isBn ? "কার্বনবিল বাংলাদেশ" : "CarbonBill Bangladesh")
                           .Bold().FontSize(15).FontColor(Colors.Green.Darken2);
                        col.Item().Text(isBn ? "গ্রিনহাউস গ্যাস অ্যাকাউন্টিং ইনভেন্টরি" : "GHG Protocol Corporate Accounting Inventory")
                           .FontSize(9).FontColor(Colors.Grey.Medium);
                    });

                    row.ConstantItem(120).AlignRight().Column(col =>
                    {
                        col.Item().Text($"{payload.ReportingPeriod}").Bold().FontSize(12);
                        col.Item().Text(isBn ? "অনুমোদিত ও সংরক্ষিত" : "Approved & Locked").FontSize(8).FontColor(Colors.Green.Darken1);
                    });
                });

                // Content
                page.Content().PaddingVertical(15).Column(col =>
                {
                    // 1. Cover & Title Section
                    col.Item().Background(Colors.Grey.Lighten4).Padding(12).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text(payload.OrgName).Bold().FontSize(14).FontColor(Colors.Black);
                            c.Item().Text($"{payload.SiteName} - {payload.Location}").FontSize(10);
                            c.Item().Text($"{ (isBn ? "সাংগঠনিক সীমানা: " : "Boundary: ") }{payload.BoundaryDescription}").FontSize(9).Italic();
                        });

                        r.ConstantItem(110).Column(c =>
                        {
                            c.Item().Border(1).BorderColor(Colors.Green.Darken1).Background(Colors.Green.Lighten5).Padding(6).Column(dqs =>
                            {
                                dqs.Item().Text(isBn ? "ডিটা কোয়ালিটি স্কোর" : "Data Quality Score").FontSize(7).SemiBold().FontColor(Colors.Green.Darken3);
                                string dqsStr = isBn ? ToBn(payload.DataQualityScore.ToString("F1", CultureInfo.InvariantCulture)) + "%" : $"{payload.DataQualityScore:F1}%";
                                dqs.Item().Text(dqsStr).Bold().FontSize(14).FontColor(Colors.Green.Darken2);
                                dqs.Item().Text(isBn ? "যাচাইকৃত চালান ভিত্তিক" : "Verified Invoices").FontSize(7).FontColor(Colors.Green.Darken1);
                            });
                        });
                    });

                    col.Item().Height(12);

                    // 2. Executive Summary & Scope Breakdown
                    col.Item().Text(isBn ? "১. নির্গমন সারসংক্ষেপ (Scopes 1, 2, 3)" : "1. Executive Emissions Summary (Scopes 1, 2, 3)")
                       .Bold().FontSize(12).FontColor(Colors.Grey.Darken4);

                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "স্কোপ ক্যাটাগরি" : "Scope Category").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "নির্গমন (tCO2e)" : "Emissions (tCO2e)").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "যাচাইকৃত %" : "Verified %").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "অনুমিত %" : "Estimated %").Bold();
                        });

                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "স্কোপ ১: সরাসরি নির্গমন (ডিজেল/গ্যাস)" : "Scope 1: Direct (Genset / Gas)");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(FormatNum(payload.Scope1Tco2e, isBn));
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(FormatNum(payload.VerifiedSharePercent, isBn) + "%");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(FormatNum(payload.EstimatedSharePercent, isBn) + "%");

                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "স্কোপ ২: গ্রিড বিদ্যুৎ (লোকেশন-ভিত্তিক)" : "Scope 2: Grid Electricity (Location)");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(FormatNum(payload.Scope2Tco2e, isBn));
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(FormatNum(payload.VerifiedSharePercent, isBn) + "%");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(FormatNum(payload.EstimatedSharePercent, isBn) + "%");

                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "স্কোপ ৩: পরিবহন ও ফ্রেইট" : "Scope 3: Upstream Freight");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(FormatNum(payload.Scope3Tco2e, isBn));
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text("-");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text("-");

                        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(isBn ? "সর্বমোট গ্রিনহাউস গ্যাস নির্গমন" : "Total GHG Inventory").Bold();
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(FormatNum(payload.TotalTco2e, isBn)).Bold();
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(FormatNum(payload.VerifiedSharePercent, isBn) + "%").Bold();
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(FormatNum(payload.EstimatedSharePercent, isBn) + "%").Bold();
                    });

                    col.Item().Height(8);

                    // Intensity Metric Box
                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(6).Row(r =>
                    {
                        r.RelativeItem().Text(
                            $"{ (isBn ? "উৎপাদন তীব্রতা (ইন্টেনসিটি): " : "Emissions Intensity: ") }" +
                            $"{FormatNum(payload.IntensityKgCo2ePerUnit, isBn)} kg CO2e / {payload.ProductionUnit} " +
                            $"({ (isBn ? "মোট উৎপাদন: " : "Volume: ") }{FormatNum(payload.ProductionVolume, isBn)} {payload.ProductionUnit})"
                        ).FontSize(9).Bold();
                    });

                    col.Item().Height(12);

                    // 3. Monthly & Activity Breakdown
                    col.Item().Text(isBn ? "২. মাসিক জ্বালানি ব্যবহার ও নির্গমন বিবরণী" : "2. Monthly Fuel Consumption & Scope Breakdown")
                       .Bold().FontSize(12).FontColor(Colors.Grey.Darken4);

                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "মাস" : "Period").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "বিদ্যুৎ (kWh)" : "Grid (kWh)").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "ডিজেল (লিটার)" : "Diesel (L)").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "গ্যাস (m3)" : "Gas (m3)").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "মোট (tCO2e)" : "Total (tCO2e)").Bold();
                        });

                        foreach (var m in payload.MonthlyActivities)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(m.Period);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(FormatNum(m.ElectricityKwh, isBn));
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(FormatNum(m.DieselLitres, isBn));
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(FormatNum(m.GasM3, isBn));
                            decimal totalT = Math.Round(m.TotalKgCo2e / 1000m, 3);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(FormatNum(totalT, isBn));
                        }
                    });

                    col.Item().Height(12);

                    // 4. Emission Factors Applied
                    col.Item().Text(isBn ? "৩. প্রয়োগকৃত নিঃসরণ গুণক (Emission Factors Registry)" : "3. Applied Emission Factor Registry")
                       .Bold().FontSize(12).FontColor(Colors.Grey.Darken4);

                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "জ্বালানি / কার্যক্রম" : "Activity / Fuel").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "গুণক মান" : "Factor Value").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "উৎস প্রতিষ্ঠান" : "Source Citation").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "বছর" : "Year").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("GWP").Bold();
                        });

                        foreach (var f in payload.AppliedFactors)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(f.ActivityOrFuel);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{f.FactorValue:F6} {f.Unit}");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(f.Source);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(isBn ? ToBn(f.PublicationYear.ToString(CultureInfo.InvariantCulture)) : f.PublicationYear.ToString(CultureInfo.InvariantCulture));
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(f.GwpBasis);
                        }
                    });

                    col.Item().Height(12);

                    // 5. Document Provenance & Audit Trail
                    col.Item().Text(isBn ? "৪. অডিট ট্রেইল ও চালান সূত্র (Document Audit Trail)" : "4. Document Audit Trail & Provenance")
                       .Bold().FontSize(12).FontColor(Colors.Grey.Darken4);

                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "ডকুমেন্ট আইডি" : "Document ID").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "ধরন" : "Doc Type").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "পরিমাণ" : "Quantity").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "টাকার পরিমাণ" : "Amount (BDT)").Bold();
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(isBn ? "অনুমোদনকারী" : "Reviewer").Bold();
                        });

                        foreach (var doc in payload.DocumentAuditTrail)
                        {
                            string docShort = doc.DocumentId.ToString()[..8];
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(docShort);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(doc.DocumentType);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{FormatNum(doc.Quantity, isBn)} {doc.Unit}");
                            
                            // Redaction Check: If RedactPrices is true, mask monetary figures
                            string amtStr = payload.RedactPrices
                                ? (isBn ? "[গোপনীয়]" : "[Confidential]")
                                : (doc.AmountBdt.HasValue ? FormatNum(doc.AmountBdt.Value, isBn) : "-");

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(amtStr);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(doc.ReviewerEmail);
                        }
                    });

                    col.Item().Height(12);

                    // 6. Mandatory GHG Protocol Methodology & Limitations Disclaimer
                    col.Item().Background(Colors.Grey.Lighten4).Padding(8).Column(disc =>
                    {
                        disc.Item().Text(isBn ? "পদ্ধতিগত সীমাবদ্ধতা ও সম্মতি বিবৃতি:" : "Methodology & Scope Boundaries:")
                            .Bold().FontSize(8).FontColor(Colors.Grey.Darken3);
                        disc.Item().Text(payload.MethodologyStatement).FontSize(8).Italic();
                        disc.Item().Text(payload.LimitationsStatement).FontSize(8).FontColor(Colors.Grey.Darken2);
                    });
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text(
                        isBn ? "কার্বনবিল বাংলাদেশ প্ল্যাটফর্ম দ্বারা স্বয়ংক্রিয়ভাবে তৈরি" : "Generated by CarbonBill Enterprise Platform"
                    ).FontSize(8).FontColor(Colors.Grey.Medium);

                    row.ConstantItem(100).AlignRight().Text(x =>
                    {
                        x.Span(isBn ? "পৃষ্ঠা " : "Page ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });
        });

        byte[] pdfBytes = document.GeneratePdf();
        return Task.FromResult(pdfBytes);
    }

    private static string FormatNum(decimal val, bool isBn)
    {
        string formatted = val.ToString("N2", CultureInfo.InvariantCulture);
        return isBn ? ToBn(formatted) : formatted;
    }

    private static string ToBn(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace('0', '০')
            .Replace('1', '১')
            .Replace('2', '২')
            .Replace('3', '৩')
            .Replace('4', '৪')
            .Replace('5', '৫')
            .Replace('6', '৬')
            .Replace('7', '৭')
            .Replace('8', '৮')
            .Replace('9', '৯');
    }
}
