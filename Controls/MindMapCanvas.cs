using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MindMapApp.ViewModels;

namespace MindMapApp.Controls;

/// <summary>
/// Canvas personnalisé pour le rendu des nœuds de carte mentale et de leurs connexions.
/// Gère le pan (déplacement), le zoom, la sélection et le drag-and-drop des nœuds.
/// </summary>
public class MindMapCanvas : Control
{
    // ──────────────────────────── Styled Properties ────────────────────────────

    public static readonly StyledProperty<MindMapViewModel?> MindMapProperty =
        AvaloniaProperty.Register<MindMapCanvas, MindMapViewModel?>(nameof(MindMap));

    public MindMapViewModel? MindMap
    {
        get => GetValue(MindMapProperty);
        set => SetValue(MindMapProperty, value);
    }

    // ──────────────────────────── Node sizing ──────────────────────────────────

    private const double NodePaddingH = 20;   // padding gauche + droite
    private const double NodePaddingV = 14;   // padding haut + bas
    private const double NodeMinWidth  = 100;
    private const double NodeMinHeight = 36;
    private const double NodeRadius    = 10;
    private const double FontSize      = 13;
    private static readonly Typeface NodeTypeface = new("Inter,Segoe UI,sans-serif");

    /// <summary>Cache des tailles calculées pour chaque nœud (clé = Id).</summary>
    private readonly Dictionary<Guid, Size> _nodeSizeCache = new();

    private Size MeasureNode(NodeViewModel node)
    {
        if (_nodeSizeCache.TryGetValue(node.Id, out var cached))
            return cached;

        var ft = new FormattedText(
            string.IsNullOrEmpty(node.Text) ? " " : node.Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            NodeTypeface,
            FontSize,
            Brushes.White);

        // Largeur = texte + padding ; bouton collapse réservé à droite si le nœud a des enfants
        double collapseExtra = node.HasChildren ? CollapseButtonSize / 2 + 4 : 0;
        double w = Math.Max(NodeMinWidth, ft.Width + NodePaddingH + collapseExtra);
        double h = Math.Max(NodeMinHeight, ft.Height + NodePaddingV);
        var size = new Size(w, h);
        _nodeSizeCache[node.Id] = size;
        return size;
    }

    private Rect NodeRect(NodeViewModel node)
    {
        var sz = MeasureNode(node);
        return new Rect(node.X, node.Y, sz.Width, sz.Height);
    }

    private void InvalidateSizeCache(Guid id) => _nodeSizeCache.Remove(id);
    private void InvalidateSizeCache(NodeViewModel node) => InvalidateSizeCache(node.Id);

    // ──────────────────────────── Constants ────────────────────────────────────

    /// <summary>Distance minimale (px écran) avant qu'un glisser de nœud soit confirmé.</summary>
    private const double DragNodeThreshold  = 4.0;
    /// <summary>Distance minimale (px écran) pour qu'un glisser soit considéré comme un pan et non un clic.</summary>
    private const double PanClickThreshold = 5.0;

    // ──────────────────────────── State ────────────────────────────────────────

    private Point _panStart;
    private bool _isPanning;
    private double _panStartOffsetX;
    private double _panStartOffsetY;

    /// <summary>Pan déclenché par le bouton gauche (fond vide ou espace tenu).
    /// Distingué du pan clic-droit pour gérer la désélection différée.</summary>
    private bool _panFromLeft;

    /// <summary>Distance parcourue depuis le début du pan gauche (pour distinguer clic de glisser).</summary>
    private double _panLeftDistance;

    /// <summary>Barre espace maintenue → mode pan forcé.</summary>
    private bool _spaceHeld;

    private NodeViewModel? _draggingNode;
    private Point _dragNodeStart;
    private double _dragNodeOriginX;
    private double _dragNodeOriginY;
    /// <summary>Nœud sous le curseur au PointerPressed, en attente de confirmer le drag.</summary>
    private NodeViewModel? _pendingDragNode;
    private bool _dragConfirmed;

    // ──────────────────────────── Constructor ──────────────────────────────────

    static MindMapCanvas()
    {
        MindMapProperty.Changed.AddClassHandler<MindMapCanvas>((c, e) => c.OnMindMapChanged(e));
        AffectsRender<MindMapCanvas>(MindMapProperty);
    }

    public MindMapCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    // ──────────────────────────── ViewModel binding ────────────────────────────

