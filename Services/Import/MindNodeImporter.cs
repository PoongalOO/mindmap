using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml.Linq;
using MindMapApp.Models;

namespace MindMapApp.Services.Import;

/// <summary>
/// Importe les fichiers MindNode (.mindnode).
/// Un fichier .mindnode est un bundle macOS (dossier ou ZIP) contenant contents.xml.
/// Le schéma XML utilise &lt;nodes&gt;/&lt;node&gt; avec l'attribut "title".
/// </summary>
public class MindNodeImporter : IMindMapImporter
{
    public string FormatName => "MindNode";
    public string[] FileExtensions => new[] { "*.mindnode" };
    public string[] MimeTypes => new[] { "application/octet-stream" };

    public async Task<MindMap> ImportAsync(string filePath)
    {
        XDocument xml;

        // Cas 1 : fichier est un ZIP (bundle packagé)
        if (IsZipFile(filePath))
        {
            using var zip = ZipFile.OpenRead(filePath);
            var entry = zip.GetEntry("contents.xml")
                      ?? zip.GetEntry("Contents/contents.xml")
                      ?? FindXmlEntry(zip)
                      ?? throw new InvalidDataException("Fichier MindNode invalide : contents.xml introuvable.");

            await using var stream = entry.Open();
            xml = await XDocument.LoadAsync(stream, LoadOptions.None, default);
        }
        else if (Directory.Exists(filePath))
        {
            // Cas 2 : bundle macOS non empaqueté (dossier)
            var xmlPath = Path.Combine(filePath, "contents.xml");
            if (!File.Exists(xmlPath))
                throw new InvalidDataException($"Fichier MindNode invalide : {xmlPath} introuvable.");
            xml = XDocument.Load(xmlPath);
        }
        else
        {
            // Cas 3 : fichier XML direct
            xml = XDocument.Load(filePath);
        }

        return ParseMindNodeXml(xml, filePath);
    }

    private static MindMap ParseMindNodeXml(XDocument xml, string filePath)
    {
        var docEl = xml.Root;
        if (docEl is null)
            throw new InvalidDataException("Fichier MindNode XML invalide.");

        var title = Path.GetFileNameWithoutExtension(filePath);

        // Chercher le premier nœud racine dans <nodes> ou directement <node>
        var nodesEl = docEl.Element("nodes") ?? docEl;
        var firstNodeEl = nodesEl.Element("node");

        if (firstNodeEl is null)
            throw new InvalidDataException("Fichier MindNode XML : aucun nœud trouvé.");

        double x = 400, y = 300;
        var rootNode = ParseNode(firstNodeEl, ref x, ref y, isRoot: true);
        if (!string.IsNullOrWhiteSpace(rootNode.Text))
            title = rootNode.Text;

        return new MindMap { Title = title, RootNode = rootNode };
    }

    private static MindMapNode ParseNode(XElement el, ref double x, ref double y, bool isRoot)
    {
        var text = el.Attribute("title")?.Value
                ?? el.Element("title")?.Value
                ?? "Nœud";

        var node = new MindMapNode { Text = text, IsRoot = isRoot, X = x, Y = y };

        double childX = x + 220;
        double childY = y;

        var childrenEl = el.Element("nodes") ?? el.Element("children");
        if (childrenEl is not null)
        {
            foreach (var childEl in childrenEl.Elements("node"))
            {
                var child = ParseNode(childEl, ref childX, ref childY, isRoot: false);
                node.AddChild(child);
                childY += 80;
            }
        }

        return node;
    }

    private static bool IsZipFile(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> header = stackalloc byte[4];
            fs.Read(header);
            // ZIP magic bytes: PK\x03\x04
            return header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
        }
        catch
        {
            return false;
        }
    }

    private static ZipArchiveEntry? FindXmlEntry(ZipArchive zip)
    {
        foreach (var entry in zip.Entries)
            if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                return entry;
        return null;
    }
}
