using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using OxyPlot.SkiaSharp;
namespace PaintUtils
{
    public class Chart
    {
        public static void SaveLineChart(string filePath, List<double[]> results, double t0, double h, Dictionary<int, string> substanceNames)
        {
            // 创建绘图模型
            var plotModel = new PlotModel
            {
                Title = "化学反应模拟结果",
                DefaultFont = "Microsoft YaHei" // 设置支持中文的字体

            };

            // 添加时间轴
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "时间 (t)",
                Minimum = t0,
                Maximum = t0 + h * (results.Count - 1)
            });

            // 添加浓度轴
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "浓度",
                Minimum = 0
            });

            // 为每种物质添加折线
            for (int i = 0; i < substanceNames.Count; i++)
            {
                var lineSeries = new LineSeries
                {
                    Title = substanceNames.Values.ElementAt(i),
                    StrokeThickness = 2
                };

                for (int j = 0; j < results.Count; j++)
                {
                    double time = t0 + j * h;
                    lineSeries.Points.Add(new DataPoint(time, results[j][i]));
                }

                plotModel.Series.Add(lineSeries);
            }

            // 保存为图片
            using (var stream = File.Create(filePath))
            {
                var exporter = new PngExporter { Width = 800, Height = 600 };
                exporter.Export(plotModel, stream);
            }

            Console.WriteLine($"折线图已保存至 {filePath}");
        }

    }
}
