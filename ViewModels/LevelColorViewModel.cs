using MindMapApp.Helpers;

namespace MindMapApp.ViewModels;

/// <summary>
/// Représente la couleur associée à un niveau de nœud (profondeur dans l'arbre).
/// </summary>
public class LevelColorViewModel : ViewModelBase
{
    private string _color;

    public int Level { get; }

    public string Label => Level == 0 ? "Racine" : $"Niveau {Level}";

    /// <summary>Couleur hex (ex: #6366F1). Modifiable par l'utilisateur.</summary>
    public string Color
    {
        get => _color;
        set => SetField(ref _color, value);
    }

    public LevelColorViewModel(int level, string color)
    {
        Level = level;
        _color = color;
    }
}
