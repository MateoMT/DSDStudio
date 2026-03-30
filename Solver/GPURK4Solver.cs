using ILGPU.Runtime;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime.OpenCL;
using ILGPU.Runtime.Cuda;

namespace Solver
{
    public class GPURK4Solver : IDisposable
    {
        private Context context;
        private Accelerator accelerator;
        private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>,
                      ArrayView<int>, ArrayView<int>, ArrayView<int>, ArrayView<int>,
                      ArrayView<int>, ArrayView<int>, ArrayView<double>, int, int, double> calculateRatesKernel;
        private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>, double, int> tempStateKernel;
        private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>,
                      ArrayView<double>, ArrayView<double>, double, int> finalUpdateKernel;

        private ODE.GPU_ODE_Data gpuData;
        private MemoryBuffer1D<double, Stride1D.Dense> d_termCoefficients;
        private MemoryBuffer1D<int, Stride1D.Dense> d_termFactorsStart;
        private MemoryBuffer1D<int, Stride1D.Dense> d_termFactorsLength;
        private MemoryBuffer1D<int, Stride1D.Dense> d_factorSubstanceIds;
        private MemoryBuffer1D<int, Stride1D.Dense> d_factorExponents;
        private MemoryBuffer1D<int, Stride1D.Dense> d_equationTermStart;
        private MemoryBuffer1D<int, Stride1D.Dense> d_equationTermCount;
        private MemoryBuffer1D<double, Stride1D.Dense> d_concentrations;
        private MemoryBuffer1D<double, Stride1D.Dense> d_k1;
        private MemoryBuffer1D<double, Stride1D.Dense> d_k2;
        private MemoryBuffer1D<double, Stride1D.Dense> d_k3;
        private MemoryBuffer1D<double, Stride1D.Dense> d_k4;
        private MemoryBuffer1D<double, Stride1D.Dense> d_tempState;

        public GPURK4Solver(ODE.ODEsys system)
        {
            // 初始化 ILGPU 
            Context context = Context.CreateDefault();
            Accelerator accelerator = null;
            foreach (Device d in context.GetCudaDevices())//先选择CUDA
            {
                accelerator = d.CreateAccelerator(context);
                Console.WriteLine(accelerator);
            }
            if (accelerator == null)
            {
                foreach (Device d in context.GetCLDevices())//如果没有CUDA设备，则选择OpenCL，但是集显可能不支持float64
                {
                    accelerator = d.CreateAccelerator(context);
                    Console.WriteLine(accelerator);
                }
            }
            if (accelerator == null)
            {
                Console.WriteLine("没有可用的 GPU 设备。");
                throw new InvalidOperationException("没有可用的 GPU 设备。");
            }
            gpuData = ODE.GPU_ODE_Data.PrepareGPUData(system);
            // 加载核函数
            calculateRatesKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D,
                ArrayView<double>, ArrayView<double>, ArrayView<double>,
                ArrayView<int>, ArrayView<int>, ArrayView<int>, ArrayView<int>,
                ArrayView<int>, ArrayView<int>, ArrayView<double>, int, int, double>(CalculateRatesKernel);

            tempStateKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D,
                ArrayView<double>, ArrayView<double>, ArrayView<double>, double, int>(
                (index, original, temp, k, factor, count) =>
                {
                    for (int i = index; i < count; i += GridExtensions.GridStrideLoopStride)
                    {
                        temp[i] = original[i] + k[i] * factor;
                    }
                });

            finalUpdateKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D,
                ArrayView<double>, ArrayView<double>, ArrayView<double>,
                ArrayView<double>, ArrayView<double>, double, int>(
                (index, y, k1, k2, k3, k4, dt, count) =>
                {
                    for (int i = index; i < count; i += GridExtensions.GridStrideLoopStride)
                    {
                        y[i] = y[i] + (dt / 6.0) * (k1[i] + 2 * k2[i] + 2 * k3[i] + k4[i]);
                    }
                });

            // 分配 GPU 内存
            d_termCoefficients = accelerator.Allocate1D(gpuData.TermCoefficients);
            d_termFactorsStart = accelerator.Allocate1D(gpuData.TermFactorsStart);
            d_termFactorsLength = accelerator.Allocate1D(gpuData.TermFactorsLength);
            d_factorSubstanceIds = accelerator.Allocate1D(gpuData.FactorSubstanceIds);
            d_factorExponents = accelerator.Allocate1D(gpuData.FactorExponents);
            d_equationTermStart = accelerator.Allocate1D(gpuData.EquationTermStart);
            d_equationTermCount = accelerator.Allocate1D(gpuData.EquationTermCount);

            // 为浓度、k1、k2、k3、k4和临时状态分配内存
            d_concentrations = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_k1 = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_k2 = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_k3 = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_k4 = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
            d_tempState = accelerator.Allocate1D<double>(gpuData.SubstanceCount);
        }

