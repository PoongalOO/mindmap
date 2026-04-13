using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MindMapApp.Models;

namespace MindMapApp.Services.Import;

/// <summary>
/// Importe les exports MindMeister (.mind).
/// Le format est du JSON avec une structure "id"/"title"/"children".
/// MindMeister n'a pas de spécification publique : cette implémentation couvre
/// la structure d'export la plus courante observée dans leurs fichiers.
/// </summary>
public class MindMeisterImporter : IMindMapImporter
{
    public string FormatName => "MindMeister";
    public string[] FileExtensions => new[] { "*.mind" };
    public string[] MimeTypes => new[] { "application/json" };

    public async Task<MindMap> ImportAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        // Le fichier peut être { "map": { ... } } ou directement { "id": ..., "title": ... }
        JsonElement mapEl;
        if (root.TryGetProperty("map", out var inner))
            mapEl = inner;
        else
            mapEl = root;

        var title = mapEl.TryGetProperty("title", out var titleProp)
                  ? titleProp.GetString() ?? Path.GetFileNameWithoutExtension(filePath)
                  : Path.GetFileNameWithoutExtension(filePath);

        double x = 400, y = 300;
        var rootNode = ParseTopic(mapEl, ref x, ref y, isRoot: true);

        return new MindMap { Title = title, RootNode = rootNode };
    }

    private static MindMapNode ParseTopic(JsonElement el, ref double x, ref double y, bool isRoot)
    {
        var text = el.TryGetProperty("title", out var tp) ? tp.GetString() ?? "Nœud"
                 : el.TryGetProperty("name", out var np) ? np.GetString() ?? "Nœud"
                 : "Nœud";

        var node = new MindMapNode { Text = text, IsRoot = isRoot, X = x, Y = y };

        double childX = x + 220;
        double childY = y;

        // Les enfants peuvent être sous "children", "ideas" ou "nodes"
        JsonElement childrenProp = default;
        bool found = el.TryGetProperty("children", out childrenProp)
                  || el.TryGetProperty("ideas", out childrenProp)
                  || el.TryGetProperty("nodes", out childrenProp);

        if (found)
        {
            if (childrenProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var childEl in childrenProp.EnumerateArray())
                {
                    var child = ParseTopic(childEl, ref childX, ref childY, isRoot: false);
                    node.AddChild(child);
                    childY += 80;
                }
            }
            else if (childrenProp.ValueKind == JsonValueKind.Object)
            {
                // Format "ideas" : objet dont les valeurs sont les topics enfants
                foreach (var prop in childrenProp.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                    var child = ParseTopic(prop.Value, ref childX, ref childY, isRoot: false);
                    node.AddChild(child);
                    childY += 80;
                }
            }
        }

        return node;
    }
}
