using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml;

namespace Datamerge.Services
{
    // Interfaz (Contrato)
    public interface IFileService
    {
        Task<IEnumerable<string>> PickFilesToOpenAsync();
        Task<string?> PickFileToSaveAsync(string defaultFileName);
    }

    // Clase (Implementación)
    public class FileService : IFileService
    {
        private readonly Window _window;

        public FileService(Window window)
        {
            _window = window;
        }

        public async Task<IEnumerable<string>> PickFilesToOpenAsync()
        {
            var picker = new FileOpenPicker();
            InitializePicker(picker);

            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".xlsx");
            picker.FileTypeFilter.Add(".xls");
            picker.FileTypeFilter.Add(".csv");

            var files = await picker.PickMultipleFilesAsync();
            if (files == null) return new List<string>();

            var results = new List<string>();
            foreach (var f in files) results.Add(f.Path);
            return results;
        }

        public async Task<string?> PickFileToSaveAsync(string defaultFileName)
        {
            var savePicker = new FileSavePicker();
            InitializePicker(savePicker);

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Excel Workbook", new List<string>() { ".xlsx" });
            savePicker.FileTypeChoices.Add("CSV", new List<string>() { ".csv" });
            savePicker.SuggestedFileName = defaultFileName;

            var file = await savePicker.PickSaveFileAsync();
            return file?.Path;
        }

        private void InitializePicker(object picker)
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
        }
    }
}