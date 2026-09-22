namespace BattleShip.Models.Domain;

/// <summary>
/// Remplace la boucle synchrone qui résolvait autrefois un lot de tirs (tir/salve/torpille/frappe aérienne) d'un
/// bloc. Avance case par case et s'arrête, sans muter, dès qu'une case a besoin d'un mini-jeu de précision (voir
/// docs/adr/0019-mini-jeu-de-precision.md) : impossible de décider à l'avance si une case plus loin dans le lot
/// couperait un navire tant que le résultat d'un défi sur une case précédente du même navire n'est pas connu
/// (cas d'une frappe aérienne ou d'une salve touchant plusieurs cases d'un même navire).
/// </summary>
internal sealed class VolleySequencer
{
    private readonly Board _target;
    private readonly IReadOnlyList<Coordinate> _cells;
    private readonly bool _stopOnFirstHit;
    private readonly Func<Coordinate, bool> _needsChallenge;
    private readonly List<ShotResolution> _resolutions = [];
    private int _index;

    public VolleySequencer(Board target, IReadOnlyList<Coordinate> cells, bool stopOnFirstHit, Func<Coordinate, bool> needsChallenge)
    {
        _target = target;
        _cells = cells;
        _stopOnFirstHit = stopOnFirstHit;
        _needsChallenge = needsChallenge;
    }

    public IReadOnlyList<ShotResolution> Resolutions => _resolutions;

    /// <summary>Avance tant que les cases ne demandent pas de défi ; renvoie la case en attente, ou <c>null</c> si le lot est terminé.</summary>
    public Coordinate? Advance()
    {
        while (_index < _cells.Count)
        {
            var cell = _cells[_index];
            if (!_target.IsValidTarget(cell))
            {
                _index++;
                continue;
            }

            if (_needsChallenge(cell))
                return cell;

            _resolutions.Add(_target.ReceiveShot(cell));
            _index++;
            if (ShouldStop())
                return null;
        }

        return null;
    }

    /// <summary>
    /// Applique le résultat du défi en attente sur la case courante avec la résolution fournie par l'appelant
    /// (qui seul sait, selon Attack/Defense et réussite/échec, s'il s'agit d'un <see cref="Board.ReceiveShot"/>,
    /// d'un <see cref="Board.ReceiveShotForcedMiss"/> ou d'un <see cref="Board.ReceiveShotDodged"/>), puis reprend l'avancement.
    /// </summary>
    public Coordinate? SubmitChallengeResult(Func<Coordinate, ShotResolution> resolve)
    {
        var cell = _cells[_index];
        _resolutions.Add(resolve(cell));
        _index++;
        return ShouldStop() ? null : Advance();
    }

    private bool ShouldStop() =>
        _target.AllSunk || (_stopOnFirstHit && _resolutions.Count > 0 && _resolutions[^1].Outcome != ShotOutcome.Miss);
}
