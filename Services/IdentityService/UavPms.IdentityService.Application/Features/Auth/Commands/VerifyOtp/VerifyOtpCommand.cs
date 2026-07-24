using MediatR;
using UavPms.IdentityService.Application.Features.Auth.DTOs;
using UavPms.IdentityService.Domain.Enums;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp;

public record VerifyOtpCommand(
    string Email,
    string Code,
    OtpPurpose OtpPurpose,
    string? UserAgent
) : IRequest<OtpVerifyResultDto>;