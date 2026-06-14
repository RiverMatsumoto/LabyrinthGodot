namespace Labyrinth;

public partial record MenuHubLogicState
{
    public static class Input
    {
        public readonly record struct OpenMenuHub;
        public readonly record struct OpenItemMenu;
        public readonly record struct OpenSkillMenu;
        public readonly record struct OpenStatusMenu;
        public readonly record struct OpenEquipMenu;
        public readonly record struct OpenCustomMenu;
        public readonly record struct OpenPartyMenu;
        public readonly record struct OpenQuestMenu;
        public readonly record struct OpenSettings;
        public readonly record struct HandleMenuInput;
        public readonly record struct Back;
        public readonly record struct Close;
    }
}
