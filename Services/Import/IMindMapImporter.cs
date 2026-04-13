using System.Threading.Tasks;
using MindMapApp.Models;

namespace MindMapApp.Services.Import;

/// <summary>
/// Contrat commun pour tous les importeurs de cartes mentales.
/// Chaque format implémente cette interface de façon indépendante.
/// </summary>
public interface IMindMapImporter
{
    /// <summary>Nom lisible du format (ex. "FreeMind").</summary>
    string FormatName { get; }

    /// <summary>Extensions supportées (ex. ["*.mm"]).</summary>
    string[] FileExtensions { get; }

    /// <summary>Type MIME associé (pour le FilePickerFileType).</summary>
    string[] MimeTypes { get; }

    /// <summary>Importe un fichier et retourne un MindMap. Lance une exception si le format est invalide.</summary>
    Task<MindMap> ImportAsync(string filePath);
}
