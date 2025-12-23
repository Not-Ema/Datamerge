using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datamerge.Models;
using Datamerge.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Datamerge.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IExcelService _excelService;
        private readonly IFileService _fileService;
        private readonly Window _window;

        public MainViewModel(IExcelService excelService, IFileService fileService, Window window)
        {
            _excelService = excelService;
            _fileService = fileService;
            _window = window;
        }

        // --- Propiedades ---
        [ObservableProperty] private ObservableCollection<FileInfoModel> _files = new();
        [ObservableProperty] private ObservableCollection<ColumnItem> _columns = new();
        [ObservableProperty] private bool _generatePeriods = false;
        [ObservableProperty] private bool _cleanJobType = false;

        [ObservableProperty]
        private ObservableCollection<object> _previewSource = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBusyVisibility))]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;
        public Visibility IsBusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;
        public bool IsNotBusy => !IsBusy;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TargetColumnInfo))]
        private ColumnItem? _targetColumn;

        public string TargetColumnInfo => TargetColumn != null
            ? $"Destino: {TargetColumn.HeaderName} (Selecciona columnas para unir)"
            : "Selecciona una columna Destino (Clic Derecho o Menú)";

        private void UpdateMergeVisuals()
        {
            foreach (var col in Columns)
            {
                col.IsTargetMode = (_targetColumn != null && col == _targetColumn);
                if (_targetColumn != null && col != _targetColumn)
                    col.CanBeMergedIntoTarget = _targetColumn.CanMergeWith(col);
                else
                    col.CanBeMergedIntoTarget = false;
            }
        }

        // --- Comandos ---

        [RelayCommand]
        private void StartRenaming(ColumnItem item)
        {
            item.IsRenaming = true;
        }

        [RelayCommand]
        private void FinishRenaming(ColumnItem item)
        {
            item.IsRenaming = false;
            if (TargetColumn == item) OnPropertyChanged(nameof(TargetColumnInfo));
        }

        // --- NUEVO COMANDO: Separar (Desvincular) columna ---
        [RelayCommand]
        private void DetachMapping(object item)
        {
            // El parámetro viene como KeyValuePair<string, string> desde el XAML
            if (item is KeyValuePair<string, string> kvp)
            {
                var filePath = kvp.Key;
                var originalName = kvp.Value;

                // Buscamos a qué columna pertenece este mapeo
                var parentColumn = Columns.FirstOrDefault(c => c.FileMappings.ContainsKey(filePath) && c.FileMappings[filePath] == originalName);

                if (parentColumn != null)
                {
                    // 1. Eliminar de la columna actual
                    parentColumn.FileMappings.Remove(filePath);
                    parentColumn.RefreshSourceCount();

                    // 2. Crear nueva columna independiente
                    var newCol = new ColumnItem
                    {
                        HeaderName = originalName,
                        IsCustom = false,
                        IsSelected = true
                    };
                    newCol.FileMappings.Add(filePath, originalName);
                    newCol.RefreshSourceCount();

                    // 3. Insertar justo debajo de la original para visibilidad
                    int index = Columns.IndexOf(parentColumn);
                    if (index >= 0 && index < Columns.Count - 1)
                        Columns.Insert(index + 1, newCol);
                    else
                        Columns.Add(newCol);

                    // 4. Limpieza opcional: Si la columna padre quedó vacía y no es manual, borrarla
                    if (parentColumn.FileMappings.Count == 0 && !parentColumn.IsCustom)
                    {
                        if (TargetColumn == parentColumn) TargetColumn = null;
                        Columns.Remove(parentColumn);
                    }

                    UpdateMergeVisuals();
                }
            }
        }

        [RelayCommand]
        private async Task RefreshPreview()
        {
            if (Files.Count == 0 || !Columns.Any(c => c.IsSelected))
            {
                _window.DispatcherQueue.TryEnqueue(() => PreviewSource.Clear());
                return;
            }

            IsBusy = true;
            await Task.Run(() =>
            {
                var rawData = _excelService.GeneratePreview(
                    Files.Select(f => f.FullPath),
                    Columns.ToList(),
                    GeneratePeriods,
                    CleanJobType,
                    10
                );

                var dynamicList = new ObservableCollection<object>();
                if (rawData != null && rawData.Count > 0)
                {
                    var activeHeaderNames = new HashSet<string>(
                        Columns.Where(c => c.IsSelected).Select(c => c.HeaderName),
                        StringComparer.OrdinalIgnoreCase
                    );
                    if (GeneratePeriods) { activeHeaderNames.Add("PeriodoL"); activeHeaderNames.Add("PeriodoA"); }

                    foreach (var rowDict in rawData)
                    {
                        var expando = new ExpandoObject() as IDictionary<string, object>;
                        bool hasData = false;
                        foreach (var kvp in rowDict)
                        {
                            if (activeHeaderNames.Contains(kvp.Key))
                            {
                                expando[kvp.Key] = kvp.Value;
                                hasData = true;
                            }
                        }
                        if (hasData) dynamicList.Add(expando);
                    }
                }
                _window.DispatcherQueue.TryEnqueue(() => { PreviewSource = dynamicList; });
            });
            IsBusy = false;
        }

        [RelayCommand]
        private async Task PickFiles()
        {
            var paths = await _fileService.PickFilesToOpenAsync();
            bool newFilesAdded = false;
            foreach (var path in paths)
            {
                if (!Files.Any(f => f.FullPath == path))
                {
                    Files.Add(new FileInfoModel { FileName = Path.GetFileName(path), FullPath = path });
                    newFilesAdded = true;
                }
            }
            if (newFilesAdded) LoadAllColumns();
        }

        [RelayCommand]
        private async Task Consolidate()
        {
            if (Files.Count == 0 || !Columns.Any(c => c.IsSelected)) return;
            string defaultName = $"Consolidado_{DateTime.Now:yyyyMMdd}";
            var savePath = await _fileService.PickFileToSaveAsync(defaultName);

            if (!string.IsNullOrEmpty(savePath))
            {
                IsBusy = true;
                await Task.Run(() => ProcessConsolidation(savePath));
                IsBusy = false;
            }
        }

        private void ProcessConsolidation(string savePath)
        {
            var rowsToExport = new List<Dictionary<string, object>>();
            var activeColumns = Columns.Where(c => c.IsSelected).ToList();

            foreach (var sourceFile in Files)
            {
                var processedRows = _excelService.ReadFile(sourceFile.FullPath);
                foreach (var rowData in processedRows)
                {
                    var newRow = new Dictionary<string, object>();
                    if (GeneratePeriods)
                    {
                        var keyLeg = rowData.Keys.FirstOrDefault(k => k.Equals("Fecha_Leg", StringComparison.OrdinalIgnoreCase));
                        var keyAsig = rowData.Keys.FirstOrDefault(k => k.Equals("Fecha_Asig", StringComparison.OrdinalIgnoreCase));
                        newRow["PeriodoL"] = _excelService.ExtractPeriodSmart(keyLeg != null ? rowData[keyLeg] : null);
                        newRow["PeriodoA"] = _excelService.ExtractPeriodSmart(keyAsig != null ? rowData[keyAsig] : null);
                    }

                    foreach (var col in activeColumns)
                    {
                        if (col.IsCustom) newRow[col.HeaderName] = col.DefaultValue;
                        else
                        {
                            object? finalValue = null;
                            if (col.FileMappings.TryGetValue(sourceFile.FullPath, out string originalName))
                                if (rowData.TryGetValue(originalName, out var value)) finalValue = value;

                            // FIX: Usar IsJobColumn para detectar mayúsculas y variantes
                            if (finalValue != null && CleanJobType && _excelService.IsJobColumn(col.HeaderName))
                                finalValue = _excelService.CleanJobValue(finalValue.ToString());

                            newRow[col.HeaderName] = finalValue ?? "";
                        }
                    }
                    rowsToExport.Add(newRow);
                }
            }
            _excelService.ExportData(savePath, rowsToExport);
        }

        private void LoadAllColumns()
        {
            var customCols = Columns.Where(c => c.IsCustom).ToList();
            var masterMap = new Dictionary<string, ColumnItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Files)
            {
                var fileCols = _excelService.GetColumnsFromFile(file.FullPath);
                foreach (var colName in fileCols)
                {
                    if (string.IsNullOrWhiteSpace(colName)) continue;
                    if (!masterMap.ContainsKey(colName))
                    {
                        var newItem = new ColumnItem { HeaderName = colName, IsCustom = false, IsSelected = true };
                        newItem.FileMappings[file.FullPath] = colName;
                        masterMap[colName] = newItem;
                    }
                    else
                    {
                        var existingItem = masterMap[colName];
                        if (!existingItem.FileMappings.ContainsKey(file.FullPath))
                            existingItem.FileMappings[file.FullPath] = colName;
                    }
                }
            }
            Columns.Clear();
            foreach (var c in customCols) Columns.Add(c);
            foreach (var kvp in masterMap) { kvp.Value.RefreshSourceCount(); Columns.Add(kvp.Value); }
            UpdateMergeVisuals();
        }

        [RelayCommand] private void AddCustomColumn() => Columns.Insert(0, new ColumnItem { HeaderName = "Nueva Columna", IsCustom = true, IsSelected = true });

        [RelayCommand]
        private void RemoveColumn(ColumnItem item)
        {
            if (Columns.Contains(item)) Columns.Remove(item);
            if (TargetColumn == item) TargetColumn = null;
            UpdateMergeVisuals();
        }

        [RelayCommand] void SelectAllColumns() { foreach (var c in Columns) c.IsSelected = true; }
        [RelayCommand] void DeselectAllColumns() { foreach (var c in Columns) c.IsSelected = false; }

        [RelayCommand]
        void ClearAll()
        {
            Files.Clear(); Columns.Clear(); TargetColumn = null;
            UpdateMergeVisuals(); PreviewSource.Clear();
        }

        [RelayCommand]
        private void SetAsTarget(ColumnItem item)
        {
            if (TargetColumn == item) TargetColumn = null; else TargetColumn = item;
            UpdateMergeVisuals();
        }

        [RelayCommand]
        private void MergeIntoTarget(ColumnItem sourceItem)
        {
            if (TargetColumn == null || sourceItem == TargetColumn || !TargetColumn.CanMergeWith(sourceItem)) return;
            foreach (var mapping in sourceItem.FileMappings)
                if (!TargetColumn.FileMappings.ContainsKey(mapping.Key))
                    TargetColumn.FileMappings[mapping.Key] = mapping.Value;
            Columns.Remove(sourceItem);
            TargetColumn.RefreshSourceCount();
            UpdateMergeVisuals();
        }

        [RelayCommand]
        private void InjectEmptyColumns()
        {
            void InsertOrMove(string colName, string targetColName)
            {
                var existing = Columns.FirstOrDefault(c => c.HeaderName.Equals(colName, StringComparison.OrdinalIgnoreCase));
                if (existing != null) Columns.Remove(existing);
                var newCol = new ColumnItem { HeaderName = colName, DefaultValue = "", IsCustom = true, IsSelected = true };
                var target = Columns.FirstOrDefault(c => c.HeaderName.Equals(targetColName, StringComparison.OrdinalIgnoreCase));
                if (target != null) Columns.Insert(Columns.IndexOf(target) + 1, newCol); else Columns.Add(newCol);
            }
            InsertOrMove("SUBCATEGORIA", "Barrio_desc");
            InsertOrMove("FECHA_PAGO", "Medidor");
            InsertOrMove("USRS_LEGAL", "FECHA_PAGO");
            UpdateMergeVisuals();
        }
    }
}