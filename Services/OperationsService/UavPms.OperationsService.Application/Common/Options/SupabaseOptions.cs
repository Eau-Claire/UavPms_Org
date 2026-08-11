using System.ComponentModel.DataAnnotations;

namespace UavPms.OperationsService.Application.Common.Options;

public class SupabaseOptions
{
    public const string SectionName = "Supabase";
    
    [Required(ErrorMessage = "Supabase:Url is required")]
    [Url(ErrorMessage = "Supabase:Url must be a valid URL.")]
    public string Url { get; init; } = "https://hurroumcfjmzsnzovefm.supabase.co";
    
    [Required(ErrorMessage = "Supabase:ApiKey is required")]
    public string ApiKey { get; init; } = string.Empty;
    
    [Required(ErrorMessage = "Supabase:Bucket is required")]
    public string Bucket { get; init; } = "uav-images";
}