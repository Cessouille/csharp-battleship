using BattleShip.Models.Contracts;

namespace BattleShip.App.Game;

/// <summary>
/// Applique un TurnResultDto (réponse REST d'un tir/torpille/frappe) à l'état de partie déjà hydraté :
/// Id/GameId et Options ne changent jamais en cours de partie, tout le reste vient du tour. Nommé et isolé
/// à part parce qu'un champ oublié ici reste figé jusqu'au prochain scan/salve (chemins gRPC qui remplacent
/// l'état en entier) — voir docs/adr/0017-systeme-de-succes.md.
/// </summary>
public static class GameStateUpdate
{
    public static GameStateDto Apply(this GameStateDto state, TurnResultDto turn) => state with
    {
        Status = turn.Status,
        Winner = turn.Winner,
        MyBoard = turn.MyBoard,
        OpponentBoard = turn.OpponentBoard,
        Actions = turn.Actions,
        History = turn.History,
        Achievements = turn.Achievements,
        // Un TurnResultDto ne peut exister que pour un tour entièrement résolu (voir docs/adr/0019-mini-jeu-de-precision.md,
        // Game.FinalizeTurn) : un défi encore affiché juste avant ce tour ne l'est plus, sans quoi il resterait
        // figé à l'écran indéfiniment (with-expression : un champ non listé ici garde sa valeur précédente).
        PendingChallenge = null
    };
}
