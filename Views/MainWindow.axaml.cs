using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MindMapApp.Services.Import;
using MindMapApp.ViewModels;

namespace MindMapApp.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType MindMapFileType = new("Carte mentale")
    {
        Patterns = new[] { "*.mindmap" },
        MimeTypes = new[] { "application/json" }
    };

    // Filtres d'import construits dynamiquement depuis les importeurs enregistrés
    private static IReadOnlyList<FilePickerFileType> BuildImportFilters()
    {
        var service = new MindMapImportService();
        var filters = new List<FilePickerFileType>();

        foreach (var importer in service.Importers)
        {
            filters.Add(new FilePickerFileType(importer.FormatName)
            {
                Patterns = importer.FileExtensions,
                MimeTypes = importer.MimeTypes
            });
        }

        // Filtre "Tous les formats supportés"
        filters.Insert(0, new FilePickerFileType("Tous les formats supportés")
        {
            Patterns = service.Importers.SelectMany(i => i.FileExtensions).ToArray()
        });

        return filters;
    }

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.RequestOpenFileDialog = OpenFileDialogAsync;
            vm.RequestSaveFileDialog = SaveFileDialogAsync;
            vm.RequestImportFileDialog = ImportFileDialogAsync;
        }
    }

    private async Task<string?> OpenFileDialogAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Ouvrir une carte mentale",
            FileTypeFilter = new[] { MindMapFileType },
            AllowMultiple = false
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private async Task<string?> SaveFileDialogAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Enregistrer la carte mentale",
            FileTypeChoices = new[] { MindMapFileType },
            DefaultExtension = "mindmap",
            ShowOverwritePrompt = true
        });

        return file?.TryGetLocalPath();
    }

    private async Task<string?> ImportFileDialogAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importer une carte mentale",
            FileTypeFilter = BuildImportFilters(),
            AllowMultiple = false
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private void OnSiteLinkClick(object? sender, RoutedEventArgs e)
        => OpenUrl("https://webinfo-concept.fr/mindmap");

    /// <summary>
    /// Ouvre une URL dans le navigateur par défaut.
    /// Tente d'abord UseShellExecute, puis des navigateurs courants sur Linux.
    /// </summary>
    private static void OpenUrl(string url)
    {
        // Windows / macOS
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return;
        }

        // Linux : essaie xdg-open puis les navigateurs courants
        foreach (var cmd in new[] { "xdg-open", "sensible-browser", "x-www-browser",
                                    "firefox", "chromium", "chromium-browser",
                                    "google-chrome", "brave-browser" })
        {
            try
            {
                Process.Start(new ProcessStartInfo(cmd, url)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true
                });
                return;
            }
            catch { /* tente le suivant */ }
        }
    }
}