using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UavPms.OperationsService.Domain.Models.Monitor
{
    public class MissionStatusOverviewModel
    {
        public int Pending { get; set;  }
        public int InProgress { get; set; }
        public int Completed { get; set; }
    }
}
