using FiniteAutomatons.Core.Models.Api;
using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface ICanvasMappingService
{
    CanvasSyncResponse BuildSyncResponse(CanvasSyncRequest request);
    AutomatonViewModel BuildAutomatonViewModel(CanvasSyncRequest request);
}
