using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Models.Monitor;
using UavPms.OperationsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static UavPms.OperationsService.Domain.Models.Monitor.DefectStatisticsModel;

namespace UavPms.OperationsService.Infrastructure.Repositories
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

        public async Task<DefectStatisticsModel> GetDefectStatisticsAsync(CancellationToken cancellationToken)
        {
            var byType = await _context.DetectedAnomalies
                .AsNoTracking()
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
            var query = _context.InspectionMedia.AsNoTracking().AsQueryable();

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
            var missionStats = await _context.Missions
                .AsNoTracking()
                .GroupBy(m => m.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var pending = missionStats.FirstOrDefault(m => m.Status == "Scheduled")?.Count ?? 0;
            var inProgress = missionStats.FirstOrDefault(m => m.Status == "InProgress")?.Count ?? 0;
            var completed = missionStats.FirstOrDefault(m => m.Status == "Completed")?.Count ?? 0;

            return new MissionStatusOverviewModel
            {
                Pending = pending,
                InProgress = inProgress,
                Completed = completed
            };
        }

        public async Task<(List<RecentDefectModel> Items, int TotalCount)> GetRecentDefectsAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            var query = _context.DetectedAnomalies
                .AsNoTracking()
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
            var missionStats = await _context.Missions
                .AsNoTracking()
                .GroupBy(m => m.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var pendingMissions = missionStats.FirstOrDefault(s => s.Status == "Scheduled")?.Count ?? 0;
            var inProgressMissions = missionStats.FirstOrDefault(s => s.Status == "InProgress")?.Count ?? 0;
            var completedMissions = missionStats.FirstOrDefault(s => s.Status == "Completed")?.Count ?? 0;
            var totalMissions = missionStats.Sum(s => s.Count);

            // 2. Get total inspection media count
            var totalInspections = await _context.InspectionMedia.AsNoTracking().CountAsync(cancellationToken);
            
            // 3. Get defect counts (total and critical) in one query
            var defectStats = await _context.DetectedAnomalies
                .AsNoTracking()
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
