namespace Labyrinth;

public enum GameMode
{
    MainMenu,
    Town,
    Labyrinth,
    Battle,
}

public readonly record struct BattleRequest(
    EncounterId EncounterId,
    int Seed,
    GameMode ReturnMode
)
{
    public static BattleRequest Debug =>
        new(new EncounterId("debug"), 1, GameMode.Labyrinth);
}
