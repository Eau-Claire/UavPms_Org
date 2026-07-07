using System;
using MediatR;

namespace UavPms.Application.Features.AIAnalysis.Queries.GetAnalysisById;

/// <summary>
/// Query lấy kết quả phân tích AI theo ID.
/// </summary>
public record GetAIAnalysisByIdQuery(Guid Id) : IRequest<AIAnalysisDetailResult>;
