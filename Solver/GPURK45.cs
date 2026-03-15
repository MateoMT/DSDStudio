using ILGPU.Runtime;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime.OpenCL;
using ILGPU.Runtime.Cuda;

namespace Solver
{
    public class GPURK45 : IDisposable
    {
        private Context context;
        private Accelerator accelerator;
        private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>,
                      ArrayView<int>, ArrayView<int>, ArrayView<int>, ArrayView<int>,
                      ArrayView<int>, ArrayView<int>, ArrayView<double>, int, int, double> calculateRatesKernel;
        private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>,
              ArrayView<double>, ArrayView<double>, ArrayView<double>,
              ArrayView<double>, ArrayView<double>, double, int, int> computeTempStateKernel;

        private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>,
                      double, int> errorEstimationKernel;

        private ODE.GPU_ODE_Data gpuData;
        private MemoryBuffer1D<double, Stride1D.Dense> d_termCoefficients;
        private MemoryBuffer1D<int, Stride1D.Dense> d_termFactorsStart;
        private MemoryBuffer1D<int, Stride1D.Dense> d_termFactorsLength;
        private MemoryBuffer1D<int, Stride1D.Dense> d_factorSubstanceIds;
        private MemoryBuffer1D<int, Stride1D.Dense> d_factorExponents;
        private MemoryBuffer1D<int, Stride1D.Dense> d_equationTermStart;
        private MemoryBuffer1D<int, Stride1D.Dense> d_equationTermCount;
        private MemoryBuffer1D<double, Stride1D.Dense> d_concentrations;

        // RK45需要7个k值
        private MemoryBuffer1D<double, Stride1D.Dense> d_k1;
        private MemoryBuffer1D<double, Stride1D.Dense> d_k2;
        private MemoryBuffer1D<double, Stride1D.Dense> d_k3;
        private MemoryBuffer1D<double, Stride1D.Dense> d_k4;
        private MemoryBuffer1D<double, Stride1D.Dense> d_k5;
        private MemoryBuffer1D<double, Stride1D.Dense> d_k6;
        private MemoryBuffer1D<double, Stride1D.Dense> d_k7;

        private MemoryBuffer1D<double, Stride1D.Dense> d_tempState;
        private MemoryBuffer1D<double, Stride1D.Dense> d_emptyBuffer;
        private MemoryBuffer1D<double, Stride1D.Dense> d_errorEstimate;

        // RK45 Dormand-Prince系数
        private readonly double[] c = { 0, 1.0 / 5.0, 3.0 / 10.0, 4.0 / 5.0, 8.0 / 9.0, 1.0, 1.0 };
        private readonly double[,] a = new double[,]
        {
            { 0, 0, 0, 0, 0, 0 },
            { 1.0/5.0, 0, 0, 0, 0, 0 },
            { 3.0/40.0, 9.0/40.0, 0, 0, 0, 0 },
            { 44.0/45.0, -56.0/15.0, 32.0/9.0, 0, 0, 0 },
            { 19372.0/6561.0, -25360.0/2187.0, 64448.0/6561.0, -212.0/729.0, 0, 0 },
            { 9017.0/3168.0, -355.0/33.0, 46732.0/5247.0, 49.0/176.0, -5103.0/18656.0, 0 },
            { 35.0/384.0, 0, 500.0/1113.0, 125.0/192.0, -2187.0/6784.0, 11.0/84.0 }
        };
        private readonly double[] b5 = { 35.0 / 384.0, 0, 500.0 / 1113.0, 125.0 / 192.0, -2187.0 / 6784.0, 11.0 / 84.0, 0 };
        private readonly double[] b4 = { 5179.0 / 57600.0, 0, 7571.0 / 16695.0, 393.0 / 640.0, -92097.0 / 339200.0, 187.0 / 2100.0, 1.0 / 40.0 };

