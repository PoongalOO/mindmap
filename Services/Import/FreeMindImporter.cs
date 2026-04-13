using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using MindMapApp.Models;

namespace MindMapApp.Services.Import;

/// <summary>
/// Importe les fichiers FreeMind et Freeplane (.mm).
/// Les deux applications partagent le même schéma XML racine &lt;map&gt;/&lt;node&gt;.
/// </summary>
public class FreeMindImporter : IMindMapImporter
{
    public string FormatName => "FreeMind / Freeplane";
    public string[] FileExtensions => new[] { "*.mm" };
    public string[] MimeTypes => new[] { "application/x-freemind", "application/x-freeplane" };

    public Task<MindMap> ImportAsync(string filePath)
    {
        var xml = XDocument.Load(filePath);
        var mapEl = xml.Root ?? throw new InvalidDataException("Fichier .mm invalide : élément <map> manquant.");

        var title = Path.GetFileNameWithoutExtension(filePath);
        var rootEl = mapEl.Element("node")
                   ?? throw new InvalidDataException("Fichier .mm invalide : aucun nœud racine.");

        double x = 400, y = 300;
        var rootNode = ParseNode(rootEl, ref x, ref y, isRoot: true, depth: 0);

        var map = new MindMap
        {
            Title = title,
            RootNode = rootNode
        };

        return Task.FromResult(map);
    }

    private static MindMapNode ParseNode(XElement el, ref double x, ref double y,
                                         bool isRoot, int depth)
    {
        var text = el.Attribute("TEXT")?.Value
                ?? el.Attribute("text")?.Value
                ?? "Nœud";

        var node = new MindMapNode
        {
            Text = text,
            IsRoot = isRoot,
            X = x,
            Y = y,
            IsCollapsed = string.Equals(el.Attribute("FOLDED")?.Value, "true",
                                        StringComparison.OrdinalIgnoreCase)
        };

        double childX = x + 220;
        double childY = y;

        foreach (var childEl in el.Elements("node"))
        {
            var child = ParseNode(childEl, ref childX, ref childY,
                                  isRoot: false, depth: depth + 1);
            node.AddChild(child);
            childY += 80;
        }

        return node;
    }
}
