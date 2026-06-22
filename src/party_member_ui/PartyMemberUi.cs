namespace Labyrinth;

using System;
using System.Globalization;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface IPartyMemberUi : IControl
{
    void SetNameLabel(string name);
    void SetHp(int hp);
    void SetTp(int tp);
    void SetStatus(StatusId statusId);
}

[Meta(typeof(IAutoNode))]
public partial class PartyMemberUi : Control, IPartyMemberUi
{
    [Node] private IRichTextLabel NameLabel { get; init; } = default!;
    [Node] private IProgressBar HpBar { get; init; } = default!;
    [Node] private IProgressBar TpBar { get; init; } = default!;
    [Node] private IRichTextLabel HpValue { get; init; } = default!;
    [Node] private IRichTextLabel TpValue { get; init; } = default!;


    public void SetNameLabel(string name)
    {
        NameLabel.Text = name;
    }

    public void SetStatus(StatusId statusId)
    {
        // status texture util need
    }

    public void SetHp(int hp)
    {
        HpBar.Value = hp;
        // pass format provider for locale, maybe use locale provider if this
        // might change
        HpValue.Text = hp.ToString(new CultureInfo("en-US"));
    }

    public void SetTp(int tp)
    {
        TpBar.Value = tp;
        // pass format provider for locale
        TpValue.Text = tp.ToString(new CultureInfo("en-US"));
    }
}