        // GPU设备内存中的系数
        private MemoryBuffer1D<double, Stride1D.Dense> d_a_flattened;
        private MemoryBuffer1D<double, Stride1D.Dense> d_b4;
        private MemoryBuffer1D<double, Stride1D.Dense> d_b5;

        public GPURK45(ODE.ODEsys system)
        {
            // 初始化 ILGPU 
            Context context = Context.CreateDefault();
            Accelerator accelerator = null;
            foreach (Device d in context.GetCudaDevices())//先选择CUDA
            {
                accelerator = d.CreateAccelerator(context);
                //Console.WriteLine(accelerator);
            }
            if (accelerator == null)
            {
                foreach (Device d in context.GetCLDevices())//如果没有CUDA设备，则选择OpenCL，但是集显可能不支持float64
                {
                    accelerator = d.CreateAccelerator(context);
                    //Console.WriteLine(accelerator);
                }
            }
            if (accelerator == null)
            {
                Console.WriteLine("没有可用的 GPU 设备。");
                throw new InvalidOperationException("没有可用的 GPU 设备。");
            }
            this.context = context;
            this.accelerator = accelerator;

            gpuData = ODE.GPU_ODE_Data.PrepareGPUData(system);

            // 加载计算浓度变化率的核函数
            calculateRatesKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D,
                ArrayView<double>, ArrayView<double>, ArrayView<double>,
                ArrayView<int>, ArrayView<int>, ArrayView<int>, ArrayView<int>,
                ArrayView<int>, ArrayView<int>, ArrayView<double>, int, int, double>(CalculateRatesKernel);

            // 加载临时状态计算核函数
            computeTempStateKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D,
                ArrayView<double>, ArrayView<double>, ArrayView<double>,
                ArrayView<double>, ArrayView<double>, ArrayView<double>,
                ArrayView<double>, ArrayView<double>, double, int, int>(ComputeTempStateKernel);

            //// 加载误差估计核函数
            //errorEstimationKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D,
            //    ArrayView<double>, ArrayView<double>, ArrayView<double>,
            //    double, int>(ErrorEstimationKernel);

            // 在GPU上分配内存
            d_termCoefficients = accelerator.Allocate1D(gpuData.TermCoefficients);
            d_termFactorsStart = accelerator.Allocate1D(gpuData.TermFactorsStart);
            d_termFactorsLength = accelerator.Allocate1D(gpuData.TermFactorsLength);
            d_factorSubstanceIds = accelerator.Allocate1D(gpuData.FactorSubstanceIds);
            d_factorExponents = accelerator.Allocate1D(gpuData.FactorExponents);
            d_equationTermStart = accelerator.Allocate1D(gpuData.EquationTermStart);
            d_equationTermCount = accelerator.Allocate1D(gpuData.EquationTermCount);

            // 为浓度和k值分配内存
            d_concentrations = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_k1 = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_k2 = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_k3 = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_k4 = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_k5 = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_k6 = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_k7 = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_tempState = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_emptyBuffer = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_errorEstimate = accelerator.Allocate1D<double>(gpuData.SubstanceCount);

            // 将Butcher表系数转换为一维数组并分配GPU内存
            double[] a_flattened = new double[7 * 6];
            for (int i = 0; i < 7; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    a_flattened[i * 6 + j] = a[i, j];
                }
            }
            d_a_flattened = accelerator.Allocate1D(a_flattened);
            d_b4 = accelerator.Allocate1D(b4);
            d_b5 = accelerator.Allocate1D(b5);
        }

