using System;
using System.Collections.ObjectModel;
using MindMapApp.Helpers;
using MindMapApp.Models;

namespace MindMapApp.ViewModels;

public class NodeViewModel : ViewModelBase
{
    private string _text;
    private double _x;
    private double _y;
    private bool _isSelected;
    private bool _isEditing;
    private bool _isCollapsed;
    private string? _customColor;

    public MindMapNode Model { get; }
    public Guid Id => Model.Id;
    public bool IsRoot => Model.IsRoot;
    public string NodeType => IsRoot ? "Racine" : "Enfant";

    /// <summary>Référence au ViewModel parent dans l'arbre (null pour la racine).</summary>
    public NodeViewModel? ParentViewModel { get; internal set; }

    /// <summary>Profondeur dans l'arbre : 0 pour la racine, 1 pour ses enfants, etc.</summary>
    public int Depth => ParentViewModel is null ? 0 : ParentViewModel.Depth + 1;

    /// <summary>Couleur personnalisée (hex). Null ou vide = hérite la couleur du niveau.</summary>
    public string? CustomColor
    {
        get => _customColor;
        set
        {
            if (SetField(ref _customColor, value))
                Model.CustomColor = string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public ObservableCollection<NodeViewModel> Children { get; } = new();

    public bool HasChildren => Children.Count > 0;

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (SetField(ref _isCollapsed, value))
            {
                Model.IsCollapsed = value;
                OnPropertyChanged(nameof(CollapseLabel));
            }
        }
    }

    public string CollapseLabel => IsCollapsed ? "Réduit" : "Développé";

    public void ToggleCollapse()
    {
        if (!HasChildren) return;
        IsCollapsed = !IsCollapsed;
    }

    public string Text
    {
        get => _text;
        set
        {
            if (SetField(ref _text, value))
                Model.Text = value;
        }
    }

    public double X
    {
        get => _x;
        set
        {
            if (SetField(ref _x, value))
                Model.X = value;
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (SetField(ref _y, value))
                Model.Y = value;
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetField(ref _isEditing, value);
    }

    public NodeViewModel(MindMapNode model)
    {
        Model = model;
        _text = model.Text;
        _x = model.X;
        _y = model.Y;
        _isCollapsed = model.IsCollapsed;
        _customColor = model.CustomColor;
    }

    public void AddChild(NodeViewModel child)
    {
        child.ParentViewModel = this;
        Model.AddChild(child.Model);
        Children.Add(child);
        OnPropertyChanged(nameof(HasChildren));
    }

    public void RemoveChild(NodeViewModel child)
    {
        Model.RemoveChild(child.Model);
        Children.Remove(child);
        OnPropertyChanged(nameof(HasChildren));
    }
}
