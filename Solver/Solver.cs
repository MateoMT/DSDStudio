using System.Collections.Concurrent;
using System.Diagnostics;
using ILGPU;
using ODE;
namespace Solver
{
    public class Solver
    {
        private static int cnt = 1000;
        public static double[] RK4Step(Func<double, double[], double[]> rhs, double[] y, double t, double dt)
        {
            int n = y.Length;
            double[] yNext = new double[n];

            double[] k1 = rhs(t, y);
            double[] yTemp = new double[n];
            for (int i = 0; i < n; i++)
                yTemp[i] = y[i] + 0.5 * dt * k1[i];

            double[] k2 = rhs(t + 0.5 * dt, yTemp);
            for (int i = 0; i < n; i++)
                yTemp[i] = y[i] + 0.5 * dt * k2[i];

            double[] k3 = rhs(t + 0.5 * dt, yTemp);
            for (int i = 0; i < n; i++)
                yTemp[i] = y[i] + dt * k3[i];

            double[] k4 = rhs(t + dt, yTemp);

            for (int i = 0; i < n; i++)
            {
                yNext[i] = y[i] + (dt / 6.0) * (k1[i] + 2 * k2[i] + 2 * k3[i] + k4[i]);
            }

            return yNext;
        }
        public static double[] RK4Step_Parallel(Func<double, double[], double[]> rhs, double t, double[] y, double h)
        {
            int n = y.Length;
            var rangePartitioner = Partitioner.Create(0, n);
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            double[] yNext = new double[n];

            // 计算k1
            double[] k1 = rhs(t, y);

            // 计算yTemp1 (并行)
            double[] yTemp = new double[n];
            Parallel.ForEach(rangePartitioner, options, range =>
            {
                // 遍历当前分区的起始索引（range.Item1）到结束索引（range.Item2）
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    yTemp[i] = y[i] + 0.5 * h * k1[i];  // 原循环体逻辑
                }
            });

            // 计算k2
            double[] k2 = rhs(t + 0.5 * h, yTemp);

            // 计算yTemp2 (并行)
            Parallel.ForEach(rangePartitioner, options, range =>
            {
                // 遍历当前分区的起始索引（range.Item1）到结束索引（range.Item2）
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    yTemp[i] = y[i] + 0.5 * h * k2[i];  // 原循环体逻辑
                }
            });

            // 计算k3
            double[] k3 = rhs(t + 0.5 * h, yTemp);

