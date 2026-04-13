using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MindMapApp.Models;

public class MindMapNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "Nœud";
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsRoot { get; set; }
    public bool IsCollapsed { get; set; }
    /// <summary>Couleur personnalisée (hex, ex: #FF5733). Null = hérite la couleur du niveau.</summary>
    public string? CustomColor { get; set; }
    public List<MindMapNode> Children { get; set; } = new();

    [JsonIgnore]
    public MindMapNode? Parent { get; set; }

    public void AddChild(MindMapNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public void RemoveChild(MindMapNode child)
    {
        child.Parent = null;
        Children.Remove(child);
    }
}
