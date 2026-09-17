using System;

namespace BatchPlotPlus.AutoCAD
{
    internal static class TemporarySetting
    {
        internal static void Run(Func<object> read, Action<object> write, object temporary, Action action, Action<string> report)
        {
            var original = read();
            Exception? operationError = null;
            try
            {
                write(temporary);
                if (!Equals(read(), temporary)) throw new InvalidOperationException("Temporary setting readback failed.");
                action();
            }
            catch (Exception exception) { operationError = exception; throw; }
            finally
            {
                try
                {
                    var current = read();
                    if (!Equals(current, original))
                    {
                        if (!Equals(current, temporary)) throw new InvalidOperationException("Setting changed concurrently; preserved current value.");
                        write(original);
                    }
                    if (!Equals(read(), original)) throw new InvalidOperationException("Setting restoration readback failed.");
                }
                catch (Exception restoreError)
                {
                    report("Setting restoration failed: " + restoreError.Message);
                    if (operationError == null) throw;
                    // Keep the original failure; retain restoration failure as evidence.
                    operationError.Data["RestorationFailure"] = restoreError.ToString();
                }
            }
        }
    }
}
