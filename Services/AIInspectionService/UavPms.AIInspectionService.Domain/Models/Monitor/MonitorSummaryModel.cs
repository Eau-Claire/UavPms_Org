namespace UavPms.AIInspectionService.Domain.Models.Monitor;

public class MonitorSummaryModel
{
    public int TotalMissions { get; set; }
    public int PendingMissions { get; set; }
    public int InProgressMissions { get; set; }
    public int CompletedMissions { get; set; }
    public int TotalInspections { get; set; }
    public int TotalDefects { get; set; }
    public int CriticalDefects { get; set;}
}