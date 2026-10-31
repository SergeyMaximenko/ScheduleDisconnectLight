using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ScheduleDisconnectLight
{
    public static class ScheduleImageGenerator
    {
        static void Text(Graphics g, string value, float x, float y, float size, string color, bool bold = false)
        {
            using (var font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(ColorTranslator.FromHtml(color)))
                g.DrawString(value, font, brush, x, y);
        }

        // Only days included in the notification are rendered. Empty schedules are
        // explicitly marked unknown because the source model has no publication flag.
        public static byte[] Generate(IEnumerable<ScheduleOneDay> schedules, DateTime today, bool emergency = false)
        {
            var days = schedules.ToList();
            if (days.Count < 1 || days.Count > 2) throw new ArgumentException("Expected one or two days.");
            using (var bitmap = new Bitmap(1080, days.Count * 520 + 16))
            using (var g = Graphics.FromImage(bitmap))
            using (var stream = new MemoryStream())
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.White);
                for (int index = 0; index < days.Count; index++)
                {
                    var day = days[index];
                    int top = index * 520;
                    string relative = day.Date.Date == today.Date ? " (сьогодні)" :
                        day.Date.Date == today.Date.AddDays(1) ? " (завтра)" : "";
                    string[] weekdays = { "Нд", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб" };
                    Text(g, day.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) +
                        " " + weekdays[(int)day.Date.DayOfWeek] + relative,
                        20, top + 18, 32, "#1E293B", true);
                    if (emergency || day.Times == null || day.Times.Count == 0)
                    {
                        Text(g, emergency ? "Екстрені відключення — графік не діє" : "Графік відсутній або відключень не заплановано",
                            20, top + 180, 28, "#64748B");
                        continue;
                    }
                    var ranges = day.Times.Select(t => new[] {
                        Math.Max(0, Math.Min(1440, (int)t.Start.TotalMinutes)),
                        Math.Max(0, Math.Min(1440, (int)t.End.TotalMinutes))
                    }).ToList();
                for (int hour = 0; hour < 24; hour++)
                {
                    float x = 16 + (hour % 6) * 176, y = top + 72 + (hour / 6) * 108;
                    const float w = 160, h = 92, radius = 14;
                    using (var shape = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        shape.AddArc(x, y, radius * 2, radius * 2, 180, 90);
                        shape.AddArc(x + w - radius * 2, y, radius * 2, radius * 2, 270, 90);
                        shape.AddArc(x + w - radius * 2, y + h - radius * 2, radius * 2, radius * 2, 0, 90);
                        shape.AddArc(x, y + h - radius * 2, radius * 2, radius * 2, 90, 90);
                        shape.CloseFigure();
                        var state = g.Save();
                        g.SetClip(shape);
                        foreach (var r in ranges)
                        {
                            int a = Math.Max(hour * 60, r[0]), b = Math.Min(hour * 60 + 60, r[1]);
                            if (b > a)
                                using (var dark = new SolidBrush(ColorTranslator.FromHtml("#1E293B")))
                                    g.FillRectangle(dark, x + (a - hour * 60) * w / 60, y, (b - a) * w / 60, h);
                        }
                        g.Restore(state);
                        using (var border = new Pen(ColorTranslator.FromHtml("#E5E7EB"), 2))
                            g.DrawPath(border, shape);
                        bool fullOff = ranges.Any(r => r[0] <= hour * 60 && r[1] >= hour * 60 + 60);
                        bool fullOn = !ranges.Any(r => r[0] < hour * 60 + 60 && r[1] > hour * 60);
                        string label = hour.ToString("00") + ":00";
                        float labelX = fullOff || fullOn ? x + 52 : x + 37;
                        // Render the same label through each background mask for good contrast.
                        var labelState = g.Save();
                        g.SetClip(shape);
                        Text(g, label, labelX, y + 28, 30, "#273244");
                        foreach (var r in ranges)
                        {
                            int a = Math.Max(hour * 60, r[0]), b = Math.Min(hour * 60 + 60, r[1]);
                            if (b <= a) continue;
                            g.SetClip(shape);
                            g.SetClip(new RectangleF(x + (a - hour * 60) * w / 60, y, (b - a) * w / 60, h), System.Drawing.Drawing2D.CombineMode.Intersect);
                            Text(g, label, labelX, y + 28, 30, "#FFFFFF");
                        }
                        g.Restore(labelState);
                        if (fullOn || fullOff)
                        {
                            // Vector lightning avoids dependence on emoji fonts.
                            PointF[] bolt = { new PointF(x+31,y+31), new PointF(x+22,y+48), new PointF(x+29,y+48), new PointF(x+25,y+61), new PointF(x+40,y+41), new PointF(x+32,y+41) };
                            using (var brush = new SolidBrush(ColorTranslator.FromHtml(fullOff ? "#ADB7C6" : "#F2C94C")))
                                g.FillPolygon(brush, bolt);
                            if (fullOff)
                                using (var slash = new Pen(ColorTranslator.FromHtml("#ADB7C6"), 2))
                                    g.DrawLine(slash, x+20, y+31, x+41, y+61);
                        }
                    }
                }
                }
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }
    }
}
