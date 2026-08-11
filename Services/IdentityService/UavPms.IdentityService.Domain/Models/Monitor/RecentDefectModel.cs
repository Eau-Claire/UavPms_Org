using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UavPms.IdentityService.Domain.Models.Monitor
{
    public class RecentDefectModel
    {
        public Guid InspectionId { get; set; }
        public Guid MissionId { get; set; }
        public string MissionTitle { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string DefectType { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
    }
}