        // 计算浓度变化率的核函数（与RK4相同）
        static void CalculateRatesKernel(Index1D index,
                                       ArrayView<double> concentrations,
                                       ArrayView<double> rates,
                                       ArrayView<double> termCoefficients,
                                       ArrayView<int> termFactorsStart,
                                       ArrayView<int> termFactorsLength,
                                       ArrayView<int> factorSubstanceIds,
                                       ArrayView<int> factorExponents,
                                       ArrayView<int> equationTermStart,
                                       ArrayView<int> equationTermCount,
                                       ArrayView<double> tempState,
                                       int substanceCount,
                                       int calculationType,
                                       double stepSize)
        {
            // Grid-stride循环，每个物质并行计算
            for (int substanceIdx = index; substanceIdx < substanceCount; substanceIdx += GridExtensions.GridStrideLoopStride)
            {
                // 获得当前物质的方程项起始位置和数量
                int termStart = equationTermStart[substanceIdx];
                int termCount = equationTermCount[substanceIdx];

                double rate = 0.0;

                // 计算每个方程项的系数加和
                for (int termIdx = 0; termIdx < termCount; termIdx++)
                {
                    int currentTermIdx = termStart + termIdx;
                    double coefficient = termCoefficients[currentTermIdx];
                    int factorStart = termFactorsStart[currentTermIdx];
                    int factorCount = termFactorsLength[currentTermIdx];

                    double termValue = coefficient;

                    // 计算产物浓度乘积
                    for (int factorIdx = 0; factorIdx < factorCount; factorIdx++)
                    {
                        int currentFactorIdx = factorStart + factorIdx;
                        int substanceId = factorSubstanceIds[currentFactorIdx];
                        int exponent = factorExponents[currentFactorIdx];

                        // 当前物质的浓度
                        double concentration = 0.0;

                        if (calculationType == 0)
                        {
                            //算k1使用初始浓度
                            concentration = concentrations[substanceId];
                        }
                        else
                        {
                            //算k2-k7使用临时浓度
                            concentration = tempState[substanceId];
                        }

                        // 计算浓度的指数
                        double power = 1.0;
                        for (int i = 0; i < exponent; i++)
                        {
                            power *= concentration;
                        }

                        termValue *= power;
                    }

                    rate += termValue;
                }

                rates[substanceIdx] = rate;
            }
        }
        // 计算RK45通用临时状态的核函数
        static void ComputeTempStateKernel(
            Index1D index,
            ArrayView<double> originalState,    // 原始状态y_n
            ArrayView<double> tempState,        // 临时状态结果
            ArrayView<double> k1,               // 已计算的k1值
            ArrayView<double> k2,               // 已计算的k2值（可能为null）
            ArrayView<double> k3,               // 已计算的k3值（可能为null）
            ArrayView<double> k4,               // 已计算的k4值（可能为null）
            ArrayView<double> k5,               // 已计算的k5值（可能为null）
            ArrayView<double> a,                // Butcher表中的系数（展平为一维数组）
            double dt,                          // 步长
            int stage,                          // 当前阶段(1-6)
            int count)                         // 物质数量
        {
            for (int i = index; i < count; i += GridExtensions.GridStrideLoopStride)
            {
                tempState[i] = originalState[i];

                // 根据当前阶段应用系数
                if (stage >= 1) tempState[i] += dt * a[(stage) * 6 + 0] * k1[i];
                if (stage >= 2) tempState[i] += dt * a[(stage ) * 6 + 1] * k2[i];
                if (stage >= 3) tempState[i] += dt * a[(stage) * 6 + 2] * k3[i];
                if (stage >= 4) tempState[i] += dt * a[(stage) * 6 + 3] * k4[i];
                if (stage >= 5) tempState[i] += dt * a[(stage) * 6 + 4] * k5[i];
            }
        }

