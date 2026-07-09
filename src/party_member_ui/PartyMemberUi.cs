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
    void SetHp(int hp, int maxHp);
    void SetTp(int tp);
    void SetTp(int tp, int maxTp);
    void SetStatus(StatusId statusId);
}

[Meta(typeof(IAutoNode))]
public partial class PartyMemberUi : Control, IPartyMemberUi
{
    [Node("NameLabel")]
    private Label? NameLabel { get; set; }

    [Node("Bars/HpBar")]
    private ProgressBar? HpBar { get; set; }

    [Node("Bars/TpBar")]
    private ProgressBar? TpBar { get; set; }

    [Node("Bars/HPValue")]
    private Label? HpValue { get; set; }

    [Node("Bars/TPValue")]
    private Label? TpValue { get; set; }


    public void SetNameLabel(string name)
    {
        ResolveNodes();
        NameLabel!.Text = name;
    }

    public void SetStatus(StatusId statusId)
    {
        // status texture util need
    }

    public void SetHp(int hp) => SetHp(hp, Math.Max(1, hp));

    public void SetHp(int hp, int maxHp)
    {
        ResolveNodes();
        HpBar!.MaxValue = Math.Max(1, maxHp);
        HpBar.Value = hp;
        // pass format provider for locale, maybe use locale provider if this
        // might change
        HpValue!.Text = hp.ToString(new CultureInfo("en-US"));
    }

    public void SetTp(int tp) => SetTp(tp, Math.Max(1, tp));

    public void SetTp(int tp, int maxTp)
    {
        ResolveNodes();
        TpBar!.MaxValue = Math.Max(1, maxTp);
        TpBar.Value = tp;
        // pass format provider for locale
        TpValue!.Text = tp.ToString(new CultureInfo("en-US"));
    }

    private void ResolveNodes()
    {
        NameLabel ??= GetNode<Label>("NameLabel");
        HpBar ??= GetNode<ProgressBar>("Bars/HpBar");
        TpBar ??= GetNode<ProgressBar>("Bars/TpBar");
        HpValue ??= GetNode<Label>("Bars/HPValue");
        TpValue ??= GetNode<Label>("Bars/TPValue");
    }
}
