namespace HostIt;

public class PortMetaData
{
    public int RangeStart { get; init; } = 8080;

    public int RangeCount { get; } = 1_000;

    public Dictionary<string, int> Tags { get; } = new();

    private IEnumerator<int> _Ports { get; set; }

    public int ReservePort()
    {
        if (_Ports is null) {
            _Ports = Enumerable.Range(RangeStart, RangeCount).GetEnumerator();
        }

        if (_Ports.MoveNext()) {
            return _Ports.Current;
        } else {
            throw new ArgumentOutOfRangeException($"No more ports starting at {RangeStart} with {RangeCount} ports");
        }
    }
}
