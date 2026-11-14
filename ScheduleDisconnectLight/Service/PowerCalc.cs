using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Service
{
    /// <summary>Стан напруги (мережа / вихід інвертора)</summary>
    public enum VoltageState
    {
        /// <summary>Немає даних для висновку</summary>
        Unknown,
        /// <summary>Напруга в нормі</summary>
        On,
        /// <summary>Напруги немає</summary>
        Off,
        /// <summary>Проміжні значення (між "є" і "немає"). Статус при цьому НЕ змінюємо.</summary>
        Unstable
    }

    /// <summary>Режим батареї. Числа - ті, що пишемо в таблицю.</summary>
    public enum BatteryMode
    {
        Discharging = 0,
        Charging = 1,
        Idle = 2
    }

    /// <summary>
    /// Розрахований стан будинку на момент вимірювання.
    /// Це порт логіки дашборда (C13PowerDashboard/lib/domain.mjs), щоб таблиця і дашборд ґрунтувались на однакових правилах.
    /// </summary>
    public class PowerSnapshot
    {
        /// <summary>Час вимірювання Deye (київський)</summary>
        public DateTime? MeasuredAt;

        /// <summary>Дані застарілі або без часу - їм не можна вірити</summary>
        public bool Stale;

        public VoltageState Grid;
        /// <summary>Найбільша напруга з фаз мережі, В (0 - усі фази нульові)</summary>
        public double? GridVoltage;

        public VoltageState Output;

        /// <summary>true - генератор працює (напруга >= 150 В або потужність >= 800 Вт), null - невідомо</summary>
        public bool? GeneratorOn;
        public double? GeneratorPower;
        public double? GeneratorVoltage;

        /// <summary>Вихідна потужність інвертора на споживачів, Вт</summary>
        public double? LoadWatts;

        /// <summary>Заряд батареї, %</summary>
        public double? Soc;
        public BatteryMode? Mode;
        /// <summary>Потужність батареї, Вт, завжди додатна (напрям задає режим)</summary>
        public double? BatteryWatts;
    }

    public static class PowerCalc
    {
        /// <summary>Дані старші за цей час вважаємо застарілими (як на дашборді)</summary>
        public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

        // Мережа вважається "є", якщо напруга в цьому діапазоні й частота 45-55 Гц
        private const double GridOnMin = 180, GridOnMax = 260;
        // ... і "немає", якщо напруга нижча за це й частота < 1 Гц
        private const double GridOffMax = 50;

        // Генератор працює, якщо на вході GEN є напруга або він реально несе навантаження
        private const double GeneratorVoltageMin = 150; // В
        private const double GeneratorPowerMin = 800;   // Вт

        // Менша потужність батареї вважається "очікуванням"
        private const double BatteryIdleLimit = 70; // Вт
        // Якщо мережа є, "розряд" менший за це - власне споживання інвертора, а не реальний розряд
        private const double MainsIdleLimit = 150;  // Вт

        public static PowerSnapshot Build(DeyeReading reading, DateTime nowKyiv)
        {
            var p = reading.Points;
            var s = new PowerSnapshot { MeasuredAt = reading.MeasuredAt };

            // --- Свіжість даних ---
            s.Stale = !reading.MeasuredAt.HasValue ||
                      nowKyiv - reading.MeasuredAt.Value > StaleAfter ||
                      nowKyiv - reading.MeasuredAt.Value < TimeSpan.FromMinutes(-2);

            // --- Мережа та вихід інвертора ---
            bool three = p.ContainsKey("GridVoltageL1") || p.ContainsKey("GridVoltageL2") || p.ContainsKey("GridVoltageL3");
            s.Grid = VoltageStateOf(p, "GridVoltage", three, "GridFrequency");
            s.Output = VoltageStateOf(p, "LoadVoltage", three, null);

            var gridKeys = three ? new[] { "GridVoltageL1", "GridVoltageL2", "GridVoltageL3" } : new[] { "GridVoltageL1L2" };
            var gridValues = gridKeys.Select(k => Get(p, k)).Where(v => v.HasValue).Select(v => v.Value).ToList();
            s.GridVoltage = gridValues.Count > 0 ? gridValues.Max() : (double?)null;

            // --- Навантаження (вихід інвертора на споживачів) ---
            var load = Get(p, "UPSLoadPower") ?? Get(p, "TotalConsumptionPower");
            s.LoadWatts = load.HasValue && load.Value >= 0 ? load : null;

            // --- Генератор ---
            FillGenerator(p, s);

            // --- Батарея ---
            var soc = Get(p, "SOC");
            s.Soc = soc.HasValue && soc.Value >= 0 && soc.Value <= 100 ? soc : null;

            var batteryPower = Get(p, "BatteryPower"); // + розряд, - заряд
            if (batteryPower.HasValue)
            {
                var mode = batteryPower.Value > BatteryIdleLimit ? BatteryMode.Discharging
                         : batteryPower.Value < -BatteryIdleLimit ? BatteryMode.Charging
                         : BatteryMode.Idle;

                // Мережа є, а "розряд" малий - це власне споживання інвертора: вважаємо очікуванням
                if (s.Grid == VoltageState.On && mode == BatteryMode.Discharging && Math.Abs(batteryPower.Value) < MainsIdleLimit)
                {
                    mode = BatteryMode.Idle;
                }
                s.Mode = mode;
                s.BatteryWatts = Math.Abs(batteryPower.Value);
            }

            return s;
        }

        private static double? Get(Dictionary<string, double?> p, string key)
        {
            double? v;
            return p.TryGetValue(key, out v) ? v : null;
        }

        /// <summary>
        /// Стан напруги: "є" (180-260 В і 45-55 Гц), "немає" (&lt;50 В і &lt;1 Гц), інакше "нестабільна".
        /// Для вихідної напруги частота не перевіряється.
        /// </summary>
        private static VoltageState VoltageStateOf(Dictionary<string, double?> p, string prefix, bool three, string frequencyKey)
        {
            var keys = three ? new[] { prefix + "L1", prefix + "L2", prefix + "L3" } : new[] { prefix + "L1L2" };
            var values = keys.Select(k => Get(p, k)).ToList();
            var hz = frequencyKey != null ? Get(p, frequencyKey) : null;

            if (values.Any(v => !v.HasValue) || (frequencyKey != null && !hz.HasValue))
            {
                return VoltageState.Unknown;
            }
            if (values.All(v => v.Value >= GridOnMin && v.Value <= GridOnMax) && (frequencyKey == null || (hz.Value >= 45 && hz.Value <= 55)))
            {
                return VoltageState.On;
            }
            if (values.All(v => v.Value >= 0 && v.Value < GridOffMax) && (frequencyKey == null || (hz.Value >= 0 && hz.Value < 1)))
            {
                return VoltageState.Off;
            }
            return VoltageState.Unstable;
        }

        /// <summary>
        /// Генератор підключений до входу GEN інвертора. Deye віддає його як окремі показники (напруга і потужність по фазах),
        /// назви ключів підбираємо за шаблоном (GenVoltageL1, GenPowerL1, TotalGenPower, GeneratorActivePower),
        /// лічильники виробітку/часу ігноруємо.
        /// </summary>
        private static void FillGenerator(Dictionary<string, double?> p, PowerSnapshot s)
        {
            var keys = p.Keys.Where(k =>
                Regex.IsMatch(k, "gen(?!eration)", RegexOptions.IgnoreCase) &&
                !Regex.IsMatch(k, "(daily|total.*(production|energy)|production|energy|time|run|hour|cumulative)", RegexOptions.IgnoreCase)).ToList();

            var voltages = keys.Where(k => Regex.IsMatch(k, "volt", RegexOptions.IgnoreCase))
                               .Select(k => p[k]).Where(v => v.HasValue).Select(v => v.Value).ToList();
            var powers = keys.Where(k => Regex.IsMatch(k, "power", RegexOptions.IgnoreCase) && !Regex.IsMatch(k, "volt", RegexOptions.IgnoreCase))
                             .Select(k => p[k]).Where(v => v.HasValue).Select(v => Math.Abs(v.Value)).ToList();

            if (voltages.Count == 0 && powers.Count == 0)
            {
                s.GeneratorOn = null;
                return;
            }

            s.GeneratorVoltage = voltages.Count > 0 ? voltages.Max() : (double?)null;
            s.GeneratorPower = powers.Count > 0 ? powers.Max() : (double?)null;
            s.GeneratorOn = (s.GeneratorVoltage.HasValue && s.GeneratorVoltage.Value >= GeneratorVoltageMin) ||
                            (s.GeneratorPower.HasValue && s.GeneratorPower.Value >= GeneratorPowerMin);
        }
    }
}
