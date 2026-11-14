using Service;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScheduleDisconnectLight
{
    /// <summary>Подія зміни світла, яку виявив PowerMonitor</summary>
    public class LightEvent
    {
        /// <summary>true - світло з'явилось, false - зникло</summary>
        public bool Appeared;

        /// <summary>Час події (за даними Deye, київський)</summary>
        public DateTime At;

        /// <summary>Коли світло змінювало стан попереднього разу (звідси рахуємо "скільки не було / було"). null - невідомо.</summary>
        public DateTime? PreviousChange;
    }

    /// <summary>
    /// Текст повідомлення в Telegram про появу/зникнення світла.
    ///
    /// Складається з:
    ///  - заголовка (час події);
    ///  - блоку-цитати: скільки світла не було / було, і рядки про розклад відключень.
    /// Про генератор у повідомленні нічого немає.
    ///
    /// Варіанти (див. домовленості):
    ///   Світло з'явилось: звичайне; раніше за розклад ("нам пощастило"); без наступних відключень.
    ///   Світло зникло: за графіком; трохи раніше за розклад; позапланове.
    ///   Особливі: екстрені відключення (графіки не діють), графік недоступний (рядків про розклад немає).
    /// </summary>
    public static class LightMessage
    {
        private class Interval
        {
            public DateTime Start;
            public DateTime End;
        }

        /// <summary>
        /// Межа "раніше розкладу": якщо світло зникло не раніше ніж за стільки до початку відключення за графіком,
        /// вважаємо це відключенням "раніше розкладу" (і пишемо, на скільки). Якщо раніше за цю межу, відключення позапланове.
        /// Межа потрібна, щоб вранішнє позапланове відключення не вважалось "раніше" вечірнього за графіком.
        /// </summary>
        private static readonly TimeSpan EarlyWindow = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Сформувати текст. schedule може бути null (графік не вдалося отримати) - тоді рядків про розклад не буде.
        /// </summary>
        public static string Build(LightEvent ev, Schedule schedule)
        {
            var header = ev.Appeared
                ? "✅💡 <b>Світло з'явилось</b> о " + Api.TimeToStr(ev.At) + " 😊"
                : "❌💡 <b>Світло зникло</b> о " + Api.TimeToStr(ev.At) + " 😡";

            var lines = new List<string>();

            // Скільки світла не було (коли воно з'явилось) або скільки було (коли зникло)
            if (ev.PreviousChange.HasValue)
            {
                var span = ev.At - ev.PreviousChange.Value;
                lines.Add((ev.Appeared ? "⏱ світла не було — " : "⏱ світло було — ") + "<b>" + Duration(span) + "</b>");
            }

            lines.AddRange(ScheduleLines(ev, schedule));

            if (lines.Count == 0)
            {
                return header;
            }
            return header + "<blockquote>" + string.Join("\n", lines) + "</blockquote>";
        }

        /// <summary>Рядки про розклад відключень</summary>
        private static List<string> ScheduleLines(LightEvent ev, Schedule schedule)
        {
            var result = new List<string>();

            // Графік недоступний: рядків про розклад не додаємо
            if (schedule == null)
            {
                return result;
            }

            // Екстрені відключення: графіки не діють
            if (schedule.IsEmergencyShutdowns)
            {
                result.Add("⚠️ діють екстрені відключення, графіки не діють");
                return result;
            }

            var intervals = GetIntervals(schedule);
            var at = ev.At;

            // Відключення за графіком, у яке потрапила подія (якщо є)
            var current = intervals.FirstOrDefault(i => at >= i.Start && at < i.End);
            // Відключення, які ще не почались на момент події
            var upcoming = intervals.Where(i => i.Start > at).OrderBy(i => i.Start).ToList();

            if (ev.Appeared)
            {
                Interval next;
                if (current != null)
                {
                    // Світло з'явилось раніше, ніж закінчується відключення за графіком
                    result.Add("🎉 нам пощастило, за графіком відключення було до <b>" + TimeWithDay(current.End, at) + "</b>");
                    // "Наступне" беремо ПІСЛЯ поточного відключення
                    next = intervals.Where(i => i.Start >= current.End).OrderBy(i => i.Start).FirstOrDefault();
                }
                else
                {
                    next = upcoming.FirstOrDefault();
                }

                result.Add(next != null
                    ? "📅 наступне планове відключення — <b>" + Period(next, at) + "</b>"
                    : "📅 в наявному графіку ДТЕК планових відключень немає");
            }
            else
            {
                if (current != null)
                {
                    // Світло зникло за графіком
                    result.Add("📅 очікуємо за розкладом <b>" + AtTime(current.End, at) + "</b>");
                }
                else
                {
                    var soon = upcoming.FirstOrDefault(i => i.Start - at <= EarlyWindow);
                    if (soon != null)
                    {
                        // Зникло раніше за початок відключення за графіком: показуємо, на скільки саме раніше
                        result.Add("😕 на <b>" + Duration(soon.Start - at) + "</b> раніше розкладу, за графіком відключення з <b>" + TimeWithDay(soon.Start, at) + "</b>");
                        result.Add("📅 очікуємо за розкладом <b>" + AtTime(soon.End, at) + "</b>");
                    }
                    else
                    {
                        // Позапланове відключення
                        result.Add("⚠️ позапланове відключення, за графіком світло мало бути");
                        var next = upcoming.FirstOrDefault();
                        if (next != null)
                        {
                            result.Add("📅 найближче планове — <b>" + Period(next, at) + "</b>");
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Усі відключення за графіком (сьогодні й завтра) як безперервні періоди з датою й часом.
        /// Відключення, що переходить через північ (17:00 - 00:00 і 00:00 - 03:30), об'єднується в один період 17:00 - 03:30.
        /// </summary>
        private static List<Interval> GetIntervals(Schedule schedule)
        {
            var list = new List<Interval>();
            var days = new[] { schedule.ScheduleCurrentDay, schedule.ScheduleNextDay };

            // Якщо останній період сьогодні вже продовжено на завтра (EndNextDay),
            // то перший період завтра (з 00:00) вже входить у нього й не рахується окремо.
            bool skipFirstOfNextDay = false;

            for (int d = 0; d < days.Length; d++)
            {
                var day = days[d];
                if (day == null || day.Times == null)
                {
                    continue;
                }

                var times = day.Times.ToList();
                if (d == 1 && skipFirstOfNextDay && times.Count > 0 && times[0].StartIsStartDay())
                {
                    times.RemoveAt(0);
                }

                for (int i = 0; i < times.Count; i++)
                {
                    var t = times[i];
                    var start = day.Date + t.Start;
                    var end = t.EndNextDay != TimeSpan.Zero
                        ? day.Date.AddDays(1) + t.EndNextDay
                        : day.Date + t.End;

                    list.Add(new Interval { Start = start, End = end });

                    if (d == 0 && i == times.Count - 1 && t.EndNextDay != TimeSpan.Zero)
                    {
                        skipFirstOfNextDay = true;
                    }
                }
            }

            return list.OrderBy(i => i.Start).ToList();
        }

        /// <summary>
        /// Час "14:30". Якщо це не сьогоднішній день (відносно події), додаємо "завтра ".
        /// Північ (00:00) рахуємо кінцем попереднього дня, щоб не писати "завтра 00:00" для періоду "17:00 - 00:00".
        /// </summary>
        private static string TimeWithDay(DateTime t, DateTime at)
        {
            var date = t.TimeOfDay == TimeSpan.Zero ? t.Date.AddDays(-1) : t.Date;
            return (date > at.Date ? "завтра " : "") + Api.TimeToStr(t);
        }

        /// <summary>
        /// "о 14:30" або, якщо не сьогодні, "завтра о 03:30" (для фраз на кшталт "очікуємо за розкладом ...").
        /// Північ (00:00) рахуємо кінцем попереднього дня.
        /// </summary>
        private static string AtTime(DateTime t, DateTime at)
        {
            var date = t.TimeOfDay == TimeSpan.Zero ? t.Date.AddDays(-1) : t.Date;
            return (date > at.Date ? "завтра о " : "о ") + Api.TimeToStr(t);
        }

        /// <summary>Період "17:00 – 23:00 (6 год)"; якщо починається не сьогодні, спереду "завтра "</summary>
        private static string Period(Interval i, DateTime at)
        {
            return (i.Start.Date > at.Date ? "завтра " : "") +
                   Api.TimeToStr(i.Start) + " – " + Api.TimeToStr(i.End) +
                   " (" + Duration(i.End - i.Start) + ")";
        }

        /// <summary>
        /// Тривалість словами без секунд: "3 хв", "1 год 5 хв", "10 год". Нуль хвилин не пишемо.
        /// </summary>
        private static string Duration(TimeSpan span)
        {
            var totalMinutes = (long)Math.Round(span.TotalMinutes);
            if (totalMinutes < 0)
            {
                totalMinutes = 0;
            }

            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours == 0)
            {
                return minutes + " хв";
            }
            if (minutes == 0)
            {
                return hours + " год";
            }
            return hours + " год " + minutes + " хв";
        }
    }
}
