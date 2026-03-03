namespace FiniteAutomatons.Core.Models.Api;

public class PanelOrderRequest
{
    public string Preferences { get; set; } = string.Empty;
}

public class CanvasWheelRequest
{
    public bool Enabled { get; set; }
}
