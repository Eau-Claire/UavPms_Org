using MediatR;
using UavPms.IdentityService.Domain.Enums;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.SendOtp;

public record SendOtpCommand (
    string Email,
    OtpPurpose OtpPurpose,
    bool IsResend = false
) : IRequest;