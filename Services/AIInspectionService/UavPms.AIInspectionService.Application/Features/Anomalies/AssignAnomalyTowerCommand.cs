using MediatR;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Common.Security;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;

namespace UavPms.AIInspectionService.Application.Features.Anomalies;

/// <summary>Links an anomaly to a tower (required before it can be Confirmed, ERD v2.2).</summary>
public record AssignAnomalyTowerCommand(Guid AnomalyId, Guid TowerId) : IRequest<AnomalyTowerAssignmentDto>;

public class AssignAnomalyTowerCommandHandler : IRequestHandler<AssignAnomalyTowerCommand, AnomalyTowerAssignmentDto>
{
    private readonly IAnomalyRepository _anomalyRepository;
    private readonly IGenericRepository<Tower> _towerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMissionAccessPolicy _accessPolicy;

    public AssignAnomalyTowerCommandHandler(
        IAnomalyRepository anomalyRepository,
        IGenericRepository<Tower> towerRepository,
        IUnitOfWork unitOfWork,
        IMissionAccessPolicy accessPolicy)
    {
        _anomalyRepository = anomalyRepository;
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
    }

    public async Task<AnomalyTowerAssignmentDto> Handle(AssignAnomalyTowerCommand request, CancellationToken cancellationToken)
    {
        if (request.TowerId == Guid.Empty)
        {
            throw new BusinessRuleException("INVALID_TOWER", "Tower ID is invalid.");
        }

        var anomaly = await _anomalyRepository.GetByIdWithDetailAsync(request.AnomalyId)
            ?? throw new NotFoundException(nameof(DetectedAnomaly), request.AnomalyId);
        // Đổi cột thủ công đi cùng quyền duyệt defect (spec §4).
        if (!await _accessPolicy.CanReviewAsync(anomaly.Media?.Mission))
        {
            throw new ForbiddenException("DEFECT_REVIEW_DENIED");
        }
        if (anomaly.SupersededById.HasValue)
        {
            throw new BusinessRuleException("ANOMALY_SUPERSEDED",
                $"The anomaly was superseded by {anomaly.SupersededById}; assign the tower on that anomaly instead.");
        }
        // A resolved anomaly is closed maintenance history: moving it would rewrite another tower's history.
        if (string.Equals(anomaly.ValidationStatus, "Resolved", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("ANOMALY_RESOLVED", "The tower of a resolved anomaly cannot be changed.");
        }

        var tower = await _towerRepository.GetByIdAsync(request.TowerId, false, cancellationToken);
        if (tower == null || tower.IsDeleted)
        {
            throw new NotFoundException(nameof(Tower), request.TowerId);
        }

        anomaly.TowerId = tower.Id;
        anomaly.UpdatedAt = DateTime.UtcNow;
        await _anomalyRepository.UpdateAsync(anomaly);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AnomalyTowerAssignmentDto(anomaly.Id, tower.Id, tower.TowerCode, anomaly.ValidationStatus);
    }
}
