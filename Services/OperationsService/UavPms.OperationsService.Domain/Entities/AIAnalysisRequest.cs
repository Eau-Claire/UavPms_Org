using System;
using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;
public class AIAnalysisRequest : BaseEntity
{
    public Guid? BatchId { get; set; }
    public Guid UploadedBy { get; set; }
    public Guid? MediaId { get; set; }
    public Guid? MissionId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public AnalysisType AnalysisType { get; set; } = AnalysisType.General;
    public string? Notes { get; set; }
    public AIAnalysisStatus Status { get; set; } = AIAnalysisStatus.Pending;
    public string? Result { get; set; }
    public DateTime? CompletedAt { get; set; }
    public virtual User? Uploader { get; set; }
    public virtual InspectionMedia? Media { get; set; }
    public virtual Mission? Mission { get; set; }
}
