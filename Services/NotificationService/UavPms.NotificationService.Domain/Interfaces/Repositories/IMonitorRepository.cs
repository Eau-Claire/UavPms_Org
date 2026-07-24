using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UavPms.NotificationService.Domain.Models.Monitor;

namespace UavPms.NotificationService.Domain.Interfaces.Repositories
{
    public interface IMonitorRepository
    {

        Task<MonitorSummaryModel> GetSummaryAsync(CancellationToken cancellationToken);
        Task<(List<RecentDefectModel> Items, int TotalCount)> GetRecentDefectsAsync(int page, int pageSize, CancellationToken cancellationToken);
        Task<DefectStatisticsModel> GetDefectStatisticsAsync(CancellationToken cancellationToken);
        Task<MissionStatusOverviewModel> GetMissionStatusOverviewAsync(CancellationToken cancellationToken);
        Task<(List<InspectionHistoryModel> Items, int TotalCount)> GetInspectionHistoryAsync(
            Guid? missionId, 
            bool? IsDefect,
            DateTime? fromDate,
            DateTime? toDate,
            int page,
            int pageSize,
            CancellationToken cancellationToken);

        Task<List<ActiveAlertModel>> GetActiveAlertsAsync(CancellationToken cancellationToken);
    }
}
