using System;

namespace MindMapApp.Models;

/// <summary>
/// Représente un lien visuel entre deux nœuds.
/// Utilisé pour le rendu dans le canvas — dérivé dynamiquement depuis la hiérarchie parent/enfant.
/// </summary>
public class NodeConnection
{
    public Guid SourceId { get; set; }
    public Guid TargetId { get; set; }

    public NodeConnection(Guid sourceId, Guid targetId)
    {
        SourceId = sourceId;
        TargetId = targetId;
    }
}
