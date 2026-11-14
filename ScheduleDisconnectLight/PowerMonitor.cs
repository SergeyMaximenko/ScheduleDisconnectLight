using Service;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ScheduleDisconnectLight
{
    /// <summary>
    /// Моніторинг світла і генератора за даними Deye. Замінює Arduino.
    ///
    /// Робота розділена на два етапи (порядок виклику в Program.Main):
    ///  1. Run(): бере ПОТОЧНИЙ стан інвертора з Deye (один виклик, історію не читає), порівнює зі станом
    ///     у закладці DeyeСтатус; якщо світло або генератор змінились, дописує рядок у журнал DeyeСтатистика
    ///     і одним атомарним запитом оновлює DeyeСтатус. Повертає подію зміни СВІТЛА (або null).
    ///  2. (Program формує графік відключень.)
    ///  3. SendLightMessage(): надсилає в Telegram повідомлення про світло вже з урахуванням графіка
    ///     (коли очікується повернення, коли наступне відключення). Про генератор повідомлень немає.
    ///
    /// Реагуємо одразу (без підтвердження). Хибних спрацювань мало, бо проміжні напруги
    /// ("нестабільна" мережа) ігноруються: статус у такому разі лишається попереднім.
    /// </summary>
    public static class PowerMonitor
    {
        /// <summary>
        /// Серійний номер інвертора будинку. Вказано явно, щоб не робити зайвий запит до Deye.
        /// Як визначити SN - див. закоментований приклад у DeyeClient.cs.
        /// </summary>
        private const string InverterSn = "2602042290";



        /// <summary>
        /// Етап 1: оновити стан у таблиці. Повертає подію зміни світла (для повідомлення) або null, якщо світло не змінювалось.
        /// </summary>
        public static LightEvent Run()
        {
            var now = Api.DateTimeUaCurrent;

            // Що зараз записано в таблиці (стовпець Прод на GitHub, ТЕСТ локально)
            var stored = DeyeSheet.ReadStored();

            // ---- 1. Поточний стан із Deye ----
            // Ключі доступу читаємо ПОЗА try: якщо їх не задано, це помилка налаштування (не збій Deye),
            // вона має піти нагору і з'явитись у логу як "Помилка в PowerMonitor" (див. Program.cs).
            var settings = DeyeSettings.Get();

            DeyeReading reading;
            try
            {
                reading = new DeyeClient(settings).GetLatest(InverterSn);
            }
            catch (Exception ex)
            {
                // Deye недоступний: статуси НЕ чіпаємо (щоб не вигадати "світло зникло"),
                // лише відмічаємо час запиту, щоб було видно, що застосунок працює.
                Console.WriteLine("PowerMonitor: Deye недоступний: " + ex.Message);
                DeyeSheet.WriteRequestTimeOnly(now);
                return null;
            }

            var snap = PowerCalc.Build(reading, now);
            Console.WriteLine($"PowerMonitor: виміряно {snap.MeasuredAt:yyyy-MM-dd HH:mm:ss}, мережа={snap.Grid}, вихід={snap.Output}, " +
                              $"генератор={snap.GeneratorOn}, SOC={snap.Soc}, режим батареї={snap.Mode}");

            if (snap.Stale)
            {
                Console.WriteLine("PowerMonitor: дані Deye застарілі або без часу, статуси не змінюємо");
                DeyeSheet.WriteRequestTimeOnly(now);
                return null;
            }

            var measured = snap.MeasuredAt.Value;

            if (stored.MeasuredAt.HasValue && measured < stored.MeasuredAt.Value)
            {
                // Deye повернув показники старіші за вже записані - ігноруємо
                Console.WriteLine("PowerMonitor: показники старіші за записані, пропускаємо");
                DeyeSheet.WriteRequestTimeOnly(now);
                return null;
            }

            // ---- 2. Нові статуси ----
            // "Невідомо" і "нестабільно" не змінюють статус: беремо попередній.
            int? newGrid = snap.Grid == VoltageState.On ? 1 : snap.Grid == VoltageState.Off ? 0 : stored.GridStatus;

           

            int? newGen = snap.GeneratorOn.HasValue ? (snap.GeneratorOn.Value ? 1 : 0) : stored.GenStatus;
            int? newInverter = snap.Output == VoltageState.On ? 1 : snap.Output == VoltageState.Off ? 0 : stored.InverterStatus;

            // Подія = статус був відомий і змінився. Перший запис (порожня таблиця) подією не вважається.
            bool gridChanged = stored.GridStatus.HasValue && newGrid.HasValue && newGrid != stored.GridStatus;
            bool genChanged = stored.GenStatus.HasValue && newGen.HasValue && newGen != stored.GenStatus;

            var next = new DeyeStored
            {
                RequestAt = now,
                MeasuredAt = measured,

                GridStatus = newGrid,
                GridDate = NewDate(stored.GridStatus, stored.GridDate, newGrid, gridChanged, measured),
                GridVoltage = snap.GridVoltage,

                GenStatus = newGen,
                GenDate = NewDate(stored.GenStatus, stored.GenDate, newGen, genChanged, measured),
                GenPower = snap.GeneratorPower,
                GenVoltage = snap.GeneratorVoltage,
                // Макс. потужність за поточний запуск: при запуску починається заново, поки працює - росте, після зупинки порожньо
                GenPeak = newGen == 1
                    ? (genChanged || stored.GenStatus != 1 || !stored.GenPeak.HasValue
                        ? snap.GeneratorPower
                        : Max(stored.GenPeak, snap.GeneratorPower))
                    : (double?)null,

                InverterStatus = newInverter,
                InverterDate = NewDate(stored.InverterStatus, stored.InverterDate, newInverter,
                    stored.InverterStatus.HasValue && newInverter.HasValue && newInverter != stored.InverterStatus, measured),
                LoadWatts = snap.LoadWatts,

                Soc = snap.Soc,
                BatteryMode = snap.Mode.HasValue ? (int?)snap.Mode.Value : null,
                BatteryWatts = snap.BatteryWatts
            };

            // ---- 3. Журнал змін (світло завжди раніше генератора, як у Arduino) ----
            var journal = new List<DeyeJournalRow>();

            if (gridChanged)
            {
                var row = new DeyeJournalRow { Object = "Світло", Date = measured, OnOff = newGrid.Value };
                if (newGrid == 1)
                {
                    // Світло з'явилось: скільки часу його не було
                    row.HoursPowerOff = Hours(stored.GridDate, measured);
                }
                journal.Add(row);
            }
            if (genChanged)
            {
                var row = new DeyeJournalRow { Object = "Генератор", Date = measured, OnOff = newGen.Value };
                if (newGen == 0)
                {
                    // Генератор зупинився: скільки часу він працював
                    row.HoursGenOn = Hours(stored.GenDate, measured);
                    // Максимум за цей запуск (накопичений у DeyeСтатус)
                    row.PeakW = stored.GenPeak;
                }
                journal.Add(row);
            }

            // ---- 4. Запис в таблицю: один атомарний запит ----
            DeyeSheet.WriteAll(next, journal);
            Console.WriteLine($"PowerMonitor: записано. Світло {stored.GridStatus}→{newGrid}, генератор {stored.GenStatus}→{newGen}, рядків у журнал: {journal.Count}");

            // Про зміну світла повідомимо на етапі 3 (після формування графіка)
            if (gridChanged)
            {
                return new LightEvent
                {
                    Appeared = newGrid == 1,
                    At = measured,
                    PreviousChange = stored.GridDate
                };
            }
            return null;
        }

        /// <summary>
        /// Етап 3: надіслати в Telegram повідомлення про світло. Викликається ПІСЛЯ формування графіка,
        /// бо в повідомленні є рядки про розклад відключень. schedule може бути null (графік недоступний).
        /// Запис у таблицю вже зроблено раніше: якщо Telegram недоступний, у таблиці все одно правильний стан.
        /// </summary>
        public static void SendLightMessage(LightEvent ev, Schedule schedule)
        {
            if (ev == null)
            {
                return;
            }

            var message = LightMessage.Build(ev, schedule);

            // Кілька спроб: Telegram іноді тимчасово недоступний
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (Parameters.IsOn(Parameters.TgLightEvent))
                    {
                        new SenderTelegram { AddLinkDashBoard = true }.Send(message);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PowerMonitor: Telegram, спроба {attempt} невдала: {ex.Message}");
                    if (attempt >= 3)
                    {
                        throw;
                    }
                    Thread.Sleep(2000);
                }
            }
        }

        /// <summary>
        /// Дата статусу: нова, якщо статус змінився або вперше став відомим; інакше лишається попередня.
        /// </summary>
        private static DateTime? NewDate(int? oldStatus, DateTime? oldDate, int? newStatus, bool changed, DateTime measured)
        {
            if (!newStatus.HasValue)
            {
                return oldDate; // статусу немає - дати теж
            }
            if (changed || !oldStatus.HasValue || !oldDate.HasValue)
            {
                return measured;
            }
            return oldDate;
        }

        /// <summary>Більше з двох значень (null ігноруємо)</summary>
        private static double? Max(double? a, double? b)
        {
            if (!a.HasValue) return b;
            if (!b.HasValue) return a;
            return Math.Max(a.Value, b.Value);
        }

        /// <summary>
        /// Скільки годин минуло між двома київськими датами (3 знаки після коми).
        /// Враховує перехід на літній/зимовий час.
        /// </summary>
        private static double? Hours(DateTime? from, DateTime to)
        {
            if (!from.HasValue)
            {
                return null;
            }
            var kyiv = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            var hours = (to - from.Value).TotalHours - (kyiv.GetUtcOffset(to) - kyiv.GetUtcOffset(from.Value)).TotalHours;
            return Math.Round(Math.Max(0, hours), 3);
        }
    }
}
