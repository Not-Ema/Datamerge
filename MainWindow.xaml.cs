using CommunityToolkit.WinUI.UI.Controls;
using Datamerge.Services;
using Datamerge.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data; // Necesario para Binding
using Microsoft.UI.Xaml.Input;
using System.Collections.Generic; // Necesario para IDictionary

namespace Datamerge
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }
        private bool _allowDrag = false;

        public MainWindow()
        {
            this.InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            var excelService = new ExcelService();
            var fileService = new FileService(this);
            ViewModel = new MainViewModel(excelService, fileService, this);

            // IMPORTANTE: Suscribirse para generar las columnas cuando cambien los datos
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Iniciar en la pestaña Configuración
            UpdateTabs("Config");
        }

        // --- LÓGICA CRÍTICA: Generar columnas dinámicas para la Vista Previa ---
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.PreviewSource))
            {
                // Si hay datos, generamos las columnas basadas en la primera fila
                if (ViewModel.PreviewSource != null && ViewModel.PreviewSource.Count > 0)
                {
                    var firstRow = ViewModel.PreviewSource[0] as IDictionary<string, object>;
                    if (firstRow != null)
                    {
                        PreviewGrid.Columns.Clear();
                        foreach (var key in firstRow.Keys)
                        {
                            var column = new DataGridTextColumn
                            {
                                Header = key,
                                Binding = new Binding { Path = new PropertyPath($"[{key}]") }
                            };
                            PreviewGrid.Columns.Add(column);
                        }
                    }
                }
                else
                {
                    PreviewGrid.Columns.Clear();
                }
            }
        }

        // --- EVENTO DE LOS BOTONES DEL MENÚ ---
        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                UpdateTabs(btn.Tag.ToString());
            }
        }

        private void UpdateTabs(string tag)
        {
            if (tag == "Config")
            {
                ConfigPanel.Visibility = Visibility.Visible;
                PreviewPanel.Visibility = Visibility.Collapsed;

                BtnConfig.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                BtnPreview.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
            }
            else
            {
                ConfigPanel.Visibility = Visibility.Collapsed;
                PreviewPanel.Visibility = Visibility.Visible;

                BtnConfig.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
                BtnPreview.Style = (Style)Application.Current.Resources["AccentButtonStyle"];

                // Forzar actualización de datos al entrar
                if (ViewModel.RefreshPreviewCommand.CanExecute(null))
                {
                    ViewModel.RefreshPreviewCommand.Execute(null);
                }
            }
        }

        // --- LÓGICA DE ARRASTRE ---
        private void Handle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _allowDrag = true;
            e.Handled = false;
        }

        private void Handle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _allowDrag = false;
        }

        private void ColumnsListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (!_allowDrag) e.Cancel = true;
        }
    }
}