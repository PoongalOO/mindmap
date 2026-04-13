using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using MindMapApp.Models;

namespace MindMapApp.Services.Import;

/// <summary>
/// Importe les fichiers XMind (.xmind).
/// Un fichier .xmind est un ZIP contenant soit content.json (XMind 8+)
/// soit content.xml (XMind ZEN/2020+).
/// Stratégie : JSON en priorité, XML en fallback.
/// </summary>
public class XMindImporter : IMindMapImporter
{
    public string FormatName => "XMind";
    public string[] FileExtensions => new[] { "*.xmind" };
    public string[] MimeTypes => new[] { "application/vnd.xmind.workbook" };

    public async Task<MindMap> ImportAsync(string filePath)
    {
        using var zip = ZipFile.OpenRead(filePath);

        // Tentative JSON (XMind 8+ / ZEN)
        var jsonEntry = zip.GetEntry("content.json");
        if (jsonEntry is not null)
        {
            await using var stream = jsonEntry.Open();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            return ParseXMindJson(json, filePath);
        }

        // Fallback XML (XMind 2013-2016)
        var xmlEntry = zip.GetEntry("content.xml");
        if (xmlEntry is not null)
        {
            await using var stream = xmlEntry.Open();
            var xml = await XDocument.LoadAsync(stream, LoadOptions.None, default);
            return ParseXMindXml(xml, filePath);
        }

        throw new InvalidDataException("Fichier XMind non reconnu : ni content.json ni content.xml trouvé.");
    }

    // ── JSON (XMind 8 / ZEN) ──────────────────────────────────────────────

    private static MindMap ParseXMindJson(string json, string filePath)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Structure : array de sheets → chaque sheet a "rootTopic"
        JsonElement sheet;
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            sheet = root[0];
        else
            throw new InvalidDataException("Format XMind JSON non reconnu.");

        var sheetTitle = sheet.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var title = string.IsNullOrWhiteSpace(sheetTitle)
                  ? Path.GetFileNameWithoutExtension(filePath)
                  : sheetTitle;

        var rootTopicEl = sheet.GetProperty("rootTopic");
        double x = 400, y = 300;
        var rootNode = ParseJsonTopic(rootTopicEl, ref x, ref y, isRoot: true);

        return new MindMap { Title = title, RootNode = rootNode };
    }

    private static MindMapNode ParseJsonTopic(JsonElement el, ref double x, ref double y, bool isRoot)
    {
        var text = el.TryGetProperty("title", out var titleProp)
                 ? titleProp.GetString() ?? "Nœud"
                 : "Nœud";

        var node = new MindMapNode { Text = text, IsRoot = isRoot, X = x, Y = y };

        double childX = x + 220;
        double childY = y;

        if (el.TryGetProperty("children", out var children)
            && children.TryGetProperty("attached", out var attached)
            && attached.ValueKind == JsonValueKind.Array)
        {
            foreach (var childEl in attached.EnumerateArray())
            {
                var child = ParseJsonTopic(childEl, ref childX, ref childY, isRoot: false);
                node.AddChild(child);
                childY += 80;
            }
        }

        return node;
    }

    // ── XML (XMind ancien format) ─────────────────────────────────────────

    private static MindMap ParseXMindXml(XDocument xml, string filePath)
    {
        XNamespace ns = "urn:xmind:xmap:xmlns:content:2.0";
        var sheetEl = xml.Descendants(ns + "sheet").FirstOrDefault()
                   ?? xml.Descendants("sheet").FirstOrDefault();

        if (sheetEl is null)
            throw new InvalidDataException("Format XMind XML non reconnu.");

        var titleEl = sheetEl.Element(ns + "title") ?? sheetEl.Element("title");
        var title = titleEl?.Value ?? Path.GetFileNameWithoutExtension(filePath);

        var topicEl = sheetEl.Element(ns + "topic") ?? sheetEl.Element("topic");
        if (topicEl is null)
            throw new InvalidDataException("Aucun topic racine dans le fichier XMind XML.");

        double x = 400, y = 300;
        var rootNode = ParseXmlTopic(topicEl, ns, ref x, ref y, isRoot: true);
        return new MindMap { Title = title, RootNode = rootNode };
    }

    private static MindMapNode ParseXmlTopic(XElement el, XNamespace ns,
                                              ref double x, ref double y, bool isRoot)
    {
        var titleEl = el.Element(ns + "title") ?? el.Element("title");
        var text = titleEl?.Value ?? "Nœud";

        var node = new MindMapNode { Text = text, IsRoot = isRoot, X = x, Y = y };

        var childrenEl = el.Element(ns + "children") ?? el.Element("children");
        if (childrenEl is null) return node;

        double childX = x + 220;
        double childY = y;

        foreach (var topicEl in childrenEl.Elements(ns + "topic").Concat(childrenEl.Elements("topic")))
        {
            var child = ParseXmlTopic(topicEl, ns, ref childX, ref childY, isRoot: false);
            node.AddChild(child);
            childY += 80;
        }
        return node;
    }
}
