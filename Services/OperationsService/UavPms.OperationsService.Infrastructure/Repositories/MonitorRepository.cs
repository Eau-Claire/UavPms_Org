using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Domain.Models.Monitor;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Enums;
using static UavPms.OperationsService.Domain.Models.Monitor.DefectStatisticsModel;

namespace UavPms.OperationsService.Infrastructure.Repositories
{
    public class MonitorRepository : IMonitorRepository
    {
        private static readonly MissionStatus[] PendingMissionStatuses =
        [
            MissionStatus.Draft,             // also MissionStatus.Draft
            MissionStatus.PendingAcceptance, // also MissionStatus.PendingAcceptance
            MissionStatus.Confirmed,
            MissionStatus.Ready,
            MissionStatus.Postponed
        ];

        private static readonly MissionStatus[] InProgressMissionStatuses =
        [
            MissionStatus.InProgress,        // also MissionStatus.InProgress
            MissionStatus.Suspended
        ];

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserServices _currentUser;

        public MonitorRepository(ApplicationDbContext context, ICurrentUserServices currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        // Dashboard chỉ tính dữ liệu của các nhiệm vụ người gọi được xem (spec §4, lọc trong SQL).
        private IQueryable<Mission> VisibleMissions => MissionScope.Visible(_context, _currentUser).AsNoTracking();

        private IQueryable<InspectionMedia> VisibleMedia
        {
            get
            {
                var media = _context.InspectionMedia.AsNoTracking();
                if (RegionScope.IsSystemAdmin(_currentUser))
                    return media;
                var missionIds = MissionScope.VisibleIds(_context, _currentUser);
                return media.Where(m => missionIds.Contains(m.MissionId));
            }
        }

        private IQueryable<DetectedAnomaly> VisibleAnomalies
        {
            get
            {
                var anomalies = _context.DetectedAnomalies.AsNoTracking();
                if (RegionScope.IsSystemAdmin(_currentUser))
                    return anomalies;
                var missionIds = MissionScope.VisibleIds(_context, _currentUser);
                return anomalies.Where(a => missionIds.Contains(a.Media!.MissionId));
            }
        }


        public async Task<List<ActiveAlertModel>> GetActiveAlertsAsync(Guid userId, CancellationToken cancellationToken)
        {
            return await _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.SentAt)
                .Take(50)
                .Select(n => new ActiveAlertModel
                {
                    NotificationId = n.Id,
                    Title = n.Title,
                    Message = n.Body,
                    Type = n.Type,
                    ReferenceType = n.ReferenceType,
                    ReferenceId = n.ReferenceId,
                    CreatedAt = n.SentAt,
                    IsRead = n.ReadAt != null
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<DefectStatisticsModel> GetDefectStatisticsAsync(CancellationToken cancellationToken)
        {
            var byType = await VisibleAnomalies
                .Where(a => a.ValidationStatus == "Confirmed")
                .GroupBy(a => a.Category!.CategoryName)
                .Select(g => new { DefectType = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var totalDefects = byType.Sum(x => x.Count);

            return new DefectStatisticsModel
            {
                TotalDefects = totalDefects,
                ByType = byType.Select(x => new DefectTypeStatModel
                {
                    DefectType = x.DefectType,
                    Count = x.Count
                }).ToList()
            };
        }

        public async Task<(List<InspectionHistoryModel> Items, int TotalCount)> GetInspectionHistoryAsync(Guid? missionId, bool? IsDefect, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken cancellationToken)
        {
            var query = VisibleMedia;

            if (missionId.HasValue)
            {
                query = query.Where(m => m.MissionId == missionId.Value);
            }

            if(IsDefect.HasValue){
                if(IsDefect.Value)
                {
                    query = query.Where(m => 
                    m.DetectedAnomalies.Any(a => 
                    a.ValidationStatus == "Confirmed"));
                }
                else
                {
                    query = query.Where(m =>
                    !m.DetectedAnomalies.Any(a =>
                        a.ValidationStatus == "Confirmed"));
                }
            }

            if (fromDate.HasValue) 
            { 
                query = query.Where(m => m.CapturedAt >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                query = query.Where(m => m.CapturedAt <= toDate.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(m => m.CapturedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new InspectionHistoryModel
                {
                    InspectionId = m.Id,
                    MissionId = m.MissionId,
                    MissionTitle = !string.IsNullOrEmpty(m.Mission!.Description)
                    ? m.Mission.Description : m.Mission.MissionCode,
                    ImageUrl = m.FileUrl,
                    IsDefect = m.DetectedAnomalies.Any(a => a.ValidationStatus == "Confirmed"),
                    DefectType = m.DetectedAnomalies.Where(a => a.ValidationStatus == "Confirmed").Select(a => a.Category!.CategoryName).FirstOrDefault() ?? string.Empty,
                    DetectedAt = m.CapturedAt
                }).ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<MissionStatusOverviewModel> GetMissionStatusOverviewAsync(CancellationToken cancellationToken)
        {
            var missionStats = await VisibleMissions
                .GroupBy(m => m.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var pending = missionStats.Where(m => PendingMissionStatuses.Contains(m.Status)).Sum(m => m.Count);
            var inProgress = missionStats.Where(m => InProgressMissionStatuses.Contains(m.Status)).Sum(m => m.Count);
            var completed = missionStats.Where(m => m.Status == MissionStatus.Completed).Sum(m => m.Count);

            return new MissionStatusOverviewModel
            {
                Pending = pending,
                InProgress = inProgress,
                Completed = completed
            };
        }

        public async Task<(List<RecentDefectModel> Items, int TotalCount)> GetRecentDefectsAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            var query = VisibleAnomalies
                .Where(a => a.ValidationStatus == "Confirmed");
            
            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new RecentDefectModel
                {
                    InspectionId = a.MediaId,
                    MissionId = a.Media!.MissionId,
                    MissionTitle = !string.IsNullOrEmpty(a.Media.Mission!.Description) 
                    ? a.Media.Mission.Description : a.Media.Mission.MissionCode,
                    ImageUrl = a.Media.FileUrl,
                    DefectType = a.Category!.CategoryName,
                    DetectedAt = a.CreatedAt
                }).ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<MonitorSummaryModel> GetSummaryAsync(CancellationToken cancellationToken)
        {
            // 1. Get mission counts grouped by status in one query
            var missionStats = await VisibleMissions
                .GroupBy(m => m.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var pendingMissions = missionStats.Where(s => PendingMissionStatuses.Contains(s.Status)).Sum(s => s.Count);
            var inProgressMissions = missionStats.Where(s => InProgressMissionStatuses.Contains(s.Status)).Sum(s => s.Count);
            var completedMissions = missionStats.Where(s => s.Status == MissionStatus.Completed).Sum(s => s.Count);
            var totalMissions = missionStats.Sum(s => s.Count);

            // 2. Get total inspection media count
            var totalInspections = await VisibleMedia.CountAsync(cancellationToken);
            
            // 3. Get defect counts (total and critical) in one query
            var defectStats = await VisibleAnomalies
                .Where(a => a.ValidationStatus == "Confirmed")
                .GroupBy(a => a.Category!.IsEmergencyClass)
                .Select(g => new { IsCritical = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var totalDefects = defectStats.Sum(s => s.Count);
            var criticalDefects = defectStats.FirstOrDefault(s => s.IsCritical)?.Count ?? 0;

            return new MonitorSummaryModel
            {
                TotalMissions = totalMissions,
                PendingMissions = pendingMissions,
                InProgressMissions = inProgressMissions,
                CompletedMissions = completedMissions,
                TotalInspections = totalInspections,
                TotalDefects = totalDefects,
                CriticalDefects = criticalDefects
            };
        }
    }
}
