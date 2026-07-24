using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UavPms.AIInspectionService.Domain.Models.Monitor
{
    public class DefectStatisticsModel
    {
        public int TotalDefects { get; set; }
        public List<DefectTypeStatModel> ByType { get; set; } = new();
        public class DefectTypeStatModel
        {
            public string DefectType { get; set; } = string.Empty;
            public int Count { get; set; }
        }
    }
}
