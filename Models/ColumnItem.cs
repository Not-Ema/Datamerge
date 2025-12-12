using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
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

        public Visibility CustomVisibility => IsCustom ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FileVisibility => IsCustom ? Visibility.Collapsed : Visibility.Visible;

        // --- SISTEMA DE IDENTIFICACIÓN DE ORIGEN (ETL) ---
        // Key: RutaArchivo, Value: NombreColumnaOriginal
        public Dictionary<string, string> FileMappings { get; } = new();

        [ObservableProperty] private string _sourceInfo = "";

        public void RefreshSourceCount()
        {
            SourceInfo = IsCustom ? "Manual" : $"{FileMappings.Count} fuente(s)";
        }

        // Lógica de Dominio: Validación de Fusión
        public bool CanMergeWith(ColumnItem other)
        {
            foreach (var myFile in FileMappings.Keys)
            {
                if (other.FileMappings.ContainsKey(myFile)) return false;
            }
            return true;
        }
    }
}