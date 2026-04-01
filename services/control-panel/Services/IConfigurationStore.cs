using control_panel.Models;

namespace control_panel.Services;

public interface IConfigurationStore
{
    Task<GameConfiguration> GetOrCreateAsync(string gameKey, CancellationToken cancellationToken = default);
    Task<GameConfiguration> SaveAsync(string gameKey, string jsonContent, string updatedBy, CancellationToken cancellationToken = default);
}
