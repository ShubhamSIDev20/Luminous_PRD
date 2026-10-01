using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Models.SqliteEntities;

namespace BatteryTestingSystem.Components.UI.DataViewer
{
    // BmsDummyData.cs
    // Usage: var (data, steps) = BmsDummyData.Generate();
    public static class BmsDummyData
    {
        public static (List<MeasurementData> Data, List<StepModel> Steps) Generate(int rowCount = 500)
        {
            var steps = GenerateSteps();
            var data = GenerateMeasurements(rowCount, steps);
            return (data, steps);
        }

        // ─── Steps ───────────────────────────────────────────────────────────────

        public static List<StepModel> GenerateSteps() =>
        [
            new StepModel
        {
            StepNumber    = 1,
            OperatorCode  = 0x01,
            Label         = "Rest",
            Comment       = "Initial rest before test",
            NominalValues = ["0 A", "25°C"],
            Limits        = ["T ≤ 45°C", "V ≥ 2.5V"],
            Actions       = ["Log temp", "Log voltage"],
            Registrations = ["Voltage", "Temperature"],
        },
        new StepModel
        {
            StepNumber    = 2,
            OperatorCode  = 0x02,
            Label         = "CC Charge",
            Comment       = "Constant current charge phase",
            NominalValues = ["10 A", "54.6V cutoff"],
            Limits        = ["I ≤ 12A", "V ≤ 54.6V", "T ≤ 45°C"],
            Actions       = ["Stop on V limit", "Stop on T limit"],
            Registrations = ["Voltage", "Current", "Temperature", "Power"],
        },
        new StepModel
        {
            StepNumber    = 3,
            OperatorCode  = 0x03,
            Label         = "CV Charge",
            Comment       = "Constant voltage taper",
            NominalValues = ["54.6V", "I cutoff 0.5A"],
            Limits        = ["I ≥ 0.5A", "T ≤ 45°C"],
            Actions       = ["Stop on I limit"],
            Registrations = ["Voltage", "Current", "ChargeCapacity", "ChargeEnergy"],
        },
        new StepModel
        {
            StepNumber    = 4,
            OperatorCode  = 0x04,
            Label         = "Rest",
            Comment       = "Post-charge rest",
            NominalValues = ["0 A"],
            Limits        = ["T ≤ 40°C"],
            Actions       = ["Log capacity"],
            Registrations = ["Voltage", "Temperature"],
        },
        new StepModel
        {
            StepNumber    = 5,
            OperatorCode  = 0x05,
            Label         = "CC Discharge",
            Comment       = "Constant current discharge",
            NominalValues = ["10 A", "40V cutoff"],
            Limits        = ["V ≥ 40V", "T ≤ 50°C"],
            Actions       = ["Stop on V limit", "Stop on T limit"],
            Registrations = ["Voltage", "Current", "DischargeCapacity", "DischargeEnergy"],
        },
        new StepModel
        {
            StepNumber    = 6,
            OperatorCode  = 0x06,
            Label         = "End",
            Comment       = "Test complete",
            NominalValues = [],
            Limits        = [],
            Actions       = ["Save log", "Send report"],
            Registrations = ["AccumulatedCapacity", "AccumulatedEnergy"],
        },
    ];

        // ─── Measurements ────────────────────────────────────────────────────────

        public static List<MeasurementData> GenerateMeasurements(int count, List<StepModel> steps)
        {
            var rng = new Random(42);
            var list = new List<MeasurementData>(count);
            var start = DateTime.Now.AddMinutes(-count);

            // How many rows per step (distribute evenly-ish)
            var stepCount = steps.Count;
            var rowsPerStep = count / stepCount;

            long id = 1;
            int runTime = 0;

            for (int s = 0; s < stepCount; s++)
            {
                var step = steps[s];
                var rows = (s == stepCount - 1) ? count - s * rowsPerStep : rowsPerStep;
                var phase = (double)s / stepCount; // 0..1 across test

                // Base values per step
                var baseV = step.StepNumber switch
                {
                    1 => 48.0,           // resting
                    2 => 48.0 + phase * 6, // charging up
                    3 => 54.0,           // CV hold
                    4 => 54.5,           // resting full
                    5 => 54.5 - phase * 14, // discharging
                    6 => 40.5,
                    _ => 48.0,
                };

                var baseA = step.StepNumber switch
                {
                    1 => 0.0,
                    2 => 10.0,
                    3 => 10.0 * (1 - (double)(s * rowsPerStep) / count),  // taper
                    4 => 0.0,
                    5 => -10.0,
                    6 => 0.0,
                    _ => 0.0,
                };

                double accumAh = list.Count > 0 ? (double)(list[^1].AccumulatedCapacity ?? 0) : 0;
                double accumWh = list.Count > 0 ? (double)(list[^1].AccumulatedEnergy ?? 0) : 0;
                double chaAh = list.Count > 0 ? (double)(list[^1].ChargeCapacity ?? 0) : 0;
                double dchAh = list.Count > 0 ? (double)(list[^1].DischargeCapacity ?? 0) : 0;
                double stepAh = 0;

                for (int i = 0; i < rows; i++)
                {
                    var t = start.AddMinutes(s * rowsPerStep + i);
                    var frac = (double)i / rows;

                    var v = (float)(baseV + Math.Sin(frac * Math.PI * 4) * 0.3 + Jitter(rng, 0.05));
                    var a = (float)(baseA + Math.Sin(frac * Math.PI * 6) * 0.5 + Jitter(rng, 0.2));

                    // CV taper: current decreases as voltage holds
                    if (step.StepNumber == 3)
                        a = (float)(9.5 * (1 - frac) + Jitter(rng, 0.1));

                    var temp = (float)(25 + Math.Sin(frac * Math.PI * 2) * 4 + phase * 6 + Jitter(rng, 0.3));
                    var power = v * a;

                    // Integrate capacity (Ah) every ~1 min interval
                    var dAh = Math.Abs((double)a) / 60.0;
                    stepAh += dAh;
                    accumAh += dAh;
                    if (a > 0) chaAh += dAh; else dchAh += dAh;
                    accumWh += Math.Abs((double)power) / 60.0;

                    var hasError = rng.NextDouble() < 0.01; // 1% error chance

                    list.Add(new MeasurementData
                    {
                        Id = id++,
                        SessionID = 1001,
                        DeviceId = 1,
                        ChannelId = 1,
                        StepNumber = step.StepNumber,
                        Operator = step.OperatorCode,
                        CircuitStatus = step.StepNumber == 6 ? CircuitStatus.Charge : CircuitStatus.Pause,
                        DateTime = t,
                        ProgramRunningTime = runTime++,
                        Current = a,
                        Voltage = v,
                        Temperature = temp,
                        Power = power,
                        AccumulatedCapacity = (float)accumAh,
                        ChargeCapacity = (float)chaAh,
                        DischargeCapacity = (float)dchAh,
                        StepCapacity = (float)stepAh,
                        AccumulatedEnergy = (float)accumWh,
                        ChargeEnergy = (float)(a > 0 ? accumWh * 0.6 : 0),
                        DischargeEnergy = (float)(a < 0 ? accumWh * 0.4 : 0),
                        StepEnergy = (float)(Math.Abs((double)power) * stepAh / 60.0),
                        SystemErrorID = hasError ? 0xE1 : null,
                        ErrorId = hasError ? 1 : null,
                        MessageId = null,
                    });
                }
            }

            return list;
        }

        private static double Jitter(Random rng, double scale) =>
            (rng.NextDouble() - 0.5) * 2 * scale;
    }
}
