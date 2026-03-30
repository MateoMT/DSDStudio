using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IDE.ViewModels;
using ODE;
using unvell.ReoGrid;
using unvell.ReoGrid.IO.OpenXML.Schema;

namespace IDE.Views
{
    /// <summary>
    /// Results.xaml 的交互逻辑
    /// </summary>
    public partial class Results : Window
    {
        public ReoGridControl GetReo()
        {
            return Grids;
        }
        public Results()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.ResizeMode = ResizeMode.NoResize;
            var viewModel = DataContext as ResultViewModel;
            if (viewModel.GetState() != WindowState.Maximized)
                this.DragMove();
        }
        public void setGrid(List<double[]> datas, List<double> times, ODEsys odes)
        {
            Dispatcher.Invoke(() => {
                ReoGridControl? grid = Grids;
                if (grid == null)
                {
                    MessageBox.Show("Grid is not initialized.");
                    return;
                }

                unvell.ReoGrid.Worksheet sheet = grid.CurrentWorksheet;
                int rowCount = datas.Count;
                int columnCount = datas[0].Length;
                sheet.Reset();
                if (sheet.RowCount < rowCount + 1)
                {
                    sheet.Resize(rowCount + 1, sheet.ColumnCount > columnCount + 1 ? sheet.ColumnCount : columnCount + 1);
                }
                sheet[0, 0] = "Time";
                sheet.Cells[0, 0].Style.HAlign = ReoGridHorAlign.Center;
                sheet.Cells[0, 0].Style.VAlign = ReoGridVerAlign.Middle;
                for (int j = 0; j < columnCount; j++)
                {
                    sheet[0, j + 1] = odes.names.ContainsKey(j) ? odes.names[j] : $"Column {j + 1}";
                    sheet.Cells[0, j + 1].Style.HAlign = ReoGridHorAlign.Center;
                    sheet.Cells[0, j + 1].Style.VAlign = ReoGridVerAlign.Middle;
                }

                for (int i = 0; i < rowCount; i++)
                {
                    sheet[i + 1, 0] = Math.Round(times[i],6);
                    sheet.Cells[i + 1, 0].Style.HAlign = ReoGridHorAlign.Center;
                    sheet.Cells[i + 1, 0].Style.VAlign = ReoGridVerAlign.Middle;
                    for (int j = 0; j < columnCount; j++)
                    {
                        sheet[i + 1, j + 1] = Math.Round(datas[i][j], 6);
                        sheet.Cells[i + 1, j + 1].Style.HAlign = ReoGridHorAlign.Center;
                        sheet.Cells[i + 1, j + 1].Style.VAlign = ReoGridVerAlign.Middle;
                    }
                }

                for (int j = 0; j < columnCount + 1; j++)
                {
                    sheet.SetColumnsWidth(j, 1, 100);
                }
                sheet.SetRowsHeight(0, 1, 30);
                sheet.FreezeToCell(1, 0);
                sheet.SetCols(columnCount + 1);
                var worksheetSettingsEnable = WorksheetSettings.Edit_Readonly;
                sheet.EnableSettings(worksheetSettingsEnable);
            });
        }
        
    }
}
