/// <summary>
/// Implemented by creatures whose own movement would otherwise fight the tornado's
/// physics pull (Livestock, pets): while TornadoCarried is true the creature's
/// controller stops overwriting the Rigidbody velocity so the tornado can carry it.
/// </summary>
public interface ITornadoCarried
{
    bool TornadoCarried { get; set; }
}