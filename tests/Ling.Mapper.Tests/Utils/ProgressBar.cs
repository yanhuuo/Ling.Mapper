using System;
using System.Text;
using System.Threading;

namespace TestConsole.Utils
{
    /// <summary>
    /// 命令行进度条工具
    /// </summary>
    public class ProgressBar : IDisposable
    {
        private readonly int _total;
        private readonly int _width;
        private readonly char _completeChar;
        private readonly char _incompleteChar;
        private readonly string _prefix;
        private int _current;
        private bool _disposed;
        private readonly object _lock = new object();
        private DateTime _startTime;

        /// <summary>
        /// 创建进度条
        /// </summary>
        /// <param name="total">总数</param>
        /// <param name="width">进度条宽度（字符数）</param>
        /// <param name="completeChar">已完成字符</param>
        /// <param name="incompleteChar">未完成字符</param>
        /// <param name="prefix">前缀文本</param>
        public ProgressBar(
            int total, 
            int width = 50, 
            char completeChar = '█', 
            char incompleteChar = '░',
            string prefix = "")
        {
            _total = total;
            _width = width;
            _completeChar = completeChar;
            _incompleteChar = incompleteChar;
            _prefix = prefix;
            _current = 0;
            _startTime = DateTime.Now;
        }

        /// <summary>
        /// 更新进度
        /// </summary>
        /// <param name="current">当前进度</param>
        public void Update(int current)
        {
            lock (_lock)
            {
                _current = current;
                Render();
            }
        }

        /// <summary>
        /// 增加进度
        /// </summary>
        /// <param name="increment">增加的数量</param>
        public void Increment(int increment = 1)
        {
            lock (_lock)
            {
                _current += increment;
                Render();
            }
        }

        /// <summary>
        /// 渲染进度条
        /// </summary>
        private void Render()
        {
            if (_disposed) return;

            var percentage = (double)_current / _total;
            var completed = (int)(percentage * _width);
            var remaining = _width - completed;

            var elapsed = DateTime.Now - _startTime;
            var speed = _current / elapsed.TotalSeconds;
            var eta = _current > 0 ? TimeSpan.FromSeconds((_total - _current) / speed) : TimeSpan.Zero;

            var sb = new StringBuilder();
            
            // 清除当前行
            sb.Append('\r');
            
            // 前缀
            if (!string.IsNullOrEmpty(_prefix))
            {
                sb.Append(_prefix);
                sb.Append(' ');
            }

            // 进度条
            sb.Append('[');
            sb.Append(_completeChar, completed);
            sb.Append(_incompleteChar, remaining);
            sb.Append(']');

            // 百分比
            sb.Append($" {percentage:P0}");

            // 进度数字
            sb.Append($" ({_current:N0}/{_total:N0})");

            // 速度
            sb.Append($" | {speed:N0} ops/s");

            // 剩余时间
            if (_current > 0 && _current < _total)
            {
                sb.Append($" | ETA: {FormatTimeSpan(eta)}");
            }

            Console.Write(sb.ToString());

            // 完成时换行
            if (_current >= _total)
            {
                Console.WriteLine();
            }
        }

        /// <summary>
        /// 格式化时间跨度
        /// </summary>
        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalSeconds < 1)
                return "< 1s";
            else if (ts.TotalSeconds < 60)
                return $"{ts.TotalSeconds:F0}s";
            else if (ts.TotalMinutes < 60)
                return $"{ts.TotalMinutes:F1}m";
            else
                return $"{ts.TotalHours:F1}h";
        }

        /// <summary>
        /// 完成进度条
        /// </summary>
        public void Finish()
        {
            Update(_total);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Finish();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 进度条扩展方法
    /// </summary>
    public static class ProgressBarExtensions
    {
        /// <summary>
        /// 报告进度（每 N 次更新一次，避免过于频繁）
        /// </summary>
        public static void ReportProgress(this ProgressBar progressBar, int current, int updateInterval = 1000)
        {
            if (current % updateInterval == 0 || current == 1)
            {
                progressBar.Update(current);
            }
        }
    }
}
