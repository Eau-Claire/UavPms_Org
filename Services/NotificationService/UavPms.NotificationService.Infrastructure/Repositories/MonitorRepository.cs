using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UavPms.NotificationService.Domain.Interfaces.Repositories;
using UavPms.NotificationService.Domain.Models.Monitor;
using UavPms.NotificationService.Infrastructure.Persistence;

namespace UavPms.NotificationService.Infrastructure.Repositories
{
    public class MonitorRepository : IMonitorRepository
    {
        private readonly ApplicationDbContext _context;

        public MonitorRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ActiveAlertModel>> GetActiveAlertsAsync(CancellationToken cancellationToken)
        {
            return await _context.Notifications
                .AsNoTracking()
                .Where(n => !n.IsRead)
                .OrderByDescending(n => n.SentAt)
                .Select(n => new ActiveAlertModel
                {
                    NotificationId = n.Id,
                    Title = n.Title,
                    Message = n.Body,
                    CreatedAt = n.SentAt,
                    IsRead = n.IsRead
                })
                .ToListAsync(cancellationToken);
        }

        public Task<DefectStatisticsModel> GetDefectStatisticsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DefectStatisticsModel());
        }

        public Task<(List<InspectionHistoryModel> Items, int TotalCount)> GetInspectionHistoryAsync(
            Guid? missionId,
            bool? isDefect,
            DateTime? fromDate,
            DateTime? toDate,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            return Task.FromResult((new List<InspectionHistoryModel>(), 0));
        }

        public Task<MissionStatusOverviewModel> GetMissionStatusOverviewAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new MissionStatusOverviewModel());
        }

        public Task<(List<RecentDefectModel> Items, int TotalCount)> GetRecentDefectsAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            return Task.FromResult((new List<RecentDefectModel>(), 0));
        }

        public Task<MonitorSummaryModel> GetSummaryAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new MonitorSummaryModel());
        }
    }
}
