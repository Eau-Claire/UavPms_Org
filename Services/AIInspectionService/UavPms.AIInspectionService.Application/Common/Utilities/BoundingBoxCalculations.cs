namespace UavPms.AIInspectionService.Application.Common.Utilities;

public class BoundingBoxCalculations
{
    public static double CalculateArea(double width, double height)
    {
        if (width < 0 || height < 0) return 0;
        
        return width * height;
    }

    public static double CalculateIoU(double x1Min, double y1Min, double x1Max, double y1Max,
        double x2Min, double y2Min, double x2Max, double y2Max)
    {
        double interXMin = Math.Max(x1Min, x2Min);
        double interYMin = Math.Max(y1Min, y2Min);
        double interXMax = Math.Min(x1Max, x2Max);
        double interYMax = Math.Min(y1Max, y2Max);
        
        double interWidth = Math.Max(0, interXMax - interXMin);
        double interHeight = Math.Max(0, interYMax - interYMin);
        double interArea = CalculateArea(interWidth, interHeight);
        
        double area1 = CalculateArea(x1Max - x1Min, y1Max - y1Min);
        double area2 = CalculateArea(x2Max - x2Min, y2Max - y2Min);

        double unionArea = area1 + area2 - interArea;
        if(unionArea <= 0) return 0;
        
        return interArea / unionArea;
    }

    public static bool IsEmergencyClass(string categoryCode)
    {
        if (string.IsNullOrEmpty(categoryCode)) return false;

        var code = categoryCode.Trim().ToUpperInvariant();

        return code is "FIRE" or "CABLE_BREAK" or "TOWER_COLLAPSE" or "CRITICAL_CORROSION";
    }
}