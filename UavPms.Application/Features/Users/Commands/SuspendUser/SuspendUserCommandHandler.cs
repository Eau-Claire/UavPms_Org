using MediatR;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Users.Commands.SuspendUser;

public class SuspendUserCommandHandler : IRequestHandler<SuspendUserCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    
    public SuspendUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<bool> Handle(SuspendUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }
        
        user.Status = "Inactive";
        
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}