using System.Globalization;
using System.Text;
using CarbonBill.SharedKernel.Events;

namespace CarbonBill.Modules.Notifications.Services;

public class NotificationTemplateRenderer : INotificationTemplateRenderer
{
    public static string ToBanglaDigits(string input)
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

    public string FormatNumber(long number, string language = "en")
    {
        var str = number.ToString(CultureInfo.InvariantCulture);
        return language.Equals("bn", StringComparison.OrdinalIgnoreCase) ? ToBanglaDigits(str) : str;
    }

    public RenderedNotification RenderMissingAlert(MissingAlertRaisedEvent alert, string language = "en")
    {
        var isBangla = language.Equals("bn", StringComparison.OrdinalIgnoreCase);
        var dateFormatted = isBangla
            ? ToBanglaDigits(alert.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            : alert.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        string title;
        string body;

        if (isBangla)
        {
            title = $"অনুপস্থিত নথি সতৰ্কবার্তা: {alert.DocType}";
            body = $"দয়া করে {alert.AssetName}-এর জন্য {alert.DocType} জমা দিন ({alert.Period} মেয়াদের জন্য, শেষ তারিখ {dateFormatted})।";
        }
        else
        {
            title = $"Missing Document: {alert.DocType}";
            body = $"Please upload {alert.DocType} for {alert.AssetName} (Period: {alert.Period}, Due: {dateFormatted}).";
        }

        var url = $"/gaps?period={Uri.EscapeDataString(alert.Period)}";
        return new RenderedNotification(title, body, url);
    }

    public RenderedNotification RenderFlagRaised(FlagRaisedEvent flag, string language = "en")
    {
        var isBangla = language.Equals("bn", StringComparison.OrdinalIgnoreCase);
        string title;
        string body;

        var severityLabel = flag.Severity.ToUpperInvariant() switch
        {
            "RED" => isBangla ? "জরুরি" : "CRITICAL",
            "AMBER" => isBangla ? "সতর্কতা" : "WARNING",
            _ => isBangla ? "তথ্য" : "INFO"
        };

        if (isBangla)
        {
            title = $"[{severityLabel}] কার্বন ফ্ল্যাগ: {flag.RuleCode}";
            var explanation = !string.IsNullOrWhiteSpace(flag.ExplanationBn) ? flag.ExplanationBn : flag.ExplanationEn;
            var action = !string.IsNullOrWhiteSpace(flag.SuggestedActionBn) ? flag.SuggestedActionBn : flag.SuggestedActionEn;
            body = $"{explanation}। প্রস্তাবিত পদক্ষেপ: {action}";
        }
        else
        {
            title = $"[{severityLabel}] Carbon Flag: {flag.RuleCode}";
            var explanation = !string.IsNullOrWhiteSpace(flag.ExplanationEn) ? flag.ExplanationEn : flag.ExplanationBn;
            var action = !string.IsNullOrWhiteSpace(flag.SuggestedActionEn) ? flag.SuggestedActionEn : flag.SuggestedActionBn;
            body = $"{explanation}. Suggested Action: {action}";
        }

        var url = $"/flags?rule={Uri.EscapeDataString(flag.RuleCode)}";
        return new RenderedNotification(title, body, url);
    }

    public RenderedNotification RenderDocumentRetake(DocumentRetakeRequestedEvent retake, string language = "en")
    {
        var isBangla = language.Equals("bn", StringComparison.OrdinalIgnoreCase);
        string title;
        string body;

        if (isBangla)
        {
            title = "নথি পুনরায় ছবি তোলা প্রয়োজন";
            body = $"'{retake.FileName}' নথির স্পষ্ট ছবি পুনরায় আপলোড করুন। কারণ: {retake.Reason}";
        }
        else
        {
            title = "Document Retake Required";
            body = $"Please capture and re-upload a clear copy of '{retake.FileName}'. Reason: {retake.Reason}";
        }

        var url = $"/capture?docId={retake.DocumentId}";
        return new RenderedNotification(title, body, url);
    }

    public RenderedNotification RenderDocumentFailed(DocumentFailedEvent failed, string language = "en")
    {
        var isBangla = language.Equals("bn", StringComparison.OrdinalIgnoreCase);
        string title;
        string body;

        if (isBangla)
        {
            title = "নথি প্রক্রিয়াকরণ ব্যর্থ হয়েছে";
            body = $"'{failed.FileName}' নথি প্রক্রিয়াকরণ সম্পন্ন করা যায়নি। কারণ: {failed.FailureReason}";
        }
        else
        {
            title = "Document Processing Failed";
            body = $"Processing failed for '{failed.FileName}'. Reason: {failed.FailureReason}";
        }

        var url = $"/review?docId={failed.DocumentId}";
        return new RenderedNotification(title, body, url);
    }

    public RenderedDigest RenderWeeklyDigest(WeeklyDigestModel model, string language = "en")
    {
        var isBangla = language.Equals("bn", StringComparison.OrdinalIgnoreCase);
        var countStr = FormatNumber(model.TotalActiveAlertsCount, language);
        var topCountStr = FormatNumber(model.Top5Items.Count, language);

        var subject = isBangla
            ? $"[{model.OrganizationName}] সাপ্তাহিক কার্বন সারাংশ ({model.Period}) - শীর্ষ {topCountStr}টি সতর্কতা"
            : $"[{model.OrganizationName}] Weekly Carbon Digest ({model.Period}) - Top {topCountStr} Alerts";

        var sbHtml = new StringBuilder();
        var sbText = new StringBuilder();

        if (isBangla)
        {
            sbText.AppendLine(CultureInfo.InvariantCulture, $"কার্বনবিল সাপ্তাহিক সারাংশ - {model.OrganizationName} ({model.Period})");
            sbText.AppendLine(CultureInfo.InvariantCulture, $"মোট সক্রিয় সতৰ্কবার্তা: {countStr}টি। নীচে শীর্ষ ৫টি অগ্রাধিকার সতৰ্কবার্তা দেওয়া হলো:");
            sbText.AppendLine("--------------------------------------------------");

            sbHtml.AppendLine(CultureInfo.InvariantCulture, $"<div style=\"font-family: 'Noto Sans Bengali', sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;\">");
            sbHtml.AppendLine(CultureInfo.InvariantCulture, $"  <h2 style=\"color: #0f172a;\">সাপ্তাহিক কার্বন সারাংশ</h2>");
            sbHtml.AppendLine(CultureInfo.InvariantCulture, $"  <p style=\"color: #475569;\">প্রতিষ্ঠান: <strong>{model.OrganizationName}</strong> | মেয়াদ: <strong>{model.Period}</strong></p>");
            sbHtml.AppendLine(CultureInfo.InvariantCulture, $"  <p>মোট সক্রিয় বিষয়: <strong>{countStr}টি</strong>। কাজের সুবিধার্থে ড্যাশবোর্ডের শীর্ষ {topCountStr}টি বিষয় উপস্থাপিত হলো:</p>");
            sbHtml.AppendLine("  <div style=\"margin-top: 16px;\">");
        }
        else
        {
            sbText.AppendLine(CultureInfo.InvariantCulture, $"CarbonBill Weekly Digest - {model.OrganizationName} ({model.Period})");
            sbText.AppendLine(CultureInfo.InvariantCulture, $"Total Active Alerts: {countStr}. Top {topCountStr} Priority Action Items:");
            sbText.AppendLine("--------------------------------------------------");

            sbHtml.AppendLine(CultureInfo.InvariantCulture, $"<div style=\"font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;\">");
            sbHtml.AppendLine(CultureInfo.InvariantCulture, $"  <h2 style=\"color: #0f172a;\">Weekly Carbon Digest</h2>");
            sbHtml.AppendLine(CultureInfo.InvariantCulture, $"  <p style=\"color: #475569;\">Organization: <strong>{model.OrganizationName}</strong> | Period: <strong>{model.Period}</strong></p>");
            sbHtml.AppendLine(CultureInfo.InvariantCulture, $"  <p>Total Active Issues: <strong>{countStr}</strong>. Below are the Top {topCountStr} Priority items respecting your dashboard focus:</p>");
            sbHtml.AppendLine("  <div style=\"margin-top: 16px;\">");
        }

        var index = 1;
        foreach (var item in model.Top5Items)
        {
            var itemIndex = FormatNumber(index++, language);
            var badgeColor = item.Severity.ToUpperInvariant() switch
            {
                "RED" => "#ef4444",
                "AMBER" => "#f59e0b",
                _ => "#3b82f6"
            };

            sbText.AppendLine(CultureInfo.InvariantCulture, $"#{itemIndex} [{item.Severity}] {item.Title}");
            sbText.AppendLine(CultureInfo.InvariantCulture, $"   {item.Details}");
            if (!string.IsNullOrEmpty(item.Url))
            {
                sbText.AppendLine(CultureInfo.InvariantCulture, $"   Link: {item.Url}");
            }
            sbText.AppendLine();

            sbHtml.AppendLine(CultureInfo.InvariantCulture, $"    <div style=\"margin-bottom: 12px; padding: 12px; background: #f8fafc; border-left: 4px solid {badgeColor}; border-radius: 4px;\">");
            sbHtml.AppendLine(CultureInfo.InvariantCulture, $"      <div style=\"font-weight: bold; color: #1e293b;\">#{itemIndex} <span style=\"display: inline-block; padding: 2px 6px; font-size: 11px; color: white; background: {badgeColor}; border-radius: 3px;\">{item.Severity}</span> {item.Title}</div>");
            sbHtml.AppendLine(CultureInfo.InvariantCulture, $"      <div style=\"font-size: 14px; color: #334155; margin-top: 4px;\">{item.Details}</div>");
            if (!string.IsNullOrEmpty(item.Url))
            {
                var actionText = isBangla ? "বিস্তারিত দেখুন &rarr;" : "View Details &rarr;";
                sbHtml.AppendLine(CultureInfo.InvariantCulture, $"      <div style=\"margin-top: 6px;\"><a href=\"{item.Url}\" style=\"color: #2563eb; text-decoration: none; font-size: 13px;\">{actionText}</a></div>");
            }
            sbHtml.AppendLine("    </div>");
        }

        if (isBangla)
        {
            sbHtml.AppendLine("  </div>");
            sbHtml.AppendLine("  <div style=\"margin-top: 24px; padding-top: 12px; border-top: 1px solid #cbd5e1; font-size: 12px; color: #64748b;\">");
            sbHtml.AppendLine("    <em>ঘোষণা: কার্বনবিল ফলাফল GHG প্রোটোকল পদ্ধতির সাথে সামঞ্জস্যপূর্ণ আনুমানিক হিসাব, এটি কোনো নিরীক্ষিত বা প্রত্যয়িত সনদ নয়।</em>");
            sbHtml.AppendLine("  </div>");
            sbHtml.AppendLine("</div>");

            sbText.AppendLine("--------------------------------------------------");
            sbText.AppendLine("ঘোষণা: কার্বনবিল ফলাফল GHG প্রোটোকল পদ্ধতির সাথে সামঞ্জস্যপূর্ণ আনুমানিক হিসাব, এটি কোনো নিরীক্ষিত বা প্রত্যয়িত সনদ নয়।");
        }
        else
        {
            sbHtml.AppendLine("  </div>");
            sbHtml.AppendLine("  <div style=\"margin-top: 24px; padding-top: 12px; border-top: 1px solid #cbd5e1; font-size: 12px; color: #64748b;\">");
            sbHtml.AppendLine("    <em>Disclaimer: Outputs are estimates aligned with GHG Protocol methodology, not audited or certified.</em>");
            sbHtml.AppendLine("  </div>");
            sbHtml.AppendLine("</div>");

            sbText.AppendLine("--------------------------------------------------");
            sbText.AppendLine("Disclaimer: Outputs are estimates aligned with GHG Protocol methodology, not audited or certified.");
        }

        return new RenderedDigest(subject, sbHtml.ToString(), sbText.ToString());
    }
}
