using Datamerge.Models;
using ExcelDataReader;
using MiniExcelLibs;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Datamerge.Services
{
    // 1. DEFINICIÓN DE LA INTERFAZ (El contrato)
    public interface IExcelService
    {
        IEnumerable<string> GetColumnsFromFile(string path);
        List<Dictionary<string, object>> ReadFile(string path);
        void ExportData(string path, List<Dictionary<string, object>> data);

        // Helpers de negocio
        string ExtractPeriodSmart(object? rawValue);
        string CleanJobValue(string? rawValue);
        bool IsJobColumn(string headerName); // <--- NUEVO MÉTODO AGREGADO AL CONTRATO

        List<Dictionary<string, object>> GeneratePreview(
            IEnumerable<string> filePaths,
            List<ColumnItem> columns,
            bool generatePeriods,
            bool cleanJobType,
            int maxRows = 10);
    }

    // 2. IMPLEMENTACIÓN DE LA CLASE (La lógica)
    public class ExcelService : IExcelService
    {
        public ExcelService()
        {
            // Necesario para leer archivos .xls antiguos
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public IEnumerable<string> GetColumnsFromFile(string path)
        {
            try
            {
                var extension = Path.GetExtension(path).ToLower();
                if (extension == ".xls")
                {
                    using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
                    using var reader = ExcelReaderFactory.CreateReader(stream);
                    var conf = new ExcelDataSetConfiguration { ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true } };
                    var ds = reader.AsDataSet(conf);
                    if (ds.Tables.Count > 0)
                        return ds.Tables[0].Columns.Cast<DataColumn>().Select(c => c.ColumnName);
                }
                else
                {
                    return MiniExcel.GetColumns(path, useHeaderRow: true);
                }
            }
            catch
            {
                return new List<string>();
            }
            return new List<string>();
        }

        public List<Dictionary<string, object>> ReadFile(string path)
        {
            var result = new List<Dictionary<string, object>>();
            try
            {
                var extension = Path.GetExtension(path).ToLower();
                if (extension == ".xls")
                {
                    using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
                    using var reader = ExcelReaderFactory.CreateReader(stream);
                    var conf = new ExcelDataSetConfiguration { ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true } };
                    var ds = reader.AsDataSet(conf);
                    if (ds.Tables.Count > 0)
                    {
                        var dt = ds.Tables[0];
                        foreach (DataRow row in dt.Rows)
                        {
                            var dict = new Dictionary<string, object>();
                            foreach (DataColumn col in dt.Columns) dict[col.ColumnName] = row[col] ?? "";
                            result.Add(dict);
                        }
                    }
                }
                else
                {
                    var rawRows = MiniExcel.Query(path, useHeaderRow: true);
                    foreach (var r in rawRows)
                    {
                        var dict = (IDictionary<string, object>)r;
                        var safeDict = new Dictionary<string, object>();
                        foreach (var kvp in dict) safeDict[kvp.Key] = kvp.Value ?? "";
                        result.Add(safeDict);
                    }
                }
            }
            catch { }
            return result;
        }

        public void ExportData(string path, List<Dictionary<string, object>> data)
        {
            var type = Path.GetExtension(path).ToLower() == ".csv" ? ExcelType.CSV : ExcelType.XLSX;
            MiniExcel.SaveAs(path, data, excelType: type, overwriteFile: true);
        }

        public string ExtractPeriodSmart(object? rawValue)
        {
            if (rawValue == null) return "";
            if (rawValue is DateTime dt) return dt.ToString("yyyyMM");
            string? text = rawValue.ToString();
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim();
            if (DateTime.TryParse(text, out DateTime simpleDate)) return simpleDate.ToString("yyyyMM");
            var match = Regex.Match(text, @"(\d{4}[-/]\d{1,2}[-/]\d{1,2})|(\d{1,2}[-/]\d{1,2}[-/]\d{4})");
            if (match.Success && DateTime.TryParse(match.Value, out DateTime extractedDate)) return extractedDate.ToString("yyyyMM");
            return "";
        }

        public string CleanJobValue(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return "";
            var parts = rawValue.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0].Trim() : rawValue;
        }

        // --- NUEVA LÓGICA CENTRALIZADA ---
        public bool IsJobColumn(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName)) return false;

            // Comprueba si contiene "Tipo" Y "Trabajo" ignorando mayúsculas/minúsculas
            return headerName.Contains("Tipo", StringComparison.OrdinalIgnoreCase) &&
                   headerName.Contains("Trabajo", StringComparison.OrdinalIgnoreCase);
        }

        public List<Dictionary<string, object>> GeneratePreview(
            IEnumerable<string> filePaths,
            List<ColumnItem> columns,
            bool generatePeriods,
            bool cleanJobType,
            int maxRows = 10)
        {
            var previewRows = new List<Dictionary<string, object>>();
            var activeColumns = columns.Where(c => c.IsSelected).ToList();
            int rowCount = 0;

            foreach (var path in filePaths)
            {
                if (rowCount >= maxRows) break;

                var rawData = ReadFile(path);

                foreach (var rowData in rawData)
                {
                    if (rowCount >= maxRows) break;

                    var newRow = new Dictionary<string, object>();

                    // 1. Periodos
                    if (generatePeriods)
                    {
                        var keyLeg = rowData.Keys.FirstOrDefault(k => k.Equals("Fecha_Leg", StringComparison.OrdinalIgnoreCase));
                        var keyAsig = rowData.Keys.FirstOrDefault(k => k.Equals("Fecha_Asig", StringComparison.OrdinalIgnoreCase));
                        newRow["PeriodoL"] = ExtractPeriodSmart(keyLeg != null ? rowData[keyLeg] : null);
                        newRow["PeriodoA"] = ExtractPeriodSmart(keyAsig != null ? rowData[keyAsig] : null);
                    }

                    // 2. Mapeo de columnas
                    foreach (var col in activeColumns)
                    {
                        if (col.IsCustom)
                        {
                            newRow[col.HeaderName] = col.DefaultValue;
                        }
                        else
                        {
                            object? finalValue = null;
                            if (col.FileMappings.TryGetValue(path, out string originalName))
                            {
                                if (rowData.TryGetValue(originalName, out var value)) finalValue = value;
                            }

                            // USAMOS EL NUEVO MÉTODO IsJobColumn
                            if (finalValue != null && cleanJobType && IsJobColumn(col.HeaderName))
                            {
                                finalValue = CleanJobValue(finalValue.ToString());
                            }
                            newRow[col.HeaderName] = finalValue ?? "";
                        }
                    }

                    previewRows.Add(newRow);
                    rowCount++;
                }
            }
            return previewRows;
        }
    }
}