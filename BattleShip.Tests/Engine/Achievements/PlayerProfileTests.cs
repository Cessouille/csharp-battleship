using BattleShip.Models.Achievements;
using BattleShip.Models.Domain;

namespace BattleShip.Tests.Engine.Achievements;

/// <summary>
/// TICKET-14 : S-03 — 👑 Reine du difficile (3 victoires cumulées en Difficile). PlayerProfile.Project est
/// une fonction pure sur une liste de GameOutcome — pas de stockage de profil séparé, la projection est
/// recalculée à chaque lecture (voir docs/adr/0018-profil-joueur-anonyme.md).
/// </summary>
public class PlayerProfileTests
{
    private static GameOutcome Victory(AiDifficulty difficulty, params AchievementId[] achievements) =>
        new(Guid.NewGuid(), GameStatus.Finished, PlayerId.Human, difficulty, achievements);

    private static GameOutcome Defeat(AiDifficulty difficulty) =>
        new(Guid.NewGuid(), GameStatus.Finished, PlayerId.Computer, difficulty, []);

    private static GameOutcome InProgress(AiDifficulty difficulty, params AchievementId[] achievements) =>
        new(Guid.NewGuid(), GameStatus.InProgress, null, difficulty, achievements);

    [Fact]
    public void ThreeHardVictories_UnlockReineDuDifficile()
    {
        var profile = PlayerProfile.Project([Victory(AiDifficulty.Hard), Victory(AiDifficulty.Hard), Victory(AiDifficulty.Hard)]);

        Assert.Equal(3, profile.HardVictories);
        Assert.Contains(AchievementId.ReineDuDifficile, profile.Achievements);
    }

    [Fact]
    public void TwoHardVictories_DoNotUnlock()
    {
        var profile = PlayerProfile.Project([Victory(AiDifficulty.Hard), Victory(AiDifficulty.Hard)]);

        Assert.Equal(2, profile.HardVictories);
        Assert.DoesNotContain(AchievementId.ReineDuDifficile, profile.Achievements);
    }

    [Fact]
    public void EasyAndMediumVictories_DoNotCountTowardsHardVictories()
    {
        var profile = PlayerProfile.Project([Victory(AiDifficulty.Easy), Victory(AiDifficulty.Medium), Victory(AiDifficulty.Hard)]);

        Assert.Equal(1, profile.HardVictories);
    }

    [Fact]
    public void LostHardGame_DoesNotCount()
    {
        var profile = PlayerProfile.Project([Defeat(AiDifficulty.Hard), Victory(AiDifficulty.Hard), Victory(AiDifficulty.Hard)]);

        Assert.Equal(2, profile.HardVictories);
    }

    [Fact]
    public void UnfinishedHardGame_DoesNotCount()
    {
        var profile = PlayerProfile.Project([InProgress(AiDifficulty.Hard), Victory(AiDifficulty.Hard), Victory(AiDifficulty.Hard)]);

        Assert.Equal(2, profile.HardVictories);
    }

    [Fact]
    public void NonConsecutiveVictories_StillCount()
    {
        var profile = PlayerProfile.Project(
        [
            Victory(AiDifficulty.Hard),
            Defeat(AiDifficulty.Hard),
            Victory(AiDifficulty.Hard),
            Defeat(AiDifficulty.Hard),
            Victory(AiDifficulty.Hard)
        ]);

        Assert.Equal(3, profile.HardVictories);
        Assert.Contains(AchievementId.ReineDuDifficile, profile.Achievements);
    }

    [Fact]
    public void GameAchievements_AreUnioned_WithoutDuplicates()
    {
        var profile = PlayerProfile.Project(
        [
            Victory(AiDifficulty.Hard, AchievementId.RangeeParfaite),
            InProgress(AiDifficulty.Easy, AchievementId.RangeeParfaite), // même succès, partie en cours : pas de doublon
            Victory(AiDifficulty.Easy, AchievementId.GlowUp)
        ]);

        Assert.Equal(
            new[] { AchievementId.GlowUp, AchievementId.RangeeParfaite }.OrderBy(id => id),
            profile.Achievements.OrderBy(id => id));
    }

    /// <summary>Idempotence : l'index playerId → parties ne peut pas dupliquer un gameId (ConcurrentDictionary
    /// utilisé comme ensemble côté InMemoryGameStore), mais Project reste défensif si on l'appelait deux fois.</summary>
    [Fact]
    public void SameGameListedTwice_CountsItsVictoryOnce()
    {
        var outcome = Victory(AiDifficulty.Hard);

        var profile = PlayerProfile.Project([outcome, outcome]);

        Assert.Equal(1, profile.HardVictories);
    }

    [Fact]
    public void NoGames_ReturnsEmptyProfile()
    {
        var profile = PlayerProfile.Project([]);

        Assert.Equal(0, profile.HardVictories);
        Assert.Empty(profile.Achievements);
    }
}
