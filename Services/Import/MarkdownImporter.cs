using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MindMapApp.Models;

namespace MindMapApp.Services.Import;

/// <summary>
/// Importe un fichier Markdown (.md) en carte mentale.
///
/// Règles de conversion :
///   # Titre      → nœud racine
///   ## Niveau 2  → enfant de racine
///   ### Niveau 3 → petit-enfant
///   - item       → enfant du dernier heading de niveau supérieur
///   * item       → idem
///
/// Le premier heading H1 devient le nœud racine.
/// S'il n'y a pas de H1, le nom de fichier est utilisé.
/// </summary>
public class MarkdownImporter : IMindMapImporter
{
    public string FormatName => "Markdown";
    public string[] FileExtensions => new[] { "*.md", "*.markdown" };
    public string[] MimeTypes => new[] { "text/markdown", "text/x-markdown" };

    public async Task<MindMap> ImportAsync(string filePath)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        var title = Path.GetFileNameWithoutExtension(filePath);

        MindMapNode? rootNode = null;
        // Stack[depth] = dernier nœud placé à ce niveau de heading (1-based)
        var stack = new Dictionary<int, MindMapNode>();

        double baseX = 400, baseY = 300;
        double currentY = baseY;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Heading Detection
            int headingLevel = 0;
            string nodeText = line;

            if (line.StartsWith("# ") && !line.StartsWith("## "))
            {
                headingLevel = 1;
                nodeText = line[2..].Trim();
            }
            else if (line.StartsWith("## ") && !line.StartsWith("### "))
            {
                headingLevel = 2;
                nodeText = line[3..].Trim();
            }
            else if (line.StartsWith("### ") && !line.StartsWith("#### "))
            {
                headingLevel = 3;
                nodeText = line[4..].Trim();
            }
            else if (line.StartsWith("#### "))
            {
                headingLevel = 4;
                nodeText = line[5..].Trim();
            }
            else if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ "))
            {
                headingLevel = -1; // list item
                nodeText = line[2..].Trim();
            }

            if (headingLevel == 0) continue; // ligne ordinaire ignorée

            if (headingLevel == 1)
            {
                // Racine
                title = nodeText;
                rootNode = new MindMapNode
                {
                    Text = nodeText,
                    IsRoot = true,
                    X = baseX,
                    Y = baseY
                };
                stack.Clear();
                stack[1] = rootNode;
                currentY = baseY + 80;
                continue;
            }

            if (rootNode is null)
            {
                // Pas de H1 → créer un nœud racine implicite
                rootNode = new MindMapNode
                {
                    Text = title,
                    IsRoot = true,
                    X = baseX,
                    Y = baseY
                };
                stack[1] = rootNode;
                currentY = baseY + 80;
            }

            int parentLevel;
            if (headingLevel == -1)
            {
                // Trouver le dernier heading pour rattacher le list item
                parentLevel = FindDeepestLevel(stack);
            }
            else
            {
                parentLevel = headingLevel - 1;
                if (parentLevel < 1) parentLevel = 1;
            }

            // S'assurer que le parent existe
            while (!stack.ContainsKey(parentLevel) && parentLevel > 1)
                parentLevel--;

            MindMapNode parent = stack.ContainsKey(parentLevel) ? stack[parentLevel] : rootNode;

            double nodeX = baseX + (headingLevel == -1 ? parentLevel * 220 : (headingLevel - 1) * 220);
            var node = new MindMapNode { Text = nodeText, X = nodeX, Y = currentY };
            parent.AddChild(node);
            currentY += 80;

            if (headingLevel > 0)
            {
                stack[headingLevel] = node;
                // Invalider les niveaux plus profonds
                for (int i = headingLevel + 1; i <= 10; i++)
                    stack.Remove(i);
            }
        }

        rootNode ??= new MindMapNode { Text = title, IsRoot = true, X = baseX, Y = baseY };

        return new MindMap { Title = title, RootNode = rootNode };
    }

    private static int FindDeepestLevel(Dictionary<int, MindMapNode> stack)
    {
        int max = 1;
        foreach (var k in stack.Keys)
            if (k > max) max = k;
        return max;
    }
}
