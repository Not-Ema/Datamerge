using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace Datamerge.Models
{
    public partial class ColumnItem : ObservableObject
    {
        [ObservableProperty] private string _headerName = string.Empty;
        [ObservableProperty] private bool _isSelected = true;
        [ObservableProperty] private string _defaultValue = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CustomVisibility))]
        [NotifyPropertyChangedFor(nameof(FileVisibility))]
        private bool _isCustom = false;

        // Visibilidad específica para campos exclusivos de columnas manuales (como Valor Fijo)
        public Visibility CustomVisibility => IsCustom ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FileVisibility => IsCustom ? Visibility.Collapsed : Visibility.Visible;

        // --- SISTEMA DE RENOMBRADO (NUEVO) ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NameReadVisibility))]
        [NotifyPropertyChangedFor(nameof(NameEditVisibility))]
        private bool _isRenaming = false;

        // Si NO estamos renombrando, mostramos el texto y el lápiz
        public Visibility NameReadVisibility => IsRenaming ? Visibility.Collapsed : Visibility.Visible;
        // Si ESTAMOS renombrando, mostramos el TextBox y el Check
        public Visibility NameEditVisibility => IsRenaming ? Visibility.Visible : Visibility.Collapsed;

        // --- SISTEMA DE IDENTIFICACIÓN DE ORIGEN (ETL) ---
        public Dictionary<string, string> FileMappings { get; } = new();

        [ObservableProperty] private string _sourceInfo = "";

        public void RefreshSourceCount()
        {
            SourceInfo = IsCustom ? "Manual" : $"{FileMappings.Count} fuente(s)";
        }

        public bool CanMergeWith(ColumnItem other)
        {
            foreach (var myFile in FileMappings.Keys)
            {
                if (other.FileMappings.ContainsKey(myFile)) return false;
            }
            return true;
        }

        // --- UI DE FUSIÓN (Visuales) ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RowBackground))]
        [NotifyPropertyChangedFor(nameof(DeleteButtonVisibility))]
        private bool _isTargetMode;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MergeButtonVisibility))]
        [NotifyPropertyChangedFor(nameof(DeleteButtonVisibility))]
        private bool _canBeMergedIntoTarget;

        public Brush RowBackground =>
            IsTargetMode
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 120, 215))
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        public Visibility MergeButtonVisibility =>
            CanBeMergedIntoTarget ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DeleteButtonVisibility =>
            (!IsTargetMode && !CanBeMergedIntoTarget) ? Visibility.Visible : Visibility.Collapsed;
    }
}