using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using MindMapApp.Helpers;
using MindMapApp.Models;
using MindMapApp.Services;
using MindMapApp.Services.Import;

namespace MindMapApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IMindMapPersistenceService _persistenceService;
    private readonly MindMapImportService _importService;
    private string? _currentFilePath;
    private string _statusMessage = "Prêt";

    public MindMapViewModel MindMap { get; } = new();

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand AddRootNodeCommand { get; }
    public ICommand AddChildNodeCommand { get; }
    public ICommand DeleteNodeCommand { get; }
    public ICommand ResetViewCommand { get; }
    public ICommand ToggleCollapseCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand OpenCommand { get; }

    // Délégués pour les dialogues (injectés par la View, pattern MVVM-friendly)
    public Func<Task<string?>>? RequestOpenFileDialog { get; set; }
    public Func<Task<string?>>? RequestSaveFileDialog { get; set; }
    public Func<Task<string?>>? RequestImportFileDialog { get; set; }

    public MainWindowViewModel(IMindMapPersistenceService persistenceService)
    {
        _persistenceService = persistenceService;
        _importService = new MindMapImportService();

        NewCommand = new RelayCommand(ExecuteNew);
        OpenCommand = new RelayCommand(async () => await ExecuteOpenAsync());
        SaveCommand = new RelayCommand(async () => await ExecuteSaveAsync());
        AddRootNodeCommand = new RelayCommand(ExecuteAddRootNode, () => MindMap.AllNodes.Count == 0);
        AddChildNodeCommand = new RelayCommand(ExecuteAddChildNode, () => MindMap.SelectedNode is not null);
        DeleteNodeCommand = new RelayCommand(ExecuteDeleteNode, () => MindMap.SelectedNode is not null);
        ResetViewCommand = new RelayCommand(MindMap.ResetView);
        ToggleCollapseCommand = new RelayCommand(ExecuteToggleCollapse, () => MindMap.SelectedNode?.HasChildren == true);
        ImportCommand = new RelayCommand(async () => await ExecuteImportAsync());

        MindMap.AllNodes.CollectionChanged += (_, _) => RaiseCanExecuteChanged();
        MindMap.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MindMapViewModel.SelectedNode))
                RaiseCanExecuteChanged();
        };
    }

    private void ExecuteNew()
    {
        MindMap.ReplaceModel(new MindMap());
        _currentFilePath = null;
        StatusMessage = "Nouvelle carte créée.";
    }

    private async Task ExecuteOpenAsync()
    {
        if (RequestOpenFileDialog is null) return;
        var path = await RequestOpenFileDialog();
        if (path is null) return;

        var loaded = await _persistenceService.LoadAsync(path);
        if (loaded is null)
        {
            StatusMessage = "Impossible de charger le fichier.";
            return;
        }

        MindMap.ReplaceModel(loaded);
        _currentFilePath = path;
        StatusMessage = $"Carte chargée : {System.IO.Path.GetFileName(path)}";
    }

    private async Task ExecuteSaveAsync()
    {
        if (_currentFilePath is null)
        {
            if (RequestSaveFileDialog is null) return;
            var path = await RequestSaveFileDialog();
            if (path is null) return;
            _currentFilePath = path;
        }

        await _persistenceService.SaveAsync(MindMap.Model, _currentFilePath);
        StatusMessage = $"Enregistré : {System.IO.Path.GetFileName(_currentFilePath)}";
    }

    private async Task ExecuteImportAsync()
    {
        if (RequestImportFileDialog is null) return;
        var path = await RequestImportFileDialog();
        if (path is null) return;

        try
        {
            var imported = await _importService.ImportAsync(path);
            MindMap.ReplaceModel(imported);
            _currentFilePath = null;
            StatusMessage = $"Importé : {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur d'import : {ex.Message}";
        }
    }

    private void ExecuteAddRootNode()
    {
        MindMap.AddRootNode();
        StatusMessage = "Nœud racine ajouté.";
        RaiseCanExecuteChanged();
    }

    private void ExecuteAddChildNode()
    {
        if (MindMap.SelectedNode is null) return;
        MindMap.AddChildNode(MindMap.SelectedNode);
        StatusMessage = "Nœud enfant ajouté.";
    }

    private void ExecuteDeleteNode()
    {
        if (MindMap.SelectedNode is null) return;
        MindMap.DeleteNode(MindMap.SelectedNode);
        StatusMessage = "Nœud supprimé.";
        RaiseCanExecuteChanged();
    }

    private void ExecuteToggleCollapse()
    {
        if (MindMap.SelectedNode is null) return;
        MindMap.ToggleCollapseNode(MindMap.SelectedNode);
        StatusMessage = MindMap.SelectedNode.IsCollapsed ? "Nœud réduit." : "Nœud développé.";
    }

    private void RaiseCanExecuteChanged()
    {
        (AddRootNodeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddChildNodeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteNodeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleCollapseCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
