namespace CarbonBill.Modules.Calculation.Services;

public interface ICalculationEngine
{
    decimal CalculateKgCo2e(decimal standardQuantity, decimal emissionFactor);
}

public class CalculationEngine : ICalculationEngine
{
    public decimal CalculateKgCo2e(decimal standardQuantity, decimal emissionFactor)
    {
        return standardQuantity * emissionFactor;
    }
}
