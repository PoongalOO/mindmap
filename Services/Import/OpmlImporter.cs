using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using MindMapApp.Models;

namespace MindMapApp.Services.Import;

/// <summary>
/// Importe les fichiers OPML (.opml).
/// Structure : &lt;opml&gt;/&lt;body&gt;/&lt;outline&gt; imbriqués.
/// </summary>
public class OpmlImporter : IMindMapImporter
{
    public string FormatName => "OPML";
    public string[] FileExtensions => new[] { "*.opml" };
    public string[] MimeTypes => new[] { "text/x-opml", "application/xml" };

    public Task<MindMap> ImportAsync(string filePath)
    {
        var xml = XDocument.Load(filePath);
        var opmlEl = xml.Root ?? throw new InvalidDataException("Fichier OPML invalide.");

        var headEl = opmlEl.Element("head");
        var title = headEl?.Element("title")?.Value
                 ?? Path.GetFileNameWithoutExtension(filePath);

        var bodyEl = opmlEl.Element("body")
                  ?? throw new InvalidDataException("Fichier OPML invalide : élément <body> manquant.");

        // Créer un nœud racine synthétique à partir du titre, puis chaque outline de premier niveau = enfant
        var rootNode = new MindMapNode
        {
            Text = title,
            IsRoot = true,
            X = 400,
            Y = 300
        };

        double childX = 640;
        double childY = 100;

        foreach (var outlineEl in bodyEl.Elements("outline"))
        {
            var child = ParseOutline(outlineEl, ref childX, ref childY);
            rootNode.AddChild(child);
            childY += 80;
        }

        var map = new MindMap { Title = title, RootNode = rootNode };
        return Task.FromResult(map);
    }

    private static MindMapNode ParseOutline(XElement el, ref double x, ref double y)
    {
        var text = el.Attribute("text")?.Value
                ?? el.Attribute("title")?.Value
                ?? el.Attribute("_note")?.Value
                ?? "Nœud";

        var node = new MindMapNode { Text = text, X = x, Y = y };

        double childX = x + 220;
        double childY = y;

        foreach (var childEl in el.Elements("outline"))
        {
            var child = ParseOutline(childEl, ref childX, ref childY);
            node.AddChild(child);
            childY += 80;
        }

        return node;
    }
}
