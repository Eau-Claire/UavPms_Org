using System.Security.Cryptography;
using System.Text;
using MediatR;
using UavPms.IdentityService.Application.Common.Exceptions;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Domain.Interfaces.Services;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IOtpService _otpService;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    public ResetPasswordCommandHandler(
        IOtpService otpService,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _otpService = otpService;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }
    
    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var hash = HashToken(request.VerificationToken);
        var email = await _otpService.GetVerificationTokenEmailAsync(hash);
        if (string.IsNullOrEmpty(email))
        {
            throw new BusinessRuleException("Invalid token");
        }
        var user = await _userRepository.GetByEmailWithRolesAsync(email);
        if (user == null)
        {
            throw new NotFoundException("User", email);
        }
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        await _otpService.DeleteVerificationTokenAsync(hash);
    }
    
    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}