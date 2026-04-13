using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MindMapApp.Models;

namespace MindMapApp.Services.Import;

/// <summary>
/// Service central d'import. Enregistre tous les importeurs disponibles
/// et sélectionne le bon selon l'extension du fichier.
/// </summary>
public class MindMapImportService
{
    private readonly IReadOnlyList<IMindMapImporter> _importers;

    public MindMapImportService()
    {
        _importers = new List<IMindMapImporter>
        {
            new FreeMindImporter(),
            new OpmlImporter(),
            new XMindImporter(),
            new MindMeisterImporter(),
            new MarkdownImporter(),
            new MindNodeImporter()
        };
    }

    /// <summary>Tous les importeurs disponibles (pour construire les filtres de dialogue fichier).</summary>
    public IReadOnlyList<IMindMapImporter> Importers => _importers;

    /// <summary>
    /// Importe le fichier donné.
    /// Lance <see cref="NotSupportedException"/> si aucun importeur ne supporte l'extension.
    /// </summary>
    public async Task<MindMap> ImportAsync(string filePath)
    {
        var ext = "*" + Path.GetExtension(filePath).ToLowerInvariant();

        var importer = _importers.FirstOrDefault(i =>
            i.FileExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase));

        if (importer is null)
            throw new NotSupportedException(
                $"Aucun importeur disponible pour l'extension « {ext} ».");

        return await importer.ImportAsync(filePath);
    }
}
