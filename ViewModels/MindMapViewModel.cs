using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using MindMapApp.Helpers;
using MindMapApp.Models;

namespace MindMapApp.ViewModels;

public class MindMapViewModel : ViewModelBase
{
    private NodeViewModel? _selectedNode;
    private double _scale = 1.0;
    private double _offsetX;
    private double _offsetY;

    public MindMap Model { get; private set; }

    public string Title
    {
        get => Model.Title;
        set
        {
            if (Model.Title == value) return;
            Model.Title = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<NodeViewModel> AllNodes { get; } = new();

    // ──────────────── Couleurs par niveau ────────────────

    private static readonly string[] DefaultLevelColors =
    {
        "#6366F1", // 0 Racine
        "#10B981", // 1
        "#F59E0B", // 2
        "#EF4444", // 3
        "#8B5CF6", // 4
        "#06B6D4", // 5
        "#84CC16", // 6
        "#F97316", // 7
    };

    /// <summary>Couleurs éditables par l'utilisateur pour chaque niveau de profondeur.</summary>
    public ObservableCollection<LevelColorViewModel> LevelColors { get; }

    /// <summary>Retourne la couleur effective d'un nœud : CustomColor si définie, sinon la couleur du niveau.</summary>
    public string GetNodeFillColor(NodeViewModel node)
    {
        if (!string.IsNullOrWhiteSpace(node.CustomColor))
            return node.CustomColor;
        int depth = node.Depth;
        return depth < LevelColors.Count
            ? LevelColors[depth].Color
            : LevelColors[LevelColors.Count - 1].Color;
    }

    private void OnLevelColorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is LevelColorViewModel lc)
            Model.LevelColors[lc.Level] = lc.Color;
    }

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode is not null)
                _selectedNode.IsSelected = false;
            SetField(ref _selectedNode, value);
            if (_selectedNode is not null)
                _selectedNode.IsSelected = true;
        }
    }

    public double Scale
    {
        get => _scale;
        set => SetField(ref _scale, Math.Clamp(value, 0.2, 5.0));
    }

    public double OffsetX
    {
        get => _offsetX;
        set => SetField(ref _offsetX, value);
    }

    public double OffsetY
    {
        get => _offsetY;
        set => SetField(ref _offsetY, value);
    }

    public MindMapViewModel(MindMap model)
    {
        Model = model;

        // Initialise les couleurs de niveau depuis le modèle (ou les défauts)
        LevelColors = new ObservableCollection<LevelColorViewModel>();
        for (int i = 0; i < DefaultLevelColors.Length; i++)
        {
            var hex = model.LevelColors.TryGetValue(i, out var c) ? c : DefaultLevelColors[i];
            var lc = new LevelColorViewModel(i, hex);
            lc.PropertyChanged += OnLevelColorChanged;
            LevelColors.Add(lc);
        }

        if (model.RootNode is not null)
            LoadNodeTree(model.RootNode, null);
    }

    public MindMapViewModel() : this(new MindMap()) { }

    private void LoadNodeTree(MindMapNode node, NodeViewModel? parentVm)
    {
        var vm = new NodeViewModel(node) { ParentViewModel = parentVm };
        AllNodes.Add(vm);
        parentVm?.Children.Add(vm);

        foreach (var child in node.Children)
            LoadNodeTree(child, vm);
    }

    public NodeViewModel AddRootNode()
    {
        var node = new MindMapNode { Text = "Idée centrale", IsRoot = true, X = 400, Y = 300 };
        Model.RootNode = node;
        var vm = new NodeViewModel(node);
        AllNodes.Add(vm);
        SelectedNode = vm;
        return vm;
    }

    public NodeViewModel? AddChildNode(NodeViewModel parent)
    {
        var node = new MindMapNode
        {
            Text = "Nouveau nœud",
            X = parent.X + 200,
            Y = parent.Y + (parent.Children.Count * 80)
        };
        var vm = new NodeViewModel(node);
        parent.AddChild(vm);  // sets vm.ParentViewModel = parent
        AllNodes.Add(vm);
        SelectedNode = vm;
        return vm;
    }

    public void DeleteNode(NodeViewModel node)
    {
        if (node.IsRoot)
        {
            RemoveNodeAndDescendants(node);
            Model.RootNode = null;
            SelectedNode = null;
            return;
        }

        var parentVm = AllNodes.FirstOrDefault(n => n.Children.Contains(node));
        if (parentVm is not null)
            parentVm.RemoveChild(node);

        RemoveNodeAndDescendants(node);
        SelectedNode = null;
    }

    private void RemoveNodeAndDescendants(NodeViewModel node)
    {
        foreach (var child in node.Children.ToList())
            RemoveNodeAndDescendants(child);
        AllNodes.Remove(node);
    }

    public IEnumerable<(NodeViewModel Source, NodeViewModel Target)> GetConnections()
    {
        foreach (var node in AllNodes)
        {
            if (node.IsCollapsed) continue;
            foreach (var child in node.Children)
                if (IsNodeVisible(child))
                    yield return (node, child);
        }
    }

    /// <summary>Retourne les nœuds qui doivent être dessinés (ancêtres non réduits).</summary>
    public IEnumerable<NodeViewModel> GetVisibleNodes()
    {
        foreach (var node in AllNodes)
            if (IsNodeVisible(node))
                yield return node;
    }

    private bool IsNodeVisible(NodeViewModel node)
    {
        var current = node;
        // Remonter jusqu'à la racine : si un ancêtre est réduit, le nœud est masqué
        while (true)
        {
            var parent = AllNodes.FirstOrDefault(n => n.Children.Contains(current));
            if (parent is null) return true;   // nœud racine, toujours visible
            if (parent.IsCollapsed) return false;
            current = parent;
        }
    }

    public void ToggleCollapseNode(NodeViewModel node)
    {
        node.ToggleCollapse();
    }

    public void ResetView()
    {
        Scale = 1.0;
        OffsetX = 0;
        OffsetY = 0;
    }

    public void ReplaceModel(MindMap newModel)
    {
        Model = newModel;
        AllNodes.Clear();
        SelectedNode = null;

        // Recharge les couleurs de niveau
        for (int i = 0; i < LevelColors.Count; i++)
        {
            var hex = newModel.LevelColors.TryGetValue(i, out var c) ? c : DefaultLevelColors[i];
            LevelColors[i].Color = hex;
        }

        if (newModel.RootNode is not null)
            LoadNodeTree(newModel.RootNode, null);
        OnPropertyChanged(nameof(Title));
    }
}
