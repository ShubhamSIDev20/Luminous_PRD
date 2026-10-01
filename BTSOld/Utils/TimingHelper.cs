using System.Diagnostics;

namespace BatteryTestingSystem.Utils
{
    public static class TimingHelper
    {
        public static async Task MeasureAsync(string name, Func<Task> action)
        {
            var sw = Stopwatch.StartNew();

            await action();

            sw.Stop();
            if (sw.ElapsedMilliseconds > 0)
                Console.WriteLine($"{name} took {sw.ElapsedMilliseconds} ms");
        }
    }
}
