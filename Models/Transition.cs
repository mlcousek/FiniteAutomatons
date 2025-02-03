namespace FiniteAutomatons.Models
{
    public class Transition
    {
        public int FromStateId { get; set; }
        public int ToStateId { get; set; }
        public char Symbol { get; set; }
    }
}
