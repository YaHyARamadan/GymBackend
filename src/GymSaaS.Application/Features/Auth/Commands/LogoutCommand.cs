using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Auth.Commands;

public record LogoutCommand : IRequest<bool>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public LogoutCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.TokenId) ||
            !_currentUserService.TokenExpiresAt.HasValue)
            return true;

        var exists = await _dbContext.Set<RevokedToken>()
            .AnyAsync(t => t.Jti == _currentUserService.TokenId, cancellationToken);

        if (!exists)
        {
            _dbContext.Set<RevokedToken>().Add(new RevokedToken
            {
                Jti = _currentUserService.TokenId,
                ExpiresAt = _currentUserService.TokenExpiresAt.Value
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
