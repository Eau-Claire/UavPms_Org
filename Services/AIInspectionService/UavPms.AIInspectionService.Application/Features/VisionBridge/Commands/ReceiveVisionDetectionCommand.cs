using System;
using MediatR;
using UavPms.AIInspectionService.Application.Features.VisionBridge.DTOs;

namespace UavPms.AIInspectionService.Application.Features.VisionBridge.Commands;

public class ReceiveVisionDetectionCommand : IRequest<VisionDetectionResultDto>
{
    public VisionDetectionDto Detection { get; set; } = null!;
    public Stream? EvidenceImageStream { get; set; }
    public string? EvidenceFileName { get; set; }
}