    private void OnMindMapChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is MindMapViewModel oldVm)
        {
            oldVm.AllNodes.CollectionChanged -= OnNodesChanged;
            oldVm.PropertyChanged -= OnVmPropertyChanged;
            oldVm.LevelColors.CollectionChanged -= OnLevelColorsChanged;
            foreach (var lc in oldVm.LevelColors)
                lc.PropertyChanged -= OnLevelColorPropertyChanged;
            foreach (var node in oldVm.AllNodes)
                node.PropertyChanged -= OnNodePropertyChanged;
        }

        if (e.NewValue is MindMapViewModel newVm)
        {
            newVm.AllNodes.CollectionChanged += OnNodesChanged;
            newVm.PropertyChanged += OnVmPropertyChanged;
            newVm.LevelColors.CollectionChanged += OnLevelColorsChanged;
            foreach (var lc in newVm.LevelColors)
                lc.PropertyChanged += OnLevelColorPropertyChanged;
            foreach (var node in newVm.AllNodes)
                node.PropertyChanged += OnNodePropertyChanged;
        }

        InvalidateVisual();
    }

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (NodeViewModel n in e.NewItems)
                n.PropertyChanged += OnNodePropertyChanged;

        if (e.OldItems is not null)
            foreach (NodeViewModel n in e.OldItems)
                n.PropertyChanged -= OnNodePropertyChanged;

        InvalidateVisual();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MindMapViewModel.Scale)
                            or nameof(MindMapViewModel.OffsetX)
                            or nameof(MindMapViewModel.OffsetY)
                            or nameof(MindMapViewModel.SelectedNode))
            InvalidateVisual();
    }

    private void OnLevelColorsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (LevelColorViewModel lc in e.NewItems)
                lc.PropertyChanged += OnLevelColorPropertyChanged;
        if (e.OldItems is not null)
            foreach (LevelColorViewModel lc in e.OldItems)
                lc.PropertyChanged -= OnLevelColorPropertyChanged;
        InvalidateVisual();
    }

    private void OnLevelColorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => InvalidateVisual();

    private void OnNodePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeViewModel.Text) && sender is NodeViewModel n)
            InvalidateSizeCache(n);

        if (e.PropertyName is nameof(NodeViewModel.X)
                            or nameof(NodeViewModel.Y)
                            or nameof(NodeViewModel.Text)
                            or nameof(NodeViewModel.IsSelected)
                            or nameof(NodeViewModel.IsCollapsed)
                            or nameof(NodeViewModel.CustomColor))
            InvalidateVisual();
    }

    // ──────────────────────────── Render ───────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Fond opaque (requis pour le hit-testing sur toute la surface)
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0F172A")), new Rect(Bounds.Size));

        var vm = MindMap;
        if (vm is null) return;

        using var transformPush = context.PushTransform(
            Matrix.CreateScale(vm.Scale, vm.Scale) *
            Matrix.CreateTranslation(vm.OffsetX, vm.OffsetY));

        DrawConnections(context, vm);
        DrawNodes(context, vm);
    }

    private void DrawConnections(DrawingContext context, MindMapViewModel vm)
    {
        foreach (var (source, target) in vm.GetConnections())
        {
            // Ligne colorée d'après la couleur du nœud source (légèrement transparent)
            var baseColor = TryParseColor(vm.GetNodeFillColor(source), Color.Parse("#94A3B8"));
            var lineColor = Color.FromArgb(180, baseColor.R, baseColor.G, baseColor.B);
            var pen = new Pen(new SolidColorBrush(lineColor), 2);

            var sr = NodeRect(source);
            var tr = NodeRect(target);
            var sx = sr.X + sr.Width / 2;
            var sy = sr.Y + sr.Height / 2;
            var tx = tr.X + tr.Width / 2;
            var ty = tr.Y + tr.Height / 2;

            var geo = new StreamGeometry();
            using (var sgc = geo.Open())
            {
                sgc.BeginFigure(new Point(sx, sy), false);
                var cx1 = sx + (tx - sx) * 0.5;
                var cx2 = tx - (tx - sx) * 0.5;
                sgc.CubicBezierTo(
                    new Point(cx1, sy),
                    new Point(cx2, ty),
                    new Point(tx, ty));
            }
            context.DrawGeometry(null, pen, geo);
        }
    }

    private void DrawNodes(DrawingContext context, MindMapViewModel vm)
    {
        foreach (var node in vm.GetVisibleNodes())
        {
            var rect = NodeRect(node);

            // Ombre douce
            context.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                null,
                rect.Inflate(2).Translate(new Vector(2, 3)),
                NodeRadius, NodeRadius);

            // Couleur effective (personnalisée > niveau) avec éclaircissement si sélectionné
            var baseColor = TryParseColor(vm.GetNodeFillColor(node), Color.Parse("#1E293B"));
            Color fillColor = node.IsSelected ? LightenColor(baseColor, 0.30f) : baseColor;

            Pen borderPen = node.IsSelected
                ? new Pen(new SolidColorBrush(Color.Parse("#60A5FA")), 2)
                : new Pen(new SolidColorBrush(Color.Parse("#334155")), 1);

            context.DrawRectangle(new SolidColorBrush(fillColor), borderPen, rect, NodeRadius, NodeRadius);

            // Texte centré
            var ft = new FormattedText(
                node.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                NodeTypeface,
                FontSize,
                Brushes.White);

            var textX = rect.X + (rect.Width - ft.Width) / 2;
            var textY = rect.Y + (rect.Height - ft.Height) / 2;
            context.DrawText(ft, new Point(textX, textY));

            if (node.HasChildren)
                DrawCollapseButton(context, node, rect);
        }
    }

    // ──────────────── Helpers couleur ────────────────

    private static Color TryParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return Color.Parse(hex); }
        catch { return fallback; }
    }

    /// <summary>Mélange la couleur avec le blanc d'un facteur <paramref name="amount"/> (0–1).</summary>
    private static Color LightenColor(Color c, float amount)
    {
        return Color.FromArgb(
            c.A,
            (byte)Math.Min(255, c.R + (255 - c.R) * amount),
            (byte)Math.Min(255, c.G + (255 - c.G) * amount),
            (byte)Math.Min(255, c.B + (255 - c.B) * amount));
    }

    private const double CollapseButtonSize   = 14;
    private const double CollapseButtonRadius = 7;

    private static void DrawCollapseButton(DrawingContext context, NodeViewModel node, Rect nodeRect)
    {
        double bx = nodeRect.Right - CollapseButtonSize / 2;
        double by = nodeRect.Y + nodeRect.Height / 2 - CollapseButtonSize / 2;
        var btnRect = new Rect(bx, by, CollapseButtonSize, CollapseButtonSize);

        context.DrawRectangle(
            new SolidColorBrush(Color.Parse("#334155")),
            new Pen(new SolidColorBrush(Color.Parse("#64748B")), 1),
            btnRect,
            CollapseButtonRadius, CollapseButtonRadius);

        var symPen = new Pen(new SolidColorBrush(Color.Parse("#CBD5E1")), 1.5);
        double cx = bx + CollapseButtonSize / 2;
        double cy = by + CollapseButtonSize / 2;
        context.DrawLine(symPen, new Point(cx - 3, cy), new Point(cx + 3, cy));
        if (node.IsCollapsed)
            context.DrawLine(symPen, new Point(cx, cy - 3), new Point(cx, cy + 3));
    }

    // ──────────────────────────── Hit testing ──────────────────────────────────

    private NodeViewModel? HitTest(Point canvasPoint)
    {
        if (MindMap is null) return null;
        var visible = MindMap.GetVisibleNodes().ToHashSet();
        for (int i = MindMap.AllNodes.Count - 1; i >= 0; i--)
        {
            var node = MindMap.AllNodes[i];
            if (!visible.Contains(node)) continue;
            if (NodeRect(node).Contains(canvasPoint))
                return node;
        }
        return null;
    }

    /// <summary>Retourne le nœud dont le bouton collapse contient le point donné.</summary>
    private NodeViewModel? HitTestCollapseButton(Point canvasPoint)
    {
        if (MindMap is null) return null;
        foreach (var node in MindMap.GetVisibleNodes())
        {
            if (!node.HasChildren) continue;
            var r = NodeRect(node);
            double bx = r.Right - CollapseButtonSize / 2;
            double by = r.Y + r.Height / 2 - CollapseButtonSize / 2;
            if (new Rect(bx, by, CollapseButtonSize, CollapseButtonSize).Contains(canvasPoint))
                return node;
        }
        return null;
    }

    private Point ToCanvasPoint(Point screenPoint)
    {
        if (MindMap is null) return screenPoint;
        return new Point(
            (screenPoint.X - MindMap.OffsetX) / MindMap.Scale,
            (screenPoint.Y - MindMap.OffsetY) / MindMap.Scale);
    }

    // ──────────────────────────── Mouse interaction ─────────────────────────────

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var pos = e.GetPosition(this);
        var canvasPos = ToCanvasPoint(pos);
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsLeftButtonPressed)
        {
            // Priorité : clic sur le bouton collapse
            var collapseTarget = HitTestCollapseButton(canvasPos);
            if (collapseTarget is not null)
            {
                MindMap?.ToggleCollapseNode(collapseTarget);
                e.Handled = true;
                return;
            }

            var hit = HitTest(canvasPos);

            // Mode pan : espace tenu OU clic sur fond vide
            if (_spaceHeld || hit is null)
            {
                BeginLeftPan(pos);
                e.Pointer.Capture(this);
            }
            else
            {
                // Réinitialise explicitement l'état pan au cas où il serait resté actif
                _isPanning    = false;
                _panFromLeft  = false;
                _panLeftDistance = 0;

                // Sélection immédiate ; le drag n'est confirmé qu'après le seuil
                if (MindMap is not null)
                    MindMap.SelectedNode = hit;

                _pendingDragNode  = hit;
                _dragConfirmed    = false;
                _dragNodeStart    = pos;
                _dragNodeOriginX  = hit.X;
                _dragNodeOriginY  = hit.Y;
                e.Pointer.Capture(this);
            }
        }
        else if (props.IsRightButtonPressed || props.IsMiddleButtonPressed)
        {
            BeginPan(pos);
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        // Drag de nœud : confirmer après le seuil
        if (_pendingDragNode is not null && MindMap is not null)
        {
            var d = pos - _dragNodeStart;
            var dist = Math.Sqrt(d.X * d.X + d.Y * d.Y);
            if (!_dragConfirmed && dist >= DragNodeThreshold)
            {
                _dragConfirmed = true;
                _draggingNode  = _pendingDragNode;
            }
            if (_dragConfirmed)
            {
                _draggingNode!.X = _dragNodeOriginX + d.X / MindMap.Scale;
                _draggingNode!.Y = _dragNodeOriginY + d.Y / MindMap.Scale;
            }
        }
        else if (_isPanning && MindMap is not null)
        {
            var delta = pos - _panStart;
            if (_panFromLeft)
                _panLeftDistance = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            MindMap.OffsetX = _panStartOffsetX + delta.X;
            MindMap.OffsetY = _panStartOffsetY + delta.Y;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        ResetInteractionState(e.Pointer);

        // Pan gauche sans déplacement significatif = clic sur fond → désélectionner
        if (_panFromLeft && _panLeftDistance < PanClickThreshold && MindMap is not null)
            MindMap.SelectedNode = null;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        ResetInteractionState(null);
    }

    private void ResetInteractionState(IPointer? pointer)
    {
        _draggingNode    = null;
        _pendingDragNode = null;
        _dragConfirmed   = false;
        _isPanning       = false;
        _panFromLeft     = false;
        _panLeftDistance = 0;
        UpdateCursor();
        pointer?.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space && !_spaceHeld)
        {
            _spaceHeld = true;
            UpdateCursor();
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
        {
            _spaceHeld = false;
            UpdateCursor();
            e.Handled = true;
        }
    }

    // ──────────────────────────── Pan helpers ──────────────────────────────────

    private void BeginPan(Point screenPos)
    {
        _isPanning = true;
        _panFromLeft = false;
        _panStart = screenPos;
        _panStartOffsetX = MindMap?.OffsetX ?? 0;
        _panStartOffsetY = MindMap?.OffsetY ?? 0;
        Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    private void BeginLeftPan(Point screenPos)
    {
        _isPanning = true;
        _panFromLeft = true;
        _panLeftDistance = 0;
        _panStart = screenPos;
        _panStartOffsetX = MindMap?.OffsetX ?? 0;
        _panStartOffsetY = MindMap?.OffsetY ?? 0;
        Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    private void UpdateCursor()
    {
        if (_isPanning)
            Cursor = new Cursor(StandardCursorType.SizeAll);
        else if (_spaceHeld)
            Cursor = new Cursor(StandardCursorType.Hand);
        else
            Cursor = Cursor.Default;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (MindMap is null) return;

        var pos = e.GetPosition(this);
        var delta = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
        var newScale = Math.Clamp(MindMap.Scale * delta, 0.2, 5.0);

        // Zoom centré sur la position du curseur
        MindMap.OffsetX = pos.X - (pos.X - MindMap.OffsetX) * (newScale / MindMap.Scale);
        MindMap.OffsetY = pos.Y - (pos.Y - MindMap.OffsetY) * (newScale / MindMap.Scale);
        MindMap.Scale = newScale;
    }
}
