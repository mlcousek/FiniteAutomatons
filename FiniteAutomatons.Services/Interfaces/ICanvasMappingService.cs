using FiniteAutomatons.Core.Models.Api;
using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

/// <summary>
/// Provides mapping and transformation services for canvas API requests.
/// </summary>
public interface ICanvasMappingService
{
    /// <summary>
    /// Builds a sync response DTO from a canvas sync request, including derived alphabet, state and transition metadata.
    /// </summary>
    CanvasSyncResponse BuildSyncResponse(CanvasSyncRequest request);

    /// <summary>
    /// Builds an automaton view model from a canvas sync request for use in execution/analysis services.
    /// </summary>
    AutomatonViewModel BuildAutomatonViewModel(CanvasSyncRequest request);
}
