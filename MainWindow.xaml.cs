using QuantApp.Services;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace QuantApp
{
    public partial class MainWindow : Window
    {
        private DatabaseService dbService;
        private DataTable stockData;

        public MainWindow()
        {
            InitializeComponent();
            dbService = new DatabaseService();

            // Handle checkbox changes
            stockDataGrid.CellEditEnding += DataGrid_CellEditEnding;

            LoadData();
        }

        private async void LoadData()
        {
            try
            {
                stockData = await dbService.GetStockDataAsync();
                stockDataGrid.ItemsSource = stockData.DefaultView;
                txtStatus.Text = $"Loaded {stockData.Rows.Count} records";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private async void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.ToString() == "✓")
            {
                var row = e.Row.Item as DataRowView;
                var checkBox = e.EditingElement as CheckBox;

                if (row != null && checkBox != null)
                {
                    string stockCode = row["stock_code"].ToString();

                    if (checkBox.IsChecked == true)
                    {
                        await dbService.MarkAsCheckedAsync(stockCode);
                        row["LastChecked"] = DateTime.Now;
                        row["LastCheckedDisplay"] = $"Last checked: {DateTime.Now:yy/MM/dd}";
                    }
                    else
                    {
                        await dbService.UnmarkAsCheckedAsync(stockCode);
                        row["LastCheckedDisplay"] = "Not checked";
                    }
                }
            }
        }
    }
}