        // 求解RK45方法
        public (List<double>, List<double[]>) Solve(double[] initialConcentrations, double tStart, double tEnd, double initialStep = 0.01, double tol = 1e-6, double safetyFactor = 0.8)
        {
            List<double[]> results = new List<double[]>();
            List<double> timePoints = new List<double>();

            // 将初始浓度复制到GPU
            d_concentrations.CopyFromCPU(initialConcentrations);

            // 复制初始状态到结果数组
            double[] currentState = d_concentrations.GetAsArray1D();
            results.Add((double[])currentState.Clone());
            timePoints.Add(tStart);

            double t = tStart;
            double dt = initialStep;

            // 主循环
            while (t < tEnd)
            {
                

                bool stepAccepted = false;
                double error = 0.0;

                // 计算新的状态和误差，直到步长被接受
                while (!stepAccepted)
                {
                    // 1. 计算k1 = f(y_n)
                    calculateRatesKernel(gpuData.SubstanceCount,
                        d_concentrations.View, d_k1.View,
                        d_termCoefficients.View, d_termFactorsStart.View, d_termFactorsLength.View,
                        d_factorSubstanceIds.View, d_factorExponents.View, d_equationTermStart.View,
                        d_equationTermCount.View, d_concentrations.View, gpuData.SubstanceCount, 0, dt);
                    //Console.WriteLine("k1计算完成");
                    // 2. 计算k2 = f(y_n + a21*k1)
                    // 计算临时状态 y_n + dt * a21 * k1
                    //accelerator.Synchronize();


                    computeTempStateKernel(gpuData.SubstanceCount,
                        d_concentrations.View, d_tempState.View,
                        d_k1.View, d_k2.View, d_k3.View, d_k4.View, d_k5.View,
                        d_a_flattened.View, dt, 1, gpuData.SubstanceCount);
                    // 使用临时状态计算k2
                    calculateRatesKernel(gpuData.SubstanceCount,
                        d_concentrations.View, d_k2.View,
                        d_termCoefficients.View, d_termFactorsStart.View, d_termFactorsLength.View,
                        d_factorSubstanceIds.View, d_factorExponents.View, d_equationTermStart.View,
                        d_equationTermCount.View, d_tempState.View, gpuData.SubstanceCount, 1, dt);
                    //Console.WriteLine("k2计算完成");

                    computeTempStateKernel(gpuData.SubstanceCount,
                        d_concentrations.View, d_tempState.View,
                        d_k1.View, d_k2.View, d_k3.View, d_k4.View, d_k5.View,
                        d_a_flattened.View, dt, 2, gpuData.SubstanceCount);

                    calculateRatesKernel(gpuData.SubstanceCount,
                        d_concentrations.View, d_k3.View,
                        d_termCoefficients.View, d_termFactorsStart.View, d_termFactorsLength.View,
                        d_factorSubstanceIds.View, d_factorExponents.View, d_equationTermStart.View,
                        d_equationTermCount.View, d_tempState.View, gpuData.SubstanceCount, 1, dt);


                    computeTempStateKernel(gpuData.SubstanceCount,
                        d_concentrations.View, d_tempState.View,
                        d_k1.View, d_k2.View, d_k3.View, d_k4.View, d_k5.View,
                        d_a_flattened.View, dt, 3, gpuData.SubstanceCount);


                    calculateRatesKernel(gpuData.SubstanceCount,
                        d_concentrations.View, d_k4.View,
                        d_termCoefficients.View, d_termFactorsStart.View, d_termFactorsLength.View,
                        d_factorSubstanceIds.View, d_factorExponents.View, d_equationTermStart.View,
                        d_equationTermCount.View, d_tempState.View, gpuData.SubstanceCount, 1, dt);

                    computeTempStateKernel(gpuData.SubstanceCount,
                        d_concentrations.View, d_tempState.View,
                        d_k1.View, d_k2.View, d_k3.View, d_k4.View, d_k5.View,
                        d_a_flattened.View, dt, 4, gpuData.SubstanceCount);

                    calculateRatesKernel(gpuData.SubstanceCount,
                        d_concentrations.View, d_k5.View,
                        d_termCoefficients.View, d_termFactorsStart.View, d_termFactorsLength.View,
                        d_factorSubstanceIds.View, d_factorExponents.View, d_equationTermStart.View,
                        d_equationTermCount.View, d_tempState.View, gpuData.SubstanceCount, 1, dt);


                    computeTempStateKernel(gpuData.SubstanceCount,
                        d_concentrations.View, d_tempState.View,
                        d_k1.View, d_k2.View, d_k3.View, d_k4.View, d_k5.View,
                        d_a_flattened.View, dt, 5, gpuData.SubstanceCount);

                    calculateRatesKernel(gpuData.SubstanceCount,
                        d_concentrations.View, d_k6.View,
                        d_termCoefficients.View, d_termFactorsStart.View, d_termFactorsLength.View,
                        d_factorSubstanceIds.View, d_factorExponents.View, d_equationTermStart.View,
                        d_equationTermCount.View, d_tempState.View, gpuData.SubstanceCount, 1, dt);

                    // 计算5阶和4阶解并估计误差
                    double[] yNext = new double[gpuData.SubstanceCount];
                    double[] yErr = new double[gpuData.SubstanceCount];

                    // 使用CPU计算
                    double[] k1 = d_k1.GetAsArray1D();
                    double[] k2 = d_k2.GetAsArray1D();
                    double[] k3 = d_k3.GetAsArray1D();
                    double[] k4 = d_k4.GetAsArray1D();
                    double[] k5 = d_k5.GetAsArray1D();
                    double[] k6 = d_k6.GetAsArray1D();

                    // 计算最大误差
                    error = 0.0;
                    for (int i = 0; i < gpuData.SubstanceCount; i++)
                    {
                        double yerr = dt * (
                            (b5[0] - b4[0]) * k1[i] +
                            (b5[1] - b4[1]) * k2[i] +
                            (b5[2] - b4[2]) * k3[i] +
                            (b5[3] - b4[3]) * k4[i] +
                            (b5[4] - b4[4]) * k5[i] +
                            (b5[5] - b4[5]) * k6[i] -
                            b4[6] * 0  // k7=0
                        );

                        yErr[i] = Math.Abs(yerr);
                        double relErr = yErr[i] / (Math.Abs(currentState[i]) + 1e-10);
                        error = Math.Max(error, relErr);

                        yNext[i] = currentState[i] + dt * (
                            b5[0] * k1[i] + b5[1] * k2[i] + b5[2] * k3[i] +
                            b5[3] * k4[i] + b5[4] * k5[i] + b5[5] * k6[i]
                        );
                    }


                    //Console.WriteLine($"误差计算完成{error}");
                    double newStep = dt;
                    if (error > 0)
                    {
                        newStep = safetyFactor * dt * Math.Pow(tol / error, 0.2);
                        newStep = Math.Max(newStep, 0.1 * dt);
                        newStep = Math.Min(newStep, 5.0 * dt);
                    }
                    dt = newStep;
                    stepAccepted = true;
                    // 更新状态
                    currentState = yNext;
                    // 调整dt以确保最后一步不会超出范围
                    if (t + dt > tEnd)
                        dt = tEnd - t;

                    t += dt;
                    //Console.WriteLine($"当前时间: {t}");
                    // 复制当前状态到结果数组
                    results.Add((double[])currentState.Clone());
                    timePoints.Add(t);

                    // 将更新后的状态复制回GPU
                    d_concentrations.CopyFromCPU(currentState);

                }
            }

            return (timePoints, results);
        }

        // 清理资源
        public void Dispose()
        {
            d_termCoefficients?.Dispose();
            d_termFactorsStart?.Dispose();
            d_termFactorsLength?.Dispose();
            d_factorSubstanceIds?.Dispose();
            d_factorExponents?.Dispose();
            d_equationTermStart?.Dispose();
            d_equationTermCount?.Dispose();
            d_concentrations?.Dispose();
            d_k1?.Dispose();
            d_k2?.Dispose();
            d_k3?.Dispose();
            d_k4?.Dispose();
            d_k5?.Dispose();
            d_k6?.Dispose();
            d_k7?.Dispose();
            d_tempState?.Dispose();
            d_errorEstimate?.Dispose();
            d_a_flattened?.Dispose();
            d_b4?.Dispose();
            d_b5?.Dispose();
            accelerator?.Dispose();
            context?.Dispose();
        }
    }
}