        // 计算浓度变化率的核函数
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
                            //算k2,k3,k4使用临时浓度
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

        // 求解RK4
        public double[,] Solve(double[] initialConcentrations, double tStart, double tEnd, double dt)
        {
            // 将初始浓度复制到GPU
            d_concentrations.CopyFromCPU(initialConcentrations);

            double t = tStart;

            // 计算总步数
            int totalSteps = (int)Math.Ceiling((tEnd - tStart) / dt) + 1;

            // 创建结果数组：[时间步数, 物质数量]
            double[,] results = new double[totalSteps, gpuData.SubstanceCount];

            // 复制初始状态到结果数组
            double[] currentState = d_concentrations.GetAsArray1D();
            for (int i = 0; i < gpuData.SubstanceCount; i++)
            {
                results[0, i] = currentState[i];
            }

            int stepIndex = 1;
            Console.WriteLine($"初始浓度: {string.Join(", ", initialConcentrations)}");
            // 主循环
            while (t + dt < tEnd)
            {
                // 调整dt以确保最后一步不会超出范围
                if (t + dt > tEnd)
                    dt = tEnd - t;

                // 1. 计算 k1 = f(y_n)
                calculateRatesKernel(gpuData.SubstanceCount,
                    d_concentrations.View, d_k1.View,
                    d_termCoefficients.View, d_termFactorsStart.View, d_termFactorsLength.View,
                    d_factorSubstanceIds.View, d_factorExponents.View, d_equationTermStart.View,
                    d_equationTermCount.View, d_concentrations.View, gpuData.SubstanceCount, 0, dt);
                Console.WriteLine($"k1计算完成");
                // 2. 计算 k2 = f(y_n + dt/2 * k1)
                tempStateKernel(gpuData.SubstanceCount,
                    d_concentrations.View, d_tempState.View, d_k1.View, 0.5 * dt, gpuData.SubstanceCount);

                calculateRatesKernel(gpuData.SubstanceCount,
                    d_concentrations.View, d_k2.View,
                    d_termCoefficients.View, d_termFactorsStart.View, d_termFactorsLength.View,
                    d_factorSubstanceIds.View, d_factorExponents.View, d_equationTermStart.View,
                    d_equationTermCount.View, d_tempState.View, gpuData.SubstanceCount, 1, dt);

                // 3.计算 k3 = f(y_n + dt/2 * k2)
                tempStateKernel(gpuData.SubstanceCount,
                    d_concentrations.View, d_tempState.View, d_k2.View, 0.5 * dt, gpuData.SubstanceCount);

                calculateRatesKernel(gpuData.SubstanceCount,
                    d_concentrations.View, d_k3.View,
                    d_termCoefficients.View, d_termFactorsStart.View, d_termFactorsLength.View,
                    d_factorSubstanceIds.View, d_factorExponents.View, d_equationTermStart.View,
                    d_equationTermCount.View, d_tempState.View, gpuData.SubstanceCount, 1, dt);

                // 4.计算 k4 = f(y_n + dt * k3)
                tempStateKernel(gpuData.SubstanceCount,
                    d_concentrations.View, d_tempState.View, d_k3.View, dt, gpuData.SubstanceCount);

                calculateRatesKernel(gpuData.SubstanceCount,
                    d_concentrations.View, d_k4.View,
                    d_termCoefficients.View, d_termFactorsStart.View, d_termFactorsLength.View,
                    d_factorSubstanceIds.View, d_factorExponents.View, d_equationTermStart.View,
                    d_equationTermCount.View, d_tempState.View, gpuData.SubstanceCount, 1, dt);

                // 组合所有 k 值来计算下一个状态
                finalUpdateKernel(gpuData.SubstanceCount, d_concentrations.View,
                    d_k1.View, d_k2.View, d_k3.View, d_k4.View,
                    dt, gpuData.SubstanceCount);

                t += dt;
                Console.WriteLine($"当前时间: {t}");
                // 复制当前状态到结果数组
                currentState = d_concentrations.GetAsArray1D();
                for (int i = 0; i < gpuData.SubstanceCount; i++)
                {
                    results[stepIndex, i] = currentState[i];
                }

                stepIndex++;
            }

            // 如果实际步数小于预计步数，裁剪数组
            if (stepIndex < totalSteps)
            {
                double[,] trimmedResults = new double[stepIndex, gpuData.SubstanceCount];
                for (int i = 0; i < stepIndex; i++)
                {
                    for (int j = 0; j < gpuData.SubstanceCount; j++)
                    {
                        trimmedResults[i, j] = results[i, j];
                    }
                }
                return trimmedResults;
            }

            return results;
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
            d_tempState?.Dispose();
            accelerator?.Dispose();
            context?.Dispose();
        }
    }
}
