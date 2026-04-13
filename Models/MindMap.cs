using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MindMapApp.Models;

public class MindMap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Nouvelle carte mentale";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public MindMapNode? RootNode { get; set; }
    /// <summary>Couleurs par profondeur de niveau (clé = niveau, valeur = hex). Persistées avec la carte.</summary>
    public Dictionary<int, string> LevelColors { get; set; } = new();
}
