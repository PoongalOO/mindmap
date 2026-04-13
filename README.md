# MindMapApp

Application de bureau multiplateforme pour créer, éditer et visualiser des cartes mentales (mind maps). Développée en C# / .NET 8 avec le framework Avalonia UI.

---

## Fonctionnalités

- **Création et édition** : nœud racine + arborescence enfants à profondeur illimitée
- **Canvas infini** : déplacement libre des nœuds par glisser-déposer
- **Navigation** :
  - Panoramique (pan) : clic droit / clic molette / espace + glisser
  - Zoom molette (0.2× – 5.0×), indicateur affiché en temps réel
- **Sélection et propriétés** : panneau latéral pour modifier le texte, la position et la couleur d'un nœud
- **Réduction/expansion** de sous-arbres (bouton inline sur le nœud ou barre d'outils)
- **Palette de couleurs par niveau** : 8 niveaux configurables (0 = racine → 7), surchargeables nœud par nœud
- **Titre de la carte** éditable directement dans la barre d'outils
- **Sauvegarde / chargement natif** au format JSON
- **Import** depuis 6 formats tiers (voir ci-dessous)
- **Barre de statut** avec retour contextuel, version et lien vers le projet

---

## Formats d'import supportés

| Format | Extension | Description |
|---|---|---|
| FreeMind / Freeplane | `.mm` | XML `<map>/<node>` |
| OPML | `.opml` | XML `<opml>/<body>/<outline>` |
| XMind 8+ | `.xmind` | Archive ZIP → `content.json` (ou `content.xml` en repli) |
| MindMeister | `.mind` | JSON `title/children` |
| Markdown | `.md`, `.markdown` | Niveaux de titres H1–H4 → profondeur ; listes rattachées au titre parent |
| MindNode | `.mindnode` | Bundle ZIP ou répertoire → `contents.xml` |

---

## Technologies

| Aspect | Détail |
|---|---|
| Langage | C# 12, nullable activé |
| Runtime | .NET 8.0 |
| UI | [Avalonia UI](https://avaloniaui.net/) 11.2.1 |
| Thème | `Avalonia.Themes.Fluent` 11.2.1 |
| Typographie | Inter (via `Avalonia.Fonts.Inter`) |
| Sérialisation | `System.Text.Json` (intégré .NET) |
| Architecture | MVVM (sans framework tiers) |

---

## Prérequis

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## Installation et lancement

```bash
# Cloner le dépôt
git clone <url-du-dépôt>
cd MindMapApp

# Restaurer les dépendances et compiler
dotnet build

# Lancer l'application
dotnet run
```

### Publication autonome (exemple Linux x64)

```bash
dotnet publish -c Release -r linux-x64 --self-contained true
```

L'exécutable est produit dans `bin/Release/net8.0/linux-x64/publish/`.

---

## Structure du projet

```
MindMapApp/
├── Models/                    # Données pures (aucun couplage UI)
│   ├── MindMap.cs             # Agrégat racine : Id, titre, dates, LevelColors
│   ├── MindMapNode.cs         # Nœud de l'arbre : texte, position, couleur, enfants
│   └── NodeConnection.cs      # Descripteur d'arête (SourceId → TargetId)
│
├── ViewModels/                # Couche présentation MVVM
│   ├── MainWindowViewModel.cs # Commandes globales (Nouveau, Ouvrir, Sauvegarder, Import…)
│   ├── MindMapViewModel.cs    # État du canvas : nœuds, sélection, zoom, pan, couleurs
│   ├── NodeViewModel.cs       # État d'un nœud : texte, position, sélection, profondeur
│   └── LevelColorViewModel.cs # Une entrée de la palette de 8 couleurs par niveau
│
├── Views/                     # UI déclarative (AXAML)
│   ├── MainWindow.axaml       # Barre d'outils, panneau propriétés, barre de statut
│   └── MindMapView.axaml      # Canvas + badge de zoom
│
├── Controls/
│   └── MindMapCanvas.cs       # Rendu custom via DrawingContext (Bézier, ombres, hit-test)
│
├── Services/
│   ├── MindMapPersistenceService.cs  # Sauvegarde/chargement JSON asynchrone
│   └── Import/                       # Un importer par format tiers
│
├── Converters/
│   └── HexToBrushConverter.cs # Chaîne "#RRGGBB" → SolidColorBrush
│
├── Helpers/
│   ├── ViewModelBase.cs       # INotifyPropertyChanged (SetField)
│   └── RelayCommand.cs        # ICommand léger (Action + Func<bool>)
│
└── Assets/
    └── Styles/
        ├── Base.axaml         # Palette sombre (Slate) + police
        └── Controls.axaml     # Styles nommés (ToolbarButton, PropertiesPanel…)
```

---

## Rendu du canvas

Le contrôle `MindMapCanvas` hérite directement de `Avalonia.Controls.Control` et effectue tout le rendu dans `Render(DrawingContext)` :

- **Connexions** : courbes de Bézier cubiques entre les centres des nœuds, colorées d'après le niveau source (opacité 70 %)
- **Nœuds** : rectangles arrondis (rayon 10 px) avec ombre portée
- **Sélection** : fond éclairci 30 % + bordure bleue 2 px (`#60A5FA`)
- **Bouton collapse** : cercle ±14 px sur le bord droit des nœuds parents
- **Cache de taille** : `_nodeSizeCache` évite de recalculer la largeur du texte à chaque frame

---

## Système de couleurs

Huit niveaux de profondeur, chacun avec une couleur d'accent par défaut :

| Niveau | Couleur par défaut |
|---|---|
| 0 (racine) | Indigo |
| 1 | Émeraude |
| 2 | Ambre |
| 3 | Rouge |
| 4 | Violet |
| 5 | Cyan |
| 6 | Citron vert |
| 7 | Orange |

Chaque nœud peut surcharger sa couleur via la propriété `CustomColor` (valeur hexadécimale). Les couleurs sont persistées dans `MindMap.LevelColors`.

---

## Format de sauvegarde natif (JSON)

```json
{
  "id": "...",
  "title": "Ma carte",
  "createdAt": "2026-01-01T00:00:00Z",
  "updatedAt": "2026-04-13T12:00:00Z",
  "levelColors": { "0": "#6366F1", "1": "#10B981" },
  "rootNode": {
    "id": "...",
    "text": "Nœud racine",
    "x": 400,
    "y": 300,
    "isRoot": true,
    "isCollapsed": false,
    "customColor": null,
    "children": [ ... ]
  }
}
```

> Les références circulaires (`Parent`) sont exclues avec `[JsonIgnore]` et restaurées au chargement.

---

## Licence

Ce projet est distribué sous licence **GNU General Public License v3.0** (GPLv3).  
Voir le fichier [LICENSE](LICENSE) pour le texte complet.

---

## Auteur

**Poongaloo** — sam@poongaloo.org
