namespace D47.Core.Ship;

public sealed class FuelStatus
{
    public double MainTank { get; private set; }

    public double Capacity { get; private set; }

    public bool IsFull => Capacity > 0 && MainTank >= Capacity;

    public void ApplyLoadout(double fuelCapacityMain) => Capacity = fuelCapacityMain;

    public void ApplyFuelScoop(double scooped, double total)
    {
        MainTank = total;
        Capacity = total;
    }

    public string Describe()
    {
        if (Capacity <= 0)
        {
            return $"{MainTank:0.0} tonnes.";
        }

        return IsFull ? "Tank full." : $"{MainTank:0.0} of {Capacity:0.0} tonnes.";
    }
}
