namespace Csi500DropRadar.Models;

public class StockRecord
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public double StartClose { get; set; }
    public double EndClose { get; set; }
    public double PctChange { get; set; }
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
}
