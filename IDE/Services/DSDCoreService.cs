using System.Collections.Generic;
using System.Text;
using DSDCore;
using ODE;
using PaintUtils;

namespace IDE.Services
{
    public interface IDSDCoreService
    {
        (bool Success, string ErrorMessage) Compile(string codeContent);

        string GetErrorMessage();


        List<double[]> Simulate();
        (List<double>, List<double[]>) SimulateWithTime();

        List<SvgGenerator> GetReactionSvgs();


        ODEsys GetODESystem();
    }

    public class DSDCoreService : IDSDCoreService
    {
        private DSDCore.DSDCore _dsdCore;
        private string _lastErrorMessage;

        public (bool Success, string ErrorMessage) Compile(string codeContent)
        {
            try
            {
                _dsdCore = new DSDCore.DSDCore(codeContent);
                var errors = _dsdCore.GetErrors();

                if (errors != null && !errors.Success)
                {
                    StringBuilder errorBuilder = new StringBuilder();

                    foreach (var error in errors.ErrorsList)
                    {
                        errorBuilder.AppendLine($"行 {error.line}, 列 {error.column}: {error.message}");
                    }

                    _lastErrorMessage = errorBuilder.ToString();
                    return (false, _lastErrorMessage);
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"编译过程中出现异常: {ex.Message}";
                return (false, _lastErrorMessage);
            }
        }

        public string GetErrorMessage()
        {
            return _lastErrorMessage;
        }

        public List<double[]> Simulate()
        {
            if (_dsdCore == null)
            {
                _lastErrorMessage = "请先编译代码";
                return null;
            }
            try
            {
                return _dsdCore.Solve();
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"模拟过程中出现异常: {ex.Message}";
                return null;
            }
        }

        public (List<double>, List<double[]>) SimulateWithTime()
        {
            if (_dsdCore == null)
            {
                _lastErrorMessage = "请先编译代码";
                return (null, null);
            }
            try
            {
                return _dsdCore.SolveWithTime();
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"模拟过程中出现异常: {ex.Message}";
                return (null, null);
            }
        }
        public List<SvgGenerator> GetReactionSvgs()
        {
            if (_dsdCore == null)
            {
                _lastErrorMessage = "请先编译代码";
                return null;
            }

            try
            {
                return _dsdCore.GetSvgs();
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"获取反应图形过程中出现异常: {ex.Message}";
                return null;
            }
        }

        public ODEsys GetODESystem()
        {
            if (_dsdCore == null)
            {
                _lastErrorMessage = "请先编译代码";
                return null;
            }

            try
            {
                return _dsdCore.GetODEsys();
            }
            catch (Exception ex)
            {
                _lastErrorMessage = $"获取微分方程系统过程中出现异常: {ex.Message}";
                return null;
            }
        }
    }
}
