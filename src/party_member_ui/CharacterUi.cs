namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Godot;

[Meta(typeof(IAutoNode))]
public partial class CharacterUi : Control
{
    [Node] private RichTextLabel _nameLabel { get; set; } = default!;
    [Node] private RichTextLabel _hpValue { get; set; } = default!;
    [Node] private RichTextLabel _tpValue { get; set; } = default!;
}
