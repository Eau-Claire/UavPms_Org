using System;
using System.IO;
using MediatR;

namespace UavPms.Application.Features.Inspections.Commands.UploadImage;

public class UploadInspectionImageCommand : IRequest<UploadInspectionImageResult>
{
    public Guid MissionId { get; set; }
    public Stream FileStream { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}