            Parallel.ForEach(rangePartitioner, options, range =>
            {
                // 遍历当前分区的起始索引（range.Item1）到结束索引（range.Item2）
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    yTemp[i] = y[i] + h * k3[i];  // 原循环体逻辑
                }
            });

            // 计算k4
            double[] k4 = rhs(t + h, yTemp);

            Parallel.ForEach(rangePartitioner, options, range =>
            {
                // 遍历当前分区的起始索引（range.Item1）到结束索引（range.Item2）
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    yNext[i] = y[i] + (h / 6.0) * (k1[i] + 2 * k2[i] + 2 * k3[i] + k4[i]);
                }
            });
            return yNext;
        }
        public static List<double[]> RK4(Func<double, double[], double[]> rhs, double[] y0, double t0, double tfinal, int n)
        {
            List<double[]> results = new List<double[]>
            {
                (double[])y0.Clone()
            };
            double h = (tfinal - t0) / n;
            double[] y = new double[y0.Length];
            Array.Copy(y0, y, y0.Length);
            for (int i = 0; i < n; i++)
            {
                y = RK4Step(rhs, y, t0 + i * h, h);
                results.Add((double[])y.Clone());
            }
            return results;
        }
        public static (List<double> ,List<double[]>) RK45(Func<double, double[], double[]> rhs, double[] y0, double t0, double tfinal, double initialStep = 0.01, double tol = 1e-6)
        {
            List<double[]> results = new List<double[]> { (double[])y0.Clone() };
            List<double> times = new List<double> { t0 };

            double t = t0;
            double dt = initialStep;
            double[] y = (double[])y0.Clone();

            while (t < tfinal)
            {

                var stepResult = RK45Step(rhs, y, t, dt, tol);
                y = stepResult.Item1;
                dt = stepResult.Item2;

                if (t + dt > tfinal)
                    dt = tfinal - t;

                t += dt;
                results.Add((double[])y.Clone());
                times.Add(t);
            }

            return (times,results);
        }
        public static Tuple<double[], double> RK45Step(Func<double, double[], double[]> rhs, double[] y, double t, double dt, double tol = 1e-6, double safetyFactor = 0.7)
        {
            int n = y.Length;
            double[] yNext = new double[n];
            double[] yErr = new double[n];

            // Butcher表系数
            double[] c = { 0, 1.0 / 5.0, 3.0 / 10.0, 4.0 / 5.0, 8.0 / 9.0, 1.0, 1.0 };

            double[,] a = new double[7, 6] 
            {
                { 0, 0, 0, 0, 0, 0 },
                { 1.0/5.0, 0, 0, 0, 0, 0 },
                { 3.0/40.0, 9.0/40.0, 0, 0, 0, 0 },
                { 44.0/45.0, -56.0/15.0, 32.0/9.0, 0, 0, 0 },
                { 19372.0/6561.0, -25360.0/2187.0, 64448.0/6561.0, -212.0/729.0, 0, 0 },
                { 9017.0/3168.0, -355.0/33.0, 46732.0/5247.0, 49.0/176.0, -5103.0/18656.0, 0 },
                { 35.0/384.0, 0, 500.0/1113.0, 125.0/192.0, -2187.0/6784.0, 11.0/84.0 }
            };

            // 5阶方法的权重系数
            double[] b5 = { 35.0 / 384.0, 0, 500.0 / 1113.0, 125.0 / 192.0, -2187.0 / 6784.0, 11.0 / 84.0, 0 };

            // 4阶方法的权重系数 - 用于误差估计
            double[] b4 = { 5179.0 / 57600.0, 0, 7571.0 / 16695.0, 393.0 / 640.0, -92097.0 / 339200.0, 187.0 / 2100.0, 1.0 / 40.0 };

            // 计算k值
            double[][] k = new double[7][];
            for (int i = 0; i < 7; i++)
                k[i] = new double[n];
            // 计算k1
            k[0] = rhs(t, y);

            // 计算后续的k
            for (int i = 1; i < 7; i++)
            {
                double[] yTemp = new double[n];
                for (int j = 0; j < n; j++)
                {
                    yTemp[j] = y[j];
                    for (int m = 0; m < i; m++)
                        yTemp[j] += dt * a[i, m] * k[m][j];
                }
                k[i] = rhs(t + c[i] * dt, yTemp);
            }

            // 计算5阶近似解
            for (int i = 0; i < n; i++)
            {
                yNext[i] = y[i];
                for (int j = 0; j < 7; j++)
                    yNext[i] += dt * b5[j] * k[j][i];
            }
            //for(int i=0;i<20;i++)
            //{
            //    Console.WriteLine(k[1][i]);
            //}
            // 计算误差估计
            double error = 0.0;
            for (int i = 0; i < n; i++)
            {
                double yerr = 0.0;
                for (int j = 0; j < 7; j++)
                    yerr += dt * (b5[j] - b4[j]) * k[j][i];
                yErr[i] = yerr;
                //Console.WriteLine($"当前误差是{yerr}，当前浓度是{y[i]}");
                error = Math.Max(error, Math.Abs(yerr / (Math.Abs(y[i]) + 1e-10)));
            }
            //Console.WriteLine($"当前误差是{error}");
            // 根据误差估计新步长
            double newStep = dt;
            if (error > 0)
            {
                newStep = safetyFactor * dt * Math.Pow(tol / error, 0.2);
                newStep = Math.Max(newStep, 0.1 * dt); 
                newStep = Math.Min(newStep, 5.0 * dt); 
            }

            return new Tuple<double[], double>(yNext, newStep);
        }
        public static List<double[]> RK4_Parallel(Func<double, double[], double[]> rhs, double[] y0, double t0, double tfinal, int n)
        {
            List<double[]> results = new List<double[]>
            {
                (double[])y0.Clone()
            };
            double h = (tfinal - t0) / n;
            double[] y = new double[y0.Length];
            Array.Copy(y0, y, y0.Length);
            for (int i = 0; i < n; i++)
            {
                y = RK4Step_Parallel(rhs, t0 + i * h, y, h);
                results.Add((double[])y.Clone());
            }
            return results;
        }
        public static double[] RK45DormandPrinceStep(Func<double, double[], double[]> rhs, double[] y, double t, double dt, out double error)
        {
            int n = y.Length;
            double[] yNext4 = new double[n]; // 4阶结果
            double[] yNext5 = new double[n]; // 5阶结果

            // Butcher表的系数
            // c 系数 - 阶段时间点
            double[] c = { 0, 0.2, 0.3, 0.8, 8.0 / 9.0, 1.0, 1.0 };

            // a 系数 - 阶段间的依赖关系
            double[,] a = new double[7, 6] {
        { 0, 0, 0, 0, 0, 0 },
        { 1.0/5.0, 0, 0, 0, 0, 0 },
        { 3.0/40.0, 9.0/40.0, 0, 0, 0, 0 },
        { 44.0/45.0, -56.0/15.0, 32.0/9.0, 0, 0, 0 },
        { 19372.0/6561.0, -25360.0/2187.0, 64448.0/6561.0, -212.0/729.0, 0, 0 },
        { 9017.0/3168.0, -355.0/33.0, 46732.0/5247.0, 49.0/176.0, -5103.0/18656.0, 0 },
        { 35.0/384.0, 0, 500.0/1113.0, 125.0/192.0, -2187.0/6784.0, 11.0/84.0 }
    };

            // b 系数 - 5阶解的权重
            double[] b5 = { 35.0 / 384.0, 0, 500.0 / 1113.0, 125.0 / 192.0, -2187.0 / 6784.0, 11.0 / 84.0, 0 };

            // b 系数 - 4阶解的权重
            double[] b4 = { 5179.0 / 57600.0, 0, 7571.0 / 16695.0, 393.0 / 640.0, -92097.0 / 339200.0, 187.0 / 2100.0, 1.0 / 40.0 };

            // 计算k值
            double[][] k = new double[7][];
            for (int i = 0; i < 7; i++)
                k[i] = new double[n];

            // 第一个k值
            k[0] = rhs(t, y);

            // 计算剩余的k值
            for (int i = 1; i < 7; i++)
            {
                double[] yTemp = new double[n];
                for (int j = 0; j < n; j++)
                {
                    yTemp[j] = y[j];
                    for (int l = 0; l < i; l++)
                    {
                        yTemp[j] += dt * a[i, l] * k[l][j];
                    }
                }
                k[i] = rhs(t + c[i] * dt, yTemp);
            }

            // 计算4阶和5阶结果
            for (int i = 0; i < n; i++)
            {
                yNext4[i] = y[i];
                yNext5[i] = y[i];

                for (int j = 0; j < 7; j++)
                {
                    yNext4[i] += dt * b4[j] * k[j][i];
                    yNext5[i] += dt * b5[j] * k[j][i];
                }
            }

            // 计算误差估计
            error = 0;
            for (int i = 0; i < n; i++)
            {
                double err = Math.Abs(yNext5[i] - yNext4[i]);
                error = Math.Max(error, err);
            }

            // 返回5阶结果作为更精确的解
            return yNext5;
        }

        public static (List<double[]> solutions, List<double> timePoints) RK45DormandPrince(
    Func<double, double[], double[]> rhs, double[] y0, double t0, double tfinal,
    double tol = 1e-6, double dtMin = 1e-10, double dtMax = 1.0)
        {
            List<double[]> results = new List<double[]>();
            List<double> timePoints = new List<double>();

            double[] y = (double[])y0.Clone();
            double t = t0;
            double dt = Math.Min(dtMax, (tfinal - t0) / 10.0); // 初始步长

            results.Add((double[])y.Clone());
            timePoints.Add(t);

            while (t < tfinal)
            {
                // 确保不会超过终止时间
                if (t + dt > tfinal)
                    dt = tfinal - t;

                // 执行一步RK45
                double error;
                double[] yNew = RK45DormandPrinceStep(rhs, y, t, dt, out error);

                // 计算新的步长
                double safety = 0.9; // 安全因子
                double p = 5.0;      // 方法阶数

                // 如果误差在容许范围内，接受这一步
                if (error <= tol || dt <= dtMin)
                {
                    t += dt;
                    y = yNew;
                    results.Add((double[])y.Clone());
                    timePoints.Add(t);
                }

                // 计算新的步长
                if (error < 1e-15) // 避免除以零
                    dt = safety * dt * Math.Pow(tol / error, 1.0 / p);

                dt = Math.Min(dtMax, Math.Max(dtMin, dt));

                if (Math.Abs(t - tfinal) < 1e-10)
                    break;
            }

            return (results, timePoints);
        }
        public static (List<double> ,List<double[]>) solve2(ODEsys system, Dictionary<int, double> initinitialConcentrations, double tStart, double tEnd, double dt)//根据物质数目选择调用GPU还是CPU求解，并返回一致的结果
        {
            List<double[]> ans = new List<double[]>();
            double[] initialConcentrations = new double[system.equations.Count];

            //for (int i = 0; i < system.equations.Count; i++)
            foreach (var (key, val) in initinitialConcentrations)
            {
                initialConcentrations[key] = val;
            }
            int steps = (int)((tEnd - tStart) / dt);
            var (solutions, timePoints) = RK45DormandPrince(system.GetRightHandSide(), initialConcentrations, tStart, tEnd, dt);
            return (timePoints, solutions);
        }
        public static (List<double>, List<double[]>) solve3(ODEsys system, Dictionary<int, double> initinitialConcentrations, double tStart, double tEnd, double dt=0.1)//根据物质数目选择调用GPU还是CPU求解，并返回一致的结果
        {
            List<double[]> ans = new List<double[]>();
            double[] initialConcentrations = new double[system.equations.Count];
            foreach (var (key, val) in initinitialConcentrations)
            {
                initialConcentrations[key] = val;
            }
            return RK45(system.GetRightHandSide(), initialConcentrations, tStart, tEnd);
        }
        public static (List<double>, List<double[]>) solve4(ODEsys system, Dictionary<int, double> initinitialConcentrations, double tStart, double tEnd, double dt = 0.1)//根据物质数目选择调用GPU还是CPU求解，并返回一致的结果
        {
            List<double[]> ans = new List<double[]>();
            double[] initialConcentrations = new double[system.equations.Count];
            foreach (var (key, val) in initinitialConcentrations)
            {
                initialConcentrations[key] = val;
            }
            List<double> times = new();
            List<double[]> results = new();
            using (var solver = new GPURK45(system))
            {
                (times,results) = solver.Solve(initialConcentrations, tStart, tEnd, dt);
            }
            return (times, results);
        }
        public static List<double[]> solve(ODEsys system, Dictionary<int, double> initinitialConcentrations, double tStart, double tEnd, double dt)//根据物质数目选择调用GPU还是CPU求解，并返回一致的结果
        {
            List<double[]> ans = new List<double[]>();
            double[] initialConcentrations = new double[system.equations.Count];

            //for (int i = 0; i < system.equations.Count; i++)
            foreach (var (key, val) in initinitialConcentrations)
            {
                initialConcentrations[key] = val;
            }
            int steps = (int)((tEnd - tStart) / dt);
            Stopwatch stopwatch = Stopwatch.StartNew();


            if (system.equations.Count <= cnt)
            {
                try
                {
                    //ans = RK45(system.GetRightHandSide(), initialConcentrations, tStart, tEnd);

                }
                catch (Exception e)
                {
                    Console.WriteLine($"CPU求解失败: {e.Message}");
                    throw;
                }
                finally
                {
                    stopwatch.Stop();
                }
            }
            else//尝试调用GPU
            {
                try
                {
                    using (var solver = new GPURK4Solver(system))
                    {
                        double[,] results = solver.Solve(initialConcentrations, tStart, tEnd, dt);
                        stopwatch.Stop();
                        ans = new List<double[]>();
                        int steps1 = (int)((tEnd - tStart) / dt);
                        int substances = system.equations.Count;
                        for (int i = 0; i < steps1; i++)
                        {
                            double[] row = new double[substances];
                            for (int j = 0; j < substances; j++)
                            {
                                row[j] = results[i, j];
                            }
                            ans.Add(row);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"GPU求解失败: {e.Message}");
                    Console.WriteLine("使用CPU求解");
                    ans = RK4(system.GetRightHandSide(), initialConcentrations, tStart, tEnd, (int)((tEnd - tStart) / dt));
                }
            }
            Console.WriteLine($"RK4 求解耗时: {stopwatch.ElapsedMilliseconds} 毫秒");
            return ans;
        }
    }
}