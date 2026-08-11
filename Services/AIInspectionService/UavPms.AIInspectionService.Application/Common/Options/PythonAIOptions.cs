using System.ComponentModel.DataAnnotations;

namespace UavPms.AIInspectionService.Application.Common.Options;

public class PythonAIOptions
{
    public const string SectionName = "PythonAI";
    
    [Required(ErrorMessage = "BaseUrl is required")]
    [Url(ErrorMessage = "PythonAI:BaseUrl must be a valid URL")]
    public string BaseUrl { get; init; } = "http://localhost:8000";
    
    [Range(1, 300, ErrorMessage = "PythonAI:TimeoutSeconds must be between 1 and 300")]
    public int TimeoutSeconds { get; init; } = 60;
}