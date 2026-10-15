namespace CarbonBill.Modules.Extraction.Services.Classification;

public static class DocumentTypes
{
    public const string ElectricityBill = "ElectricityBill";
    public const string DieselSlip = "DieselSlip";
    public const string GasBill = "GasBill";
    public const string ShippingChallan = "ShippingChallan";
    public const string Unknown = "Unknown";
}

public record ClassificationResult(string DocumentType, float Confidence, string Reason);

public static class DocumentClassifier
{
    public static ClassificationResult Classify(string rawText, string? fileName = null)
    {
        var text = (rawText + " " + (fileName ?? "")).ToUpperInvariant();

        // 1. Electricity Bill Keywords
        if (text.Contains("DESCO") || text.Contains("DPDC") || text.Contains("BPDB") ||
            text.Contains("BREB") || text.Contains("NESCO") || text.Contains("WZPDCL") ||
            text.Contains("POWER DEVELOPMENT BOARD") || text.Contains("PALLI BIDYUT") ||
            text.Contains("WEST ZONE POWER") || text.Contains("বিদ্যুৎ") ||
            text.Contains("ELECTRICITY BILL") || text.Contains("ELECTRICITY CHARGE") ||
            text.Contains("KWH") || text.Contains("SANCTIONED LOAD") || text.Contains("POWER FACTOR") ||
            text.Contains("CONSUMED TAKA") || text.Contains("CONSUMED UNIT") || text.Contains("PRESENT RDG") ||
            text.Contains("LT CONSUMER") || text.Contains("LT-B") || text.Contains("LT-A"))
        {
            return new ClassificationResult(DocumentTypes.ElectricityBill, 0.95f, "Matched electricity provider or power terms (BPDB/DESCO/DPDC/BREB/NESCO/WZPDCL/KWH)");
        }

        // 2. Diesel / Fuel Keywords
        if (text.Contains("DIESEL") || text.Contains("ডিজেল") || text.Contains("OCTANE") ||
            text.Contains("PETROL") || text.Contains("PADMA OIL") || text.Contains("PADMA") ||
            text.Contains("MEGHNA PETROLEUM") || text.Contains("MEGHNA") ||
            text.Contains("JAMUNA OIL") || text.Contains("JAMUNA") ||
            text.Contains("FUEL SLIP") || text.Contains("GENERATOR FUEL"))
        {
            return new ClassificationResult(DocumentTypes.DieselSlip, 0.95f, "Matched fuel slip keywords (Diesel/Padma Oil/Generator Fuel)");
        }

        // 3. Natural Gas Keywords
        if (text.Contains("TITAS") || text.Contains("তিতাস") || text.Contains("BAKHRABAD") ||
            text.Contains("বাখরাবাদ") || text.Contains("KARNAPHULI") || text.Contains("KGDCL") ||
            text.Contains("কর্ণফুলী") || text.Contains("JALALABAD") || text.Contains("জালালাবাদ") ||
            text.Contains("PASCHIMANCHAL") || text.Contains("PGCL") || text.Contains("SUNDARBAN GAS") ||
            text.Contains("SGCL") || text.Contains("GAS BILL") || text.Contains("RMS") ||
            text.Contains("CUBIC METER") || text.Contains("গ্যাস বিল") || text.Contains("M3"))
        {
            return new ClassificationResult(DocumentTypes.GasBill, 0.95f, "Matched gas distribution provider keywords (Titas/Bakhrabad/Karnaphuli/Jalalabad/Gas Bill)");
        }

        // 4. Shipping / Cargo Challan
        if (text.Contains("CHALLAN") || text.Contains("চালান") || text.Contains("TRANSPORT") ||
            text.Contains("TRUCK") || text.Contains("CARGO") || text.Contains("FREIGHT") ||
            text.Contains("TONNE-KM") || text.Contains("DELIVERY SLIP"))
        {
            return new ClassificationResult(DocumentTypes.ShippingChallan, 0.90f, "Matched transport / delivery challan keywords");
        }

        return new ClassificationResult(DocumentTypes.Unknown, 0.30f, "Insufficient keywords to reliably classify");
    }
}
