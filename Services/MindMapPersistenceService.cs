using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MindMapApp.Models;

namespace MindMapApp.Services;

public interface IMindMapPersistenceService
{
    Task<MindMap?> LoadAsync(string filePath);
    Task SaveAsync(MindMap mindMap, string filePath);
}

public class MindMapPersistenceService : IMindMapPersistenceService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public async Task<MindMap?> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        await using var stream = File.OpenRead(filePath);
        var mindMap = await JsonSerializer.DeserializeAsync<MindMap>(stream, SerializerOptions);

        if (mindMap?.RootNode is not null)
            RestoreParentReferences(mindMap.RootNode, null);

        return mindMap;
    }

    public async Task SaveAsync(MindMap mindMap, string filePath)
    {
        mindMap.UpdatedAt = DateTime.UtcNow;
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, mindMap, SerializerOptions);
    }

    private static void RestoreParentReferences(MindMapNode node, MindMapNode? parent)
    {
        node.Parent = parent;
        foreach (var child in node.Children)
            RestoreParentReferences(child, node);
    }
}
