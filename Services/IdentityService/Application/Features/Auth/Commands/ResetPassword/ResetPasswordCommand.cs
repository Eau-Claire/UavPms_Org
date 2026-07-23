using MediatR;
using UavPms.IdentityService.Domain.Contracts;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.ResetPassword;

public record ResetPasswordCommand(
    string VerificationToken,
    string NewPassword
) : IRequest;