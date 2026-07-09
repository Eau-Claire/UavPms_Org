using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.AIAnalysis.Queries.GetAnalysisById;

/// <summary>
/// Handler lấy chi tiết kết quả phân tích AI theo ID.
/// </summary>
public class GetAIAnalysisByIdQueryHandler
    : IRequestHandler<GetAIAnalysisByIdQuery, AIAnalysisDetailResult>
{
    private readonly IGenericRepository<AIAnalysisRequest> _repository;

    public GetAIAnalysisByIdQueryHandler(IGenericRepository<AIAnalysisRequest> repository)
    {
        _repository = repository;
    }

    public async Task<AIAnalysisDetailResult> Handle(
        GetAIAnalysisByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, track: false);
        if (entity == null)
        {
            throw new KeyNotFoundException($"AI analysis request with ID '{request.Id}' was not found.");
        }

        return new AIAnalysisDetailResult
        {
            Id = entity.Id,
            UploadedBy = entity.UploadedBy,
            FileUrl = entity.FileUrl,
            MediaType = entity.MediaType,
            AnalysisType = entity.AnalysisType,
            Notes = entity.Notes,
            Status = entity.Status,
            Result = entity.Result,
            CreatedAt = entity.CreatedAt,
            CompletedAt = entity.CompletedAt
        };
    }
}
