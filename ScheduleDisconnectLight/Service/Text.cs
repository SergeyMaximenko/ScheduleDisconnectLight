using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;


namespace Service
{
    /// <summary>
    /// Работа с текстом
    /// </summary>
    public static  class Text
    {

        private static readonly ITFormatProvider _formatProvider = new ITFormatProvider();

        /// <summary>
        /// Региональные форматы, принятые в системе 
        /// </summary>
        public static IFormatProvider FormatProvider { get { return _formatProvider; } }

        /// <summary>
		/// Конвертировать строку в DateTime
		/// </summary>
		/// <param name="s">Строка для конвертации в дату</param>
		/// <param name="view">Тип представления даты в строке</param>
		/// <returns>Конвертированное значение</returns>
		[Pure]
        public static DateTime ToDateTime(string s, DateTimeView view)
        {
            return ToDateTime(s, view, null);
        }

        /// <summary>
        /// Конвертировать строку в DateTime
        /// </summary>
        /// <param name="s">Строка для конвертации в дату</param>
        /// <param name="view">Тип представления даты в строке</param>
        /// <param name="cultureInfo"></param>
        /// <returns>Конвертированное значение</returns>
        /// <exclude />
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        [Pure]
        public static DateTime ToDateTime(string s, DateTimeView view, CultureInfo cultureInfo)
        {
            if (string.IsNullOrEmpty(s))
            {
                return DateTime.MinValue;
            }
            DateTime dateTimeResult;
            if ((view & DateTimeView.TimeStamp) == DateTimeView.TimeStamp)
            {
                // если передали дату в формате ГГГГММДД без времени, то установить время 00:00:00
                if (s.Length == 8)
                {
                    s = s.PadRight(14, '0');
                }
                DateTime.TryParseExact(s, GetDateTimeFormat(DateTimeView.TimeStamp), DateTimeFormat, DateTimeStyles.None,
                    out dateTimeResult);
                return dateTimeResult;
            }
            var dateTimeFormat = cultureInfo == null ? DateTimeFormat : cultureInfo.DateTimeFormat;
            DateTime.TryParse(s, dateTimeFormat, DateTimeStyles.None, out dateTimeResult);
            return dateTimeResult;
        }

        /// <summary>
        /// Конвертировать строку в DateTime
        /// </summary>
        /// <param name="s">Строка для конвертации в дату</param>
        /// <returns>Конвертированное значение</returns>
        [Pure]
        public static DateTime ToDateTime(string s)
        {
            return ToDateTime(s, DateTimeView.DateTime | DateTimeView.Seconds);
        }

        /// <summary>
        /// Конвертировать объект в строку
        /// </summary>
        /// <param name="value">Значение</param>
        /// <returns>Строковое значение</returns>
        [Pure]
        public static string Convert(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            TypeCode type = Type.GetTypeCode(value.GetType());
            switch (type)
            {
                case TypeCode.DBNull:
                    return "null";
                case TypeCode.Int16:
                    return ((Int16)value).ToString(PointDecimalSeparator);
                case TypeCode.UInt16:
                    return ((UInt16)value).ToString(PointDecimalSeparator);
                case TypeCode.Int32:
                    return Convert((Int32)value);
                case TypeCode.UInt32:
                    return ((UInt32)value).ToString(PointDecimalSeparator);
                case TypeCode.Int64:
                    return ((Int64)value).ToString(PointDecimalSeparator);
                case TypeCode.UInt64:
                    return ((UInt64)value).ToString(PointDecimalSeparator);
                case TypeCode.Single:
                    return ((Single)value).ToString(PointDecimalSeparator);
                case TypeCode.Double:
                    return Convert((Double)value);
                case TypeCode.Decimal:
                    return Convert((Decimal)value);
                case TypeCode.DateTime:
                    return Convert((DateTime)value);
                case TypeCode.Char:
                    return Convert((char)value);
                default:
                    return value.ToString();
            }
        }

        /// <summary>
        /// Конвертировать строку в другой тип
        /// </summary>
        /// <param name="s">Исходная строка</param>
        /// <param name="targetType">Тип, в который конвертировать</param>
        /// <returns></returns>
        /// <exclude />
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        [Pure]
        public static object ToTypedObject(string s, Type targetType)
        {
            if (s == null)
            {
                s = string.Empty;
            }
            TypeCode type = Type.GetTypeCode(targetType);
            switch (type)
            {
                case TypeCode.DBNull:
                    return DBNull.Value;
                case TypeCode.Int16:
                    return System.Convert.ToInt16(s, PointDecimalSeparator);
                case TypeCode.UInt16:
                    return System.Convert.ToUInt16(s, PointDecimalSeparator);
                case TypeCode.Int32:
                    return ToInt32(s);
                case TypeCode.UInt32:
                    return System.Convert.ToUInt32(s, PointDecimalSeparator);
                case TypeCode.Int64:
                    return System.Convert.ToInt64(s, PointDecimalSeparator);
                case TypeCode.UInt64:
                    return System.Convert.ToUInt64(s, PointDecimalSeparator);
                case TypeCode.Single:
                    return System.Convert.ToSingle(s, PointDecimalSeparator);
                case TypeCode.Double:
                    return (Double)ToDecimal(s);
                case TypeCode.Decimal:
                    return ToDecimal(s);
                case TypeCode.DateTime:
                    return ToDateTime(s);
                case TypeCode.Char:
                    return s.Length > 0 ? s[0] : new Char();
                case TypeCode.String:
                    return s;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Конвертировать дату и время в строку
        /// </summary>
        /// <param name="value">Дата</param>
        /// <returns>Строковое описание даты</returns>
        [Pure]
        public static string Convert(DateTime value)
        {
            return Convert(value, DateTimeView.DateTime | DateTimeView.Seconds);
        }

        /// <summary>
        /// Конвертировать decimal в строку заданной длины и точности
        /// </summary>
        /// <param name="value">Числовое значение типа decimal</param>
        /// <param name="len">Общая длина строки</param>
        /// <param name="dec"></param>
        /// <returns>Строковое представление</returns>
        [Pure]
        public static string Convert(decimal value, int len, int dec = 0)
        {
            if (len <= 0 || len <= dec)
            {
                return Convert(value);
            }

            string result;
            if (dec <= 0)
            {
                // Конвертировать только целую часть
                result = Convert((long)Math.Floor(value));
            }
            else
            {
                string mask = "0." + new string('0', dec);
                result = value.ToString(mask, PointDecimalSeparator);
            }
            if (result.Length > len)
            {
                // Не влезло. Возвращаем звездочки
                return new string('*', len);
            }
            return PadLeft(result, len);
        }

        /// <summary>
        /// Дробная часть маски для преобразования decimal в строку без вывода завершающих нулей
        /// Количество знаков после разделителя выбрано исходя из максимальной точности decimal (28 знаков)
        /// </summary>
        readonly static string _decimalTypeDecimalPlacesMask = new string('#', 28);

        /// <summary>
        /// Конвертировать decimal в строку
        /// </summary>
        /// <param name="value">Числовое значение типа decimal</param>
        /// <returns>Строковое представление</returns>
        [Pure]
        public static string Convert(decimal value)
        {

            return value.ToString("0." + _decimalTypeDecimalPlacesMask, PointDecimalSeparator);
        }

        /// <summary>
        /// <para>Конвертировать double в строку</para>
        /// <para>Если значение double - бесконечность 
        /// (<see cref="double.PositiveInfinity"/>, <see cref="double.NegativeInfinity"/>,
        /// <see cref="double.NaN"/>) - будет возвращен 0</para>
        /// </summary>
        /// <param name="value">Числовое значение типа double</param>
        /// <returns>Строковое представление</returns>
        [Pure]
        public static string Convert(double value)
        {
            if (double.IsInfinity(value) || double.IsNaN(value))
            {
                return (0d).ToString(PointDecimalSeparator);
            }
            // TODO: переписать конвертацию, сейчас реализован самый простой вариант
            // Используем стандартную конвертацию
            string result = value.ToString("R", PointDecimalSeparator);
            // Если конвертация была сделана в научный формат - то нужно заново преобразовать
            if (result.Contains("E"))
            {
                // Данная ситуация помогает только если большая дробная часть.
                // Сделано как вариант решения срочной проблемы
                result = value.ToString("F50", PointDecimalSeparator).TrimEnd('0');
                // Если всё-же дробной части не было - выкидываем её
                if (result[result.Length - 1] == '.')
                {
                    result = result.Substring(0, result.Length - 2);
                }
            }
            return result;
        }

        /// <summary>
        /// Конвертировать int в строку
        /// </summary>
        /// <param name="value">Число</param>
        /// <returns>Строковое представление числа</returns>
        [Pure]
        public static string Convert(int value)
        {
            return value.ToString(PointDecimalSeparator);
        }

        /// <summary>
        /// Конвертировать long в строку
        /// </summary>
        /// <param name="value">Число</param>
        /// <returns>Строковое представление числа</returns>
        [Pure]
        public static string Convert(long value)
        {
            return value.ToString(PointDecimalSeparator);
        }


        /// <summary>
        /// Конвертировать char в строку
        /// </summary>
        /// <param name="value">Символ</param>
        /// <returns>Строковое представление числа</returns>
        [Pure]
        public static string Convert(char value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Конвертировать дату и время в строку, используя указанный формат представления даты
        /// </summary>
        /// <param name="value">Дата для конвертации</param>
        /// <param name="view">Формат представления даты</param>
        /// <returns>Строковое описание даты</returns>
        [Pure]
        public static string Convert(DateTime value, DateTimeView view)
        {
            return Convert(value, view, false);
        }


        /// <summary>
        /// Конвертировать дату и время в строку, используя указанный формат представления даты
        /// </summary>
        /// <param name="value">Дата для конвертации</param>
        /// <param name="view">Формат представления даты</param>
        /// <param name="useClientCulture">
        /// <para>true: использовать региональные настройки рабочей станции</para>
        /// <para>false: использовать стандартные региональные настройки</para>
        /// </param>
        /// <returns>Строковое описание даты</returns>
        [Pure]
        public static string Convert(DateTime value, DateTimeView view, bool useClientCulture)
        {
            if (value == DateTime.MinValue)
            {
                return string.Empty;
            }
            var formatString = GetDateTimeFormat(view);
            DateTimeFormatInfo dateFormat = DateTimeFormat;
            if (useClientCulture && GetClientCulture != null)
            {
                var clientCulture = GetClientCulture();
                if (clientCulture != null)
                {
                    formatString = ConvertDateTimeFormat(formatString, clientCulture);
                    dateFormat = clientCulture.DateTimeFormat;
                }
            }
            return value.ToString(formatString, dateFormat);
        }

        /// <summary>
        /// Делегат для получения культуры рабочей станции
        /// </summary>
        internal static Func<CultureInfo> GetClientCulture;

        /// <summary>
        /// Получить строку пустой даты, используя указанный формат.
        /// Пример: "  .  .       :  :  "
        /// </summary>
        /// <param name="view">Формат представления даты</param>
        /// <returns>Строка пустой даты</returns>
        [Pure]
        public static string GetEmptyDate(DateTimeView view)
        {
            string format = GetDateTimeFormat(view);
            var charFormat = format.ToCharArray();
            var emptyDate = new StringBuilder();
            for (int i = 0; i < charFormat.Length; i++)
            {
                if (charFormat[i].ToString(CultureInfo.InvariantCulture).Equals(DateTimeFormat.TimeSeparator) ||
                    charFormat[i].ToString(CultureInfo.InvariantCulture).Equals(DateTimeFormat.DateSeparator))
                {
                    emptyDate.Append(charFormat[i]);
                }
                else
                {
                    emptyDate.Append(' ');
                }
            }
            return emptyDate.ToString();
        }

        /// <summary>
        /// Конвертировать интервал времени в строку
        /// </summary>
        /// <param name="firstDate">Первая дата</param>
        /// <param name="secondDate">Вторая дата</param>
        /// <param name="view">Тип представления интервала в строке</param>
        /// <returns>Конвертированное значение</returns>
        [Pure]
        public static string Convert(DateTime firstDate, DateTime secondDate, TimeSpanView view = TimeSpanView.Long)
        {
            return Convert(secondDate - firstDate, view);
        }

        /// <summary>
        /// Конвертировать интервал времени в строку
        /// Пример длинного формата: "10 дней 4 часа 3 минуты"
        /// Пример короткого формата: "10д 4ч 3м"
        /// </summary>
        /// <param name="timeSpan">Интервал времени</param>
        /// <param name="view">Тип представления интервала в строке</param>
        /// <returns>Конвертированное значение</returns>
        [Pure]
        public static string Convert(TimeSpan timeSpan, TimeSpanView view = TimeSpanView.Long)
        {
            var timeBuilder = new StringBuilder();
            var days = Math.Abs(timeSpan.Days);
            if (days > 0)
            {
                timeBuilder.AppendFormat("{0}{1}",
                    days,
                    view == TimeSpanView.Short
                        ? "d"
                        : string.Concat(" ", getNumberLabel(days, "день", "дня", "днів")));
            }
            var hours = Math.Abs(timeSpan.Hours);
            if (hours > 0)
            {
                if (timeBuilder.Length > 0)
                {
                    timeBuilder.Append(" ");
                }
                timeBuilder.AppendFormat("{0}{1}",
                    hours,
                    view == TimeSpanView.Short
                        ? "г"
                        : string.Concat(" ", getNumberLabel(hours, "година", "години", "годин")));
            }
            var minutes = Math.Abs(timeSpan.Minutes);
            if (minutes > 0)
            {
                if (timeBuilder.Length > 0)
                {
                    timeBuilder.Append(" ");
                }
                timeBuilder.AppendFormat("{0}{1}",
                    minutes,
                    view == TimeSpanView.Short
                        ? "х"
                        : string.Concat(" ", getNumberLabel(minutes, "хвилина", "хвилини", "хвилин")));
            }
            return timeBuilder.ToString();
        }

        /// <summary>
        /// Конвертировать Guid в строку
        /// </summary>
        /// <param name="value">Значение типа Guid</param>
        /// <returns>Строковое представление</returns>
        [Pure]
        public static string Convert(Guid value)
        {
            return value.ToString();
        }

        /// <summary>
        /// Конвертировать Guid в строку
        /// </summary>
        /// <param name="value">Значение типа Guid</param>
        /// <param name="binaryFormat">
        /// <see langword="false"/> - традиционное строковое представление guid (guid.ToString()),
        /// <see langword="true"/> - бинарное представление</param>
        /// <returns>Строковое представление</returns>
        [Pure]
        public static string Convert(Guid value, bool binaryFormat)
        {
            return binaryFormat ?
                BitConverter.ToString(value.ToByteArray()).Replace("-", "") :
                Convert(value);
        }

        
        /// <summary>
        /// Вспомогательный класс для вычисления лет, месяцев и дней между датами 
        /// </summary>
        private struct DateTimeSpan
        {
            public int Years { get; private set; }

            public int Months { get; private set; }

            public int Days { get; private set; }

            private enum Phase
            {
                Years,
                Months,
                Days,
                Done
            }

            public static DateTimeSpan CompareDates(DateTime date1, DateTime date2)
            {
                if (date2 < date1)
                {
                    var sub = date1;
                    date1 = date2;
                    date2 = sub;
                }

                var current = date1;
                var years = 0;
                var months = 0;
                var days = 0;

                var phase = Phase.Years;
                var span = new DateTimeSpan();
                var officialDay = current.Day;

                while (phase != Phase.Done)
                {
                    switch (phase)
                    {
                        case Phase.Years:
                            if (current.AddYears(years + 1) > date2)
                            {
                                phase = Phase.Months;
                                current = current.AddYears(years);
                            }
                            else
                            {
                                years++;
                            }

                            break;
                        case Phase.Months:
                            if (current.AddMonths(months + 1) > date2)
                            {
                                phase = Phase.Days;
                                current = current.AddMonths(months);
                                if (current.Day < officialDay && officialDay <= DateTime.DaysInMonth(current.Year, current.Month))
                                    current = current.AddDays(officialDay - current.Day);
                            }
                            else
                            {
                                months++;
                            }

                            break;
                        case Phase.Days:
                            if (current.AddDays(days + 1) > date2)
                            {
                                current = current.AddDays(days);
                                span = new DateTimeSpan { Days = days, Months = months, Years = years };
                                phase = Phase.Done;
                            }
                            else
                            {
                                days++;
                            }

                            break;
                    }
                }

                return span;
            }
        }

        /// <summary>
        /// Выбрать склонение в зависимости от числа
        /// </summary>
        /// <param name="number">Число</param>
        /// <param name="one">Для числа 1</param>
        /// <param name="lessFive">Для чисел 2,3,4</param>
        /// <param name="moreThanFive">Для чисел 5 и выше</param>
        /// <returns></returns>
        private static string getNumberLabel(int number, string one, string lessFive, string moreThanFive)
        {
            var numberLabel = "";
            if (number == 1)
            {
                numberLabel = one;
            }
            else if (number > 1 && number < 5)
            {
                numberLabel = lessFive;
            }
            else if (number >= 5)
            {
                numberLabel = moreThanFive;
            }
            return numberLabel;
        }

        /// <summary>
        /// Формат интервала
        /// </summary>
        public enum TimeSpanView
        {
            /// <summary>
            /// Длинный
            /// </summary>
            Long = 0,

            /// <summary>
            /// Краткий
            /// </summary>
            Short = 1
        }

        /// <summary>
        /// Получить строку формата по указанным флагам представления даты
        /// </summary>
        /// <param name="view">Формат представления даты</param>
        /// <returns>Строка формата</returns>
        [Pure]
        public static string GetDateTimeFormat(DateTimeView view)
        {
            string format;

            if ((view & DateTimeView.TimeStamp) == DateTimeView.TimeStamp)
            {
                if ((view & DateTimeView.Date) == DateTimeView.Date)
                {
                    //Если выбран режим TimeStamp + Date
                    format = _timeStampFormat.Substring(0, 8);
                }
                else if ((view & DateTimeView.Time) == DateTimeView.Time && (view & DateTimeView.Seconds) == DateTimeView.Seconds)
                {
                    format = String.Format("{0} {1}", _timeStampFormat.Substring(0, 8), DateTimeFormat.LongTimePattern);
                }
                else
                {
                    //Если выбран режим TimeStamp
                    format = _timeStampFormat;
                }
            }
            else if ((view & DateTimeView.Date) == DateTimeView.Date)
            {
                // Дата
                format = (view & DateTimeView.Year2) == DateTimeView.Year2
                    ? DateTimeFormat.ShortDatePattern
                    : DateTimeFormat.LongDatePattern;
            }
            else if ((view & DateTimeView.Time) == DateTimeView.Time)
            {
                // Время
                format = (view & DateTimeView.Seconds) == DateTimeView.Seconds
                    ? DateTimeFormat.LongTimePattern
                    : DateTimeFormat.ShortTimePattern;
            }
            else
            {
                // Дата и время
                format = String.Format("{0} {1}",
                    (view & DateTimeView.Year2) == DateTimeView.Year2
                        ? DateTimeFormat.ShortDatePattern
                        : DateTimeFormat.LongDatePattern,
                    (view & DateTimeView.Seconds) == DateTimeView.Seconds
                        ? DateTimeFormat.LongTimePattern
                        : DateTimeFormat.ShortTimePattern);
            }
            return format;
        }

        /// <summary>
        /// Конвертировать формат даты-времени в формат нужной культуры
        /// </summary>
        /// <param name="targetCulture"></param>
        /// <param name="sourceFormat"></param>
        /// <returns></returns>
        /// <exclude />
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        [Pure]
        public static string ConvertDateTimeFormat(string sourceFormat, CultureInfo targetCulture)
        {
            var dateTimeFormat = targetCulture.DateTimeFormat;
            string dayPattern = dateTimeFormat.ShortDatePattern;
            if (sourceFormat.IndexOf("yy", StringComparison.InvariantCultureIgnoreCase) >= 0 && sourceFormat.IndexOf("yyyy", StringComparison.InvariantCultureIgnoreCase) < 0)
            {
                // Если было 2 знака для года, то так и оставить
                dayPattern = dayPattern.Replace("yyyy", "yy").Replace("YYYY", "yy");
            }
            if (sourceFormat.IndexOf("hh", StringComparison.InvariantCultureIgnoreCase) < 0)
            {
                // Только дата (нет времени)
                return dayPattern;
            }
            // Формат времени: с секундами или без
            string timePattern = sourceFormat.IndexOf("ss", StringComparison.InvariantCultureIgnoreCase) >= 0 ? dateTimeFormat.LongTimePattern : dateTimeFormat.ShortTimePattern;
            // дата+время
            return $"{dayPattern} {timePattern}";
        }

        ///// <summary>
        ///// Конвертировать строку в Double
        ///// </summary>
        ///// <param name="s">Строка для конвертации</param>
        ///// <returns>Конвертированное значение</returns>
        //public static double ToDouble(string s)
        //{
        //    double textToDouble = 0;
        //    if (!String.IsNullOrEmpty(s))
        //    {
        //        Double.TryParse(s, NumberStyles.Any, PointDecimalSeparator, out textToDouble);
        //    }
        //    return textToDouble;
        //}

        /// <summary>
        /// Конвертировать строку в Decimal
        /// </summary>
        /// <param name="s">Строка для конвертации</param>
        /// <returns>Конвертированное значение</returns>
        [Pure]
        public static decimal ToDecimal(string s)
        {
            decimal textToDecimal = 0;
            if (!String.IsNullOrEmpty(s))
            {
                Decimal.TryParse(s, NumberStyles.Any, PointDecimalSeparator, out textToDecimal);
            }
            return textToDecimal;
        }

        /// <summary>
        /// Конвертировать строку в Int32.
        /// Если переданную строку нельзя конвертировать в число, то функция вернет ноль
        /// </summary>
        /// <param name="s">Строка для конвертации</param>
        /// <returns>Конвертированное значение</returns>
        [Pure]
        public static int ToInt32(string s)
        {
            int textToInt = 0;
            if (!String.IsNullOrEmpty(s))
            {
                Int32.TryParse(s, out textToInt);
            }
            return textToInt;
        }

        /// <summary>
        /// Конвертировать строку в Int64 (long)
        /// </summary>
        /// <param name="s">Строка для конвертации</param>
        /// <returns>Конвертированное значение</returns>
        /// <remarks>Если передана неправильная, пустая или <c>null</c> строка, то возвращается 0</remarks>
        [Pure]
        public static Int64 ToInt64(string s)
        {
            Int64 textToInt64 = 0;
            if (!String.IsNullOrEmpty(s))
            {
                Int64.TryParse(s, out textToInt64);
            }
            return textToInt64;
        }

        /// <summary>
        /// Конвертировать строку в Guid
        /// </summary>
        /// <param name="s">Строка для конвертации в Guid</param>
        /// <returns>Конвертированное значение</returns>
        [Pure]
        public static Guid ToGuid(string s)
        {
            var guid = Guid.Empty;
            if (!string.IsNullOrEmpty(s))
            {
                if (s.Length == 32)
                {
                    var bytes = new byte[16];
                    for (var i = 0; i < 16; i++)
                    {
                        bytes[i] = System.Convert.ToByte(s.Substring(i * 2, 2), 16);
                    }
                    guid = new Guid(bytes);
                }
                else
                {
                    Guid.TryParse(s, out guid);
                }
            }
            return guid;
        }


        /// <summary>
        /// Конвертировать массив произвольного типа данных в строку
        /// </summary>
        [Pure]
        public static string ArrayToString<T>(IEnumerable<T> array, Converter<T, string> converter, char separator = ';')
        {
            if (array == null)
            {
                return string.Empty;
            }
            var result = new StringBuilder();
            foreach (var item in array)
            {
                result.Append(converter(item));
                result.Append(separator);
            }
            return result.ToString().Trim(separator);
        }

        /// <summary>
        /// Конвертировать строку в массив произвольного типа данных
        /// </summary>
        [Pure]
        public static T[] StringToArray<T>(string str, Converter<string, T> converter, char separator = ';')
        {
            if (String.IsNullOrEmpty(str))
            {
                return new T[0];
            }
            try
            {
                var strArray = str.Split(separator);
                var result = Array.ConvertAll(strArray, converter);
                return result;
            }
            catch
            {
                return new T[0];
            }
        }

        



        /// <summary>
        /// Кодировка, установленная на компьютере - Windows-1251 (кириллица)
        /// </summary>
        /// <exclude />
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public static bool IsEncodingCyrillic
        {
            // ReSharper disable CSharpWarnings::CS0612
            get { return NonUnicodeEncoding.CodePage == Win1251Encoding.CodePage; }
            // ReSharper restore CSharpWarnings::CS0612
        }

        /// <summary>
        /// Кодировка для не-unicode программ, установленная на компьютере
        /// </summary>
        public static readonly Encoding NonUnicodeEncoding;

        static Text()
        {
            const int encoding1251 = 1251;
            int nonUnicodeEncodingId;
#if NETCOREAPP
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			if (OSEnvironment.IsWindowsPlatform)
			{
				nonUnicodeEncodingId = (int)GetACP();				
			}
			else
			{
				// TODO: разобраться как получить кодировку для не-unicode в linux
				nonUnicodeEncodingId = encoding1251;
			}
#else
            nonUnicodeEncodingId = (int)GetACP();
#endif
            NonUnicodeEncoding = Encoding.GetEncoding(nonUnicodeEncodingId);
            Win1251Encoding = Encoding.GetEncoding(encoding1251);
        }

        /// <summary>
        /// Получить кодировку сервера приложений для не-unicode программ
        /// </summary>
        /// <returns></returns>
        [DllImport("kernel32.dll")]
        private static extern uint GetACP();

        /// <summary>
        /// <para>Кодировка Windows-1251</para>
        /// </summary>
        /// <remarks>
        /// <para>В связи с тем, что система работает на любых кодировках, это свойство использовать нельзя!</para>
        /// <para>В большинстве случаев следует использовать <see cref="NonUnicodeEncoding"/></para>
        /// <para>Свойство можно использовать только для раскодировки данных, которые ранее были закодированы через данную кодировку.</para>
        /// </remarks>
        [Obsolete("In most cases use NonUnicodeEncoding")]
        public static readonly Encoding Win1251Encoding;

        /// <summary>
        /// Формат с точкой-разделителем целой и дробной частей
        /// </summary>
        public static readonly NumberFormatInfo PointDecimalSeparator =
            new NumberFormatInfo { NumberDecimalSeparator = ".", NumberGroupSeparator = " " };

        internal static readonly DateTimeFormatInfo DateTimeFormat =
            new DateTimeFormatInfo
            {
                DateSeparator = ".",
                TimeSeparator = ":",
                ShortDatePattern = "dd.MM.yy",
                LongDatePattern = "dd.MM.yyyy",
                ShortTimePattern = "HH:mm",
                LongTimePattern = "HH:mm:ss"
            };

        const string _timeStampFormat = "yyyyMMddHHmmss";

        //private static readonly ITFormatProvider _formatProvider = new ITFormatProvider();

        /// <summary>
        /// Региональные форматы, принятые в системе 
        /// </summary>
        //public static IFormatProvider FormatProvider { get { return _formatProvider; } }

        /// <summary>
        /// Конвертирует строку в base64
        /// </summary>
        /// <param name="str"></param>
        /// <param name="encoding">Кодировка строки. По умолчанию - UTF8</param>
        /// <returns></returns>
        [Pure]
        public static string ToBase64(string str, Encoding encoding = null)
        {
            var stringEncoding = encoding ?? Encoding.UTF8;
            var bytes = stringEncoding.GetBytes(str);
            return System.Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Преобразовать строки из base64 
        /// </summary>
        /// <param name="base64Str">Строка base64</param>
        /// <param name="encoding">Кодировка исходной строки. По умолчанию - UTF</param>
        /// <returns></returns>
        [Pure]
        public static string FromBase64(string base64Str, Encoding encoding = null)
        {
            if (string.IsNullOrWhiteSpace(base64Str))
            {
                return string.Empty;
            }
            byte[] bytes;
            try
            {
                bytes = System.Convert.FromBase64String(base64Str);
            }
            catch (FormatException)
            {
                return string.Empty;
            }
            var stringEncoding = encoding ?? Encoding.UTF8;
            return stringEncoding.GetString(bytes);
        }

        /// <summary>
        /// Строка является числом
        /// </summary>
        /// <param name="s">Проверяемая строка</param>
        /// <param name="numberHasDot">Может содержать точку-разделитель целой и дробной частей. По умолчанию - может содержать</param>
        /// <returns></returns>
        [Pure]
        public static bool IsNumber(string s, bool numberHasDot = true)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            bool hasDot = !numberHasDot;
            // Проверить, является ли число отрицательным
            bool isNegative = s[0] == '-';
            // Проверить, чтобы строка не состоялась только с минуса
            if (isNegative && s.Length == 1)
            {
                return false;
            }
            for (var i = isNegative ? 1 : 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '.')
                {
                    if (hasDot)
                    {
                        return false;
                    }
                    hasDot = true;
                    continue;
                }
                if (!Char.IsDigit(c))
                {
                    return false;
                }
            }
            return true;
        }


        /// <summary>
        /// Символ является частью слова
        /// </summary>
        /// <param name="ch">Проверяемый символ</param>
        /// <returns></returns>
        [Pure]
        public static bool IsPartOfWord(char ch)
        {
            return ch == '_' || Char.IsLetterOrDigit(ch);
        }

        /// <summary>
        /// Строка может использоваться как идентификатор:
        /// Содержит буквы цифры и подчеркивание и начинается с буквы или "_"
        /// </summary>
        /// <param name="str">Строка</param>
        /// <param name="canBeQuotable">Идентификатор может быть заключен в двойные кавычки</param>
        /// <returns></returns>
        [Pure]
        public static bool IsValidIdentifier(string str, bool canBeQuotable = false)
        {
            if (String.IsNullOrEmpty(str))
            {
                return false;
            }
            var chars = str.ToCharArray();

            // определить индекс с которого начинать проверку
            int startIndex;
            int endIndex;
            if (canBeQuotable && chars[0] == '"' && chars[chars.Length - 1] == '"')
            {
                startIndex = 1;
                endIndex = chars.Length - 1;
            }
            else
            {
                startIndex = 0;
                endIndex = chars.Length;
            }

            if (!Char.IsLetter(chars[startIndex]) && chars[startIndex] != '_')
            {
                return false;
            }
            for (int i = startIndex; i < endIndex; i++)
            {
                if (!IsPartOfWord(chars[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Проверяет наличие идентификатора в строке. Функция регистронезависима
        /// </summary>
        /// <param name="str">Проверяемая строка</param>
        /// <param name="identifier">Идентификатор</param>
        /// <returns></returns>
        [Pure]
        public static bool ContainsIdentifier(string str, string identifier)
        {
            if (string.IsNullOrEmpty(str) || string.IsNullOrEmpty(identifier))
            {
                return false;
            }
            var index = 0;
            var success = false;
            do
            {
                index = str.IndexOf(identifier, index, StringComparison.InvariantCultureIgnoreCase);
                if (index != -1)
                {
                    var endIndex = index + identifier.Length;
                    success = (index == 0 || !IsPartOfWord(str[index - 1])) && (endIndex == str.Length || !IsPartOfWord(str[endIndex]));
                    if (success)
                    {
                        break;
                    }
                    index = endIndex;
                }
            } while (index != -1);
            return success;
        }

        /// <summary>
        /// Подсчитать количество вхождений подстроки в строку
        /// </summary>
        /// <param name="search">"Что ищем" - подстрока, которая ищется</param>
        /// <param name="searched">"В чем ищем" - строка, в которой ищутся вхождения</param>
        /// <returns>Количество вхождений</returns>
        [Pure]
        public static int Occurs(string search, string searched)
        {
            int result = 0;
            if (!string.IsNullOrEmpty(search) && !string.IsNullOrEmpty(searched))
            {
                for (int i = 0; i <= searched.Length - search.Length; ++i)
                {
                    if (String.CompareOrdinal(searched, i, search, 0, search.Length) == 0)
                    {
                        i += search.Length - 1;
                        ++result;
                    }
                }
            }

            //#if DEBUG
            //            int oldEngineResult = (searched.Length - searched.Replace(search, String.Empty).Length) / search.Length;
            //            Debug.Assert(result == oldEngineResult);
            //#endif 
            return result;
        }


        /// <summary>
        /// Подсчитать количество вхождений символа в строку
        /// </summary>
        /// <param name="search">"Что ищем" - символ, который ищется</param>
        /// <param name="searched">"В чем ищем" - строка, в которой ищутся вхождения</param>
        /// <returns>Количество вхождений</returns>
        [Pure]
        public static int Occurs(char search, string searched)
        {
            return Occurs(search.ToString(CultureInfo.InvariantCulture), searched);
        }

        /// <summary>
        /// Возвращает символьную строку, созданную заменой заданного числа символов в символьном выражении другим символьным выражением
        /// </summary>
        /// <param name="expression">Задает символьное выражение, в котором происходит замещение</param>
        /// <param name="startReplacement">Задает позицию выражения, начиная c которой происходит замещение (нумерация с 0).
        /// Если задана позиция меньше нуля, то считается равной нулю.
        /// Если символ с указанной позицией отсутствует в строке (строка короче), то строка дополняется пробелами до требуемой длины.</param>
        /// <param name="charactersReplaced">Задает число символов, подлежащих замещению. 
        /// Если задано отрицательное количество, то количество считается равным нулю.</param>
        /// <param name="replacement">Задает замещающее символьное выражение</param>
        /// <returns></returns>
        [Pure]
        public static string Stuff(string expression, int startReplacement, int charactersReplaced, string replacement)
        {
            startReplacement = Math.Max(startReplacement, 0);
            charactersReplaced = Math.Max(charactersReplaced, 0);
            var sb = new StringBuilder(expression);
            if (sb.Length < startReplacement + charactersReplaced)
            {
                sb.Append(' ', startReplacement + charactersReplaced - sb.Length);
            }
            sb.Remove(startReplacement, charactersReplaced);
            sb.Insert(startReplacement, replacement);
            return sb.ToString();
        }

        


        /// <summary>
        /// Повторить строку заданное количество раз
        /// </summary>
        /// <param name="text">Строка</param>
        /// <param name="count">Количество повторений</param>
        /// <returns>Результат</returns>
        [Pure]
        public static string Replicate(string text, int count)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < count; ++i)
            {
                result.Append(text);
            }
            return result.ToString();
        }

        /// <summary>
        /// Получить заданное количество символов из выражения, начиная с самого левого символа
        /// </summary>
        /// <param name="text">Выражение</param>
        /// <param name="length">Количество символов. Если оно больше чем длина исходной строки, то функция вернет исходное выражение</param>
        /// <returns></returns>
        [Pure]
        public static string Left(string text, int length)
        {
            if (String.IsNullOrEmpty(text) || text.Length <= length)
            {
                return text ?? String.Empty;
            }
            return text.Substring(0, length);
        }

        /// <summary>
        /// Получить заданное количество символов из выражения, начиная с самого правого символа
        /// </summary>
        /// <param name="text">Выражение</param>
        /// <param name="length">Количество символов. Если оно больше чем длина исходной строки, то функция вернет исходное выражение</param>
        /// <returns></returns>
        [Pure]
        public static string Right(string text, int length)
        {
            if (String.IsNullOrEmpty(text))
            {
                return String.Empty;
            }
            int textLenght = text.Length;
            return textLenght <= length ? text : text.Substring(textLenght - length, length);
        }

        /// <summary>
        /// Заменить символы перехода на новую строку на "\r\n" (формат, принятый в Windows)
        /// </summary>
        public static string NewLinesToRn(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            return text.Replace("\r\n", "\r").Replace("\n", "\r").Replace("\r", "\r\n");
        }

        /// <summary>
        /// Заменить символы перехода на новую строку на "\r"
        /// </summary>
        public static string NewLinesToR(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            return text.Replace("\r\n", "\r").Replace('\n', '\r');
        }

        /// <summary>
        /// Получить первые N строк текста
        /// </summary>
        /// <param name="text"></param>
        /// <param name="linesCount"></param>
        /// <returns></returns>
        public static string FirstLines(string text, int linesCount)
        {
            if (String.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            string[] lines = SplitToLines(text);
            if (lines.Length <= linesCount)
            {
                return text;
            }
            var result = new StringBuilder();
            for (int i = 0; i < Math.Min(linesCount, lines.Length); ++i)
            {
                result.AppendLine(lines[i]);
            }
            result.Append("...");
            return result.ToString();
        }

        /// <summary>
        /// Разделить текст на строки.
        /// </summary>
        /// <param name="text">Входной текст.</param>
        /// <param name="keepEmptyLines">Сохранять пустые строки.</param>
        /// <returns>Массив строк.</returns>
        public static string[] SplitToLines(string text, bool keepEmptyLines = true)
        {
            if (String.IsNullOrEmpty(text))
            {
                if (text == null)
                {
                    return new string[] { };
                }
                return keepEmptyLines ? new string[] { string.Empty } : new string[] { };
            }
            var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            if (!keepEmptyLines)
            {
                lines = Array.FindAll(lines, str => str.Trim() != string.Empty);
            }
            return lines;
        }

        /// <summary>
        /// Вернуть первую строку текста
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string FirstLine(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            int newLinePosition = text.IndexOfAny(new[] { '\n', '\r' });
            return newLinePosition != -1 ? text.Substring(0, newLinePosition) : text;
        }

        /// <summary>
        /// Преобразовать текст в "proper" регистр (первый символ на верхнем регистре, остальные на нижнем)
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        [Pure]
        public static string ToProperCase(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            return Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());
        }

        /// <summary>
        /// Возвращает строковый массив, содержащий подстроки текста, ограниченные разделителями.
        /// Если текст пустой или null, возвращает пустой массив
        /// </summary>
        /// <param name="text">Исходный текст</param>
        /// <param name="separator">Разделитель</param>
        /// <param name="keepEmptyLines">Признак сохранения пустых строк</param>
        /// <returns></returns>
        [Pure]
        public static string[] Split(string text, char separator = ',', bool keepEmptyLines = false)
        {
            return Split(text, new[] { separator }, keepEmptyLines);
        }

        /// <summary>
        /// Возвращает строковый массив, содержащий подстроки текста, ограниченные разделителями.
        /// Если текст пустой или null, возвращает пустой массив
        /// </summary>
        /// <param name="text">Исходный текст</param>
        /// <param name="separators">Список разделителей</param>
        /// <param name="keepEmptyLines">Признак сохранения пустых строк</param>
        /// <returns></returns>
        [Pure]
        public static string[] Split(string text, char[] separators, bool keepEmptyLines = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new string[] { };
            }
            string[] stringArray = text.Split(separators, keepEmptyLines ? StringSplitOptions.None : StringSplitOptions.RemoveEmptyEntries);
            return stringArray;
        }

        ///<summary>Порезка строки с учетом скобок и двойных кавычек</summary>
        ///<param name="line">Исходная строка</param>
        ///<param name="separator">Разделитель</param>
        /// <remarks>Аналог VFP-функции str2arskb()</remarks>
        [Pure]
        public static string[] SplitEx(string line, char separator)
        {
            if (String.IsNullOrEmpty(line))
            {
                return new string[0];
            }

            // Список скобок, применяющихся в выражении
            string[,] skb = new[,] { { "(", ")" }, { "\"", "\"" } };
            // Масив элементов выражения
            List<string> result = new List<string>();
            // Масив элементов выражения разбитых по разделителю, без учета скобок
            string[] parts = line.Split(separator);

            // Если в выражении скобки не используются
            if (!line.Contains(skb[0, 0]) && !line.Contains(skb[1, 0]))
            {
                return parts;
            }

            // Разделитель
            string strSeparator = separator.ToString(CultureInfo.InvariantCulture);
            int index = 0;

            while (index < parts.Length)
            {
                string tmp = String.Empty;
                int tmpIndex = 0;

                // Выражение не закончено до тех пор, пока не будут закрыты все скобки или это последный элемент масива
                do
                {
                    tmp = String.Concat(tmp, tmpIndex > 0 ? strSeparator : String.Empty, parts[index]);
                    index++;
                    tmpIndex++;
                } while (index < parts.Length && !checkBrackets(skb, tmp));

                result.Add(tmp);
            }
            return result.ToArray();
        }

        /// <summary>Проверяет соответствие строки на количество открытых/закрытых скобок</summary>
        /// <param name="brackets">Набор скобок</param>
        /// <param name="line">Строка, которая проверяется</param>
        private static bool checkBrackets(string[,] brackets, string line)
        {
            for (int i = 0; i < 2; i++)
            {
                // Количество незакрытых скобок
                int number = brackets[i, 0] == brackets[i, 1]
                    ? Occurs(brackets[i, 0], line) % 2
                    : Occurs(brackets[i, 0], line) - Occurs(brackets[i, 1], line);

                // Если в выражении есть хотя б один тип незакрытых скобок
                if (number != 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Проверить является ли символ печатным
        /// </summary>
        /// <param name="ch">Символ</param>
        /// <param name="includeNewLine">Считать символы перевода каретки печатными символами</param>
        /// <returns></returns>
        [Pure]
        public static bool IsPrintableCharacter(char ch, bool includeNewLine)
        {
            bool result = ch >= 32 || ch == '\t' || includeNewLine && (ch == '\n' || ch == '\r');
            return result;
        }

        /// <summary>
        /// Конвертировать число в строку и разбить цифры на триады
        /// </summary>
        [Pure]
        public static string SplitToTriads(int number)
        {
            string result = number.ToString("#,0", new NumberFormatInfo { NumberGroupSeparator = " " });
            return result;
        }




        /// <summary>
        /// Рассчитать процент схожести двух строк
        /// </summary>
        /// <param name="string1">Первая строка</param>
        /// <param name="string2">Вторая строка</param>
        /// <param name="caseSensitive">Чувствительно к регистру (<see langword="true"/>: a != A, <see langword="false"/>: a == A)</param>
        /// <returns>Действительное число (от 0 до 1.0) - процент схожести двух строк</returns>
        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public static double SimilarText(string string1, string string2, bool caseSensitive = true)
        {
            if (string1 == null || string2 == null)
            {
                return 0d;
            }
            if (string1.Length == 0 || string2.Length == 0)
            {
                return 0d;
            }
            if (!caseSensitive)
            {
                string1 = string1.ToUpper();
                string2 = string2.ToUpper();
            }
            if (string1 == string2)
            {
                return 1d;
            }

            var stepsToSame = optimalStringAlignmentDistance(string1, string2);
            return (1.0d - stepsToSame / (double)Math.Max(string1.Length, string2.Length));
        }

        /// <summary>
        /// Редакционное расстояние между строками по алгоритму "optimal string alignment distance"
        /// </summary>
        /// <param name="string1">Первая строка</param>
        /// <param name="string2">Вторая строка</param>
        /// <returns>Целое число - количество шагов получиния из первой строки второй (или наоборот)</returns>
        private static int optimalStringAlignmentDistance(string string1, string string2)
        {
            if (string.IsNullOrEmpty(string1) || string.IsNullOrEmpty(string2))
            {
                return Math.Max(string1 == null ? 0 : string1.Length, string2 == null ? 0 : string2.Length);
            }
            var length1 = string1.Length;
            var length2 = string2.Length;

            // Матрица расстояний (для любых i, j distances[i][j] = расстояние между первыми i символами string1 и j символами string2,
            // размер матрицы: (length1 + 1) x (length2 + 1))
            var distances = new int[length1 + 1][];
            for (var i = 0; i <= length1; i++)
            {
                distances[i] = new int[length2 + 1];
                distances[i][0] = i;
            }
            for (var j = 1; j <= length2; j++)
            {
                distances[0][j] = j;
            }

            // Заполнение матрицы расстояний
            for (var i = 1; i <= length1; i++)
            {
                for (var j = 1; j <= length2; j++)
                {
                    var cost = string1[i - 1] == string2[j - 1] ? 0 : 1;
                    distances[i][j] = Math.Min(distances[i][j - 1] + 1, // Вставка
                        Math.Min(distances[i - 1][j] + 1,               // Удаление
                        distances[i - 1][j - 1] + cost));               // Замена
                                                                        // Транспозиция
                    if (i > 1 && j > 1 && string1[i - 1] == string2[j - 2] && string1[i - 2] == string2[j - 1])
                    {
                        distances[i][j] = Math.Min(distances[i][j], distances[i - 2][j - 2] + cost);
                    }
                }
            }
            return distances[length1][length2];
        }

        /// <summary>
        /// Сравнить две строки без учета регистра и правых пробелов
        /// </summary>
        /// <param name="str1">Первая строка для сравнения</param>
        /// <param name="str2">Вторая строка для сравнения</param>
        /// <returns>Результат сравнения</returns>
        [Pure]
        public static bool CompareEx(string str1, string str2)
        {
            str1 = trimEndIfNotNullOrEmpty(str1);
            str2 = trimEndIfNotNullOrEmpty(str2);
            return string.Compare(str1, str2, StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>
        /// Обрезать строку справа, если она не null и не пуста
        /// </summary>
        /// <param name="str">Строка, которую нужно обрезать справа</param>
        /// <returns>Строка без крайних правых пробелов</returns>
        private static string trimEndIfNotNullOrEmpty(string str)
        {
            return String.IsNullOrEmpty(str) ? str : str.TrimEnd();
        }

        /// <summary>
        /// <para>Сравнить два объекта. Если объекты разных типов - будет выполнено приведение к общему типу и сравнение</para>
        /// <para>Например вызов Common.Tools.Text.Compare("1", 1) вернет результат <c>true</c></para>
        /// </summary>
        /// <param name="obj1">Объект для сравнения 1</param>
        /// <param name="obj2">Объект для сравнения 2</param>
        /// <returns>Признак что заданные объекты идентичны</returns>
        [Pure]
        public static bool Compare(object obj1, object obj2)
        {
            if (obj1 == null || obj2 == null)
            {
                return (obj1 == null) == (obj2 == null);
            }
            var obj1Type = obj1.GetType();
            var obj2Type = obj2.GetType();
            // Если типы одинаковые - вызываем простой Equals
            if (obj1Type == obj2Type)
            {
                return obj1.Equals(obj2);
            }

            if (obj1Type == typeof(string) || obj2Type == typeof(string))
            {
                // Если один из объектов - строка, то приводим оба значения к строке с учетом региональных стандартов
                string str1 = Text.Convert(obj1);
                string str2 = Text.Convert(obj2);
                return str1.Equals(str2);
            }

            // Если у элементов разные типы - приводим типы
            object obj2Converted;
            // Пробуем конвертировать тип
            try
            {
                obj2Converted = System.Convert.ChangeType(obj2, obj1Type);
            }
            catch
            {
                return false;
            }

            // Если конвертация прошла успешно - сравниваем значения
            return obj1.Equals(obj2Converted);
        }

        /// <summary>
        /// Функция преобразования числа в строку по указанному формату(аналог r0_space на FoxPro)
        /// </summary>
        /// <param name="number">Число, которое необходимо преобразовать</param>
        /// <param name="length">Общая длина строкового представления числа, включая начальные пробелы</param>
        /// <param name="precision">Формат, в который преобразовывается число - количество знаков после запятой</param>
        /// <param name="millionSeparator">Признак вывода числа с отделением миллионов</param>
        /// <param name="requiredPrecision">Количество знаков после запятой, которые всегда выводятся</param>
        /// <returns>Строка с отформатированным числом. Если длина строкового представления числа больше параметра <code>length</code>,
        /// то вместо целой части выводятся "*"</returns>
        [Pure]
        public static string FormatNumber(decimal number, int length, int precision, bool millionSeparator = false, int requiredPrecision = 0)
        {
            StringBuilder resultStr = new StringBuilder();
            if (precision > 0 && length <= precision + 1)
            {
                return string.Empty;
            }
            // Если число равно 0 возвращение строки заполненной пробелами
            if (number == 0)
            {
                resultStr.Append(' ', length);

            }
            else
            {
                requiredPrecision = requiredPrecision > precision ? precision : requiredPrecision;
                // Заокруглить к нужной точности
                number = Math.Round(number, precision);
                string numberString = Convert(number);
                resultStr.Append(numberString);
                // Определение позиции разделителя
                int pointPos = numberString.IndexOf(PointDecimalSeparator.NumberDecimalSeparator, StringComparison.Ordinal);
                // Определение точности числа, в зависимости от положения разделителя
                int numberPrecision = pointPos == -1 ? 0 : numberString.Length - pointPos - 1;

                // Мнимое положение точки. Если число целое, то положение точки - после последнего символа 
                var imaginaryPointPos = pointPos >= 0 ? pointPos : resultStr.Length;

                // Добавление необходимого числа завершающих нулей
                if (requiredPrecision > 0 && numberPrecision < requiredPrecision)
                {
                    resultStr.Append(numberPrecision == 0 ? PointDecimalSeparator.NumberDecimalSeparator : "");
                    resultStr.Append('0', requiredPrecision - numberPrecision);
                }

                // Количество разделителей миллиона
                var millionSeparatorsCount = millionSeparator && number >= 1000000 ? 1 : 0;

                // Теоретическое количество точек
                var pointsCount = (precision > 0 ? 1 : 0);

                // Замена целой части на "*" в случае переполнения
                if (imaginaryPointPos > length - precision - millionSeparatorsCount - pointsCount)
                {
                    resultStr.Remove(0, imaginaryPointPos);
                    resultStr.Insert(0, "*", length - precision - millionSeparatorsCount - pointsCount);
                }

                // Если установлен признак вывода числа с отделением миллионов и число не меньше миллиона, 
                // то добавить разделитель миллиона
                if (millionSeparatorsCount > 0)
                {
                    // Отнимает от length 1, потому что один символ был зарезервирован для разделителя
                    resultStr.Insert((pointPos > 0 ? pointPos : length - 1) - 6, "'");
                }

                // Добавление к числу необходимого числа начальных пробелов
                if (resultStr.Length < length)
                {
                    resultStr.Insert(0, new string(' ', length - resultStr.Length));
                }
            }
            return resultStr.ToString();
        }

        /// <summary>
        /// Функция преобразования числа в строку по указанному формату(аналог r0_space на FoxPro)
        /// </summary>
        /// <param name="number">Число, которое необходимо преобразовать</param>
        /// <param name="length">Общая длина строкового представления числа, включая начальные пробелы</param>
        /// <param name="precision">Формат, в который преобразовывается число - количество знаков после запятой</param>
        /// <param name="millionSeparator">Признак вывода числа с отделением миллионов</param>
        /// <param name="requiredPrecision">Количество знаков после запятой, которые всегда выводятся</param>
        /// <returns>Строка с отформатированным числом. Если длина строкового представления числа больше параметра <code>length</code>,
        /// то вместо целой части выводятся "*"</returns>
        [Pure]
        public static string FormatNumber(double number, int length, int precision, bool millionSeparator = false, int requiredPrecision = 0)
        {
            return FormatNumber((decimal)number, length, precision, millionSeparator, requiredPrecision);
        }


        /// <summary>
        /// Дополнить строку слева до нужной длины пробелами или другим символом
        /// </summary>
        /// <param name="text">Текст</param>
        /// <param name="totalWidth">Требуемая длина. Если входная строка превышает <paramref name="totalWidth"/>, то она будет усечена</param>
        /// <param name="paddingChar">Символ, которым дополнить</param>
        /// <returns></returns>
        /// <remarks>Аналог VFP-функции padl()</remarks>
        [Pure]
        public static string PadLeft(string text, int totalWidth, char paddingChar = ' ')
        {
            return Left(text, totalWidth).PadLeft(totalWidth, paddingChar);
        }

        /// <summary>
        /// Дополнить строку справа до нужной длины пробелами или другим символом
        /// </summary>
        /// <param name="text">Текст</param>
        /// <param name="totalWidth">Требуемая длина. Если входная строка превышает <paramref name="totalWidth"/>, то она будет усечена</param>
        /// <param name="paddingChar">Символ, которым дополнить</param>
        /// <returns></returns>
        /// <remarks>Аналог VFP-функции padr()</remarks>
        [Pure]
        public static string PadRight(string text, int totalWidth, char paddingChar = ' ')
        {
            return Left(text, totalWidth).PadRight(totalWidth, paddingChar);
        }


        /// <summary>
        /// Центрировать строку
        /// </summary>
        /// <param name="stringToCenter">Строка, которую нужно разместить в центре</param>
        /// <param name="length">Длина результирующей строки</param>
        /// <returns>Центрирована строка</returns>
        [Pure]
        public static string PadCenter(string stringToCenter, int length)
        {
            stringToCenter = stringToCenter ?? string.Empty;
            // Если длина результирующей строки меньше длины строки, которую нужно центрировать, то вернуть первые length символов
            if (stringToCenter.Length > length)
            {
                return Left(stringToCenter, length);
            }

            stringToCenter = stringToCenter.PadLeft((length + stringToCenter.Length) / 2).PadRight(length);
            return stringToCenter;
        }

        /// <summary>
        /// Поиск позиции вхождения подстроки в строку по номеру вхождения
        /// </summary>
        /// <param name="inString">Строка, в которой осуществлять поиск</param>
        /// <param name="searchString">Строка поиска</param>
        /// <param name="occurs">Номер вхождения. Нумерация начинается с 1.</param>
        /// <returns>Номер позиции вхождения строки поиска в <see cref="inString"/>. Нумерация позиции начинается с 0.</returns>
        [Pure]
        public static int IndexOf(string inString, string searchString, int occurs)
        {
            int index = -1;
            int kol = 0;
            if (string.IsNullOrEmpty(inString) || string.IsNullOrEmpty(searchString))
            {
                return index;
            }
            while (true)
            {
                kol++;
                index = inString.IndexOf(searchString, index + 1, StringComparison.Ordinal);
                if (index < 0 || kol == occurs)
                {
                    break;
                }
            }
            return index;
        }

        /// <summary>
        /// Извлечь подстроку из строки
        /// </summary>
        /// <param name="fullString">Строка из которой нужно извлечь подстроку</param>
        /// <param name="startPosition">Позиция первого знака подстроки в данной строке (с нуля)</param>
        /// <param name="length">Длина подстроки</param>
        /// <returns>Подстрока</returns>
        [Pure]
        public static string Substring(string fullString, int startPosition, int length)
        {
            if (String.IsNullOrEmpty(fullString))
            {
                return string.Empty;
            }
            int fullStringLength = fullString.Length;
            // Если позиция первого знака больше длины строки, или недопустимые значения позиции первого знака или длины,
            // то вернуть пустую строку
            if (startPosition > fullStringLength || startPosition < 0 || length <= 0)
            {
                return string.Empty;
            }

            // Если длина подстроки не задана, или подстрока выходит за пределы строки, то вернуть подстроку, начало которой в startPosition 
            if (length + startPosition > fullStringLength)
            {
                return fullString.Substring(startPosition);
            }
            return fullString.Substring(startPosition, length);
        }

        /// <summary>
        /// Извлечь подстроку из строки (от указанной позиции знака до конца строки)
        /// </summary>
        /// <param name="fullString">Строка из которой нужно извлечь подстроку</param>
        /// <param name="startPosition">Позиция первого знака подстроки в данной строке (с нуля)</param>
        /// <returns>Подстрока</returns>
        [Pure]
        public static string Substring(string fullString, int startPosition)
        {
            return Substring(fullString, startPosition, fullString.Length - startPosition);
        }

        /// <summary>
        /// Получить i-ю часть строки с разделителями
        /// </summary>
        /// <param name="expression">Входная строка, из которой нужно получить часть</param>
        /// <param name="index">Номер части строки (нумерация с единицы)</param>
        /// <param name="separator">Разделитель</param>		
        /// <returns>i-ая часть строки</returns>
        [Pure]
        public static string PartOfString(string expression, int index, string separator = ",")
        {
            if (String.IsNullOrEmpty(expression) || String.IsNullOrEmpty(separator) || index < 1)
            {
                return string.Empty;
            }
            // Перейти от нумерации с 1 до нумерации с 0
            var zeroBasedIndex = index - 1;
            // Разбить строку с разделителями на зоны
            var zones = expression.Split(new[] { separator }, StringSplitOptions.None);
            // Вернуть подстроку, если подстрока с переданным индексом существует. Иначе вернуть пустую строку
            if (zeroBasedIndex < zones.Length)
            {
                return zones[zeroBasedIndex];
            }
            return string.Empty;
        }

        /// <summary>
        /// Удалить лишние пробелы из строки (несколько подряд идущих пробелов заменяются на один)
        /// </summary>
        [Pure]
        public static string RemoveExtraSpaces(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return string.Empty;
            }

            // находим группы символов между пробелами и склеиваем их в строку
            var returnValue = new StringBuilder();
            int wordPartBegin = 0;
            while (wordPartBegin < str.Length)
            {
                // группой символов будем считать подстроку, разделенную последовательностью из более чем 2 символов пробела
                // найти индекс конца следующей группы
                int wordPartEnd = wordPartBegin;
                // проверить, что на данной позиции нет подряд идущих двух пробелов
                while (wordPartEnd < str.Length && !(str[wordPartEnd] == ' ' && wordPartEnd + 1 < str.Length && str[wordPartEnd + 1] == ' '))
                {
                    wordPartEnd++;
                }
                // добавить к выходной строке текущую группу
                returnValue.Append(str.Substring(wordPartBegin, wordPartEnd - wordPartBegin));
                // перейти к поиску следующей группы
                wordPartBegin = wordPartEnd + 1;
            }
            return returnValue.ToString();
        }

        /// <summary>
        /// Удалить перечисленные символы из строки
        /// </summary>
        [Pure]
        public static string RemoveSymbol(string str, char[] symbols)
        {
            if (string.IsNullOrEmpty(str) || symbols == null)
            {
                return str;
            }
            var sb = new StringBuilder(str);
            foreach (var symbol in symbols)
            {
                sb.Replace(symbol.ToString(CultureInfo.InvariantCulture), string.Empty);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Заменить элемент формата в указанной строке строковым представлением соответствующего объекта в указанном массиве
        /// </summary>
        /// <param name="format">Строка составного формата</param>
        /// <param name="args">Массив объектов, которые необходимо отформатировать</param>
        /// <returns>Копия <paramref name="format"/>, в которой элементы формата заменены 
        /// строковыми представления соответствующих объектов в <paramref name="args"/></returns>
        /// <remarks>
        /// Для приведения элементов args к строковому виду функцией Text.Format используется метод Text.Convert.
        /// Стандартная функция C# (string.Format) для приведения элементов args к строковому виду использует метод object.ToString(),
        /// поэтому может возвращать разные результаты в зависимости от региональных настроек сервера приложений.
        /// </remarks>
        [Pure]
        public static string Format(string format, params object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return string.Empty;
            }
            var len = args == null ? 0 : args.Length;
            var convertedArgs = new object[len];
            for (int i = 0; i < len; i++)
            {
                convertedArgs[i] = Convert(args[i]);
            }
            string result;
            try
            {
                result = string.Format(format, convertedArgs);
            }
            catch (FormatException)
            {
                result = format;
            }

            return result;
        }

        /// <summary>
        /// Преобразовать текст в стиль Кэмел
        /// </summary>
        /// <param name="text">Текст для преобразования</param>
        /// <returns>Результат преобразования</returns>
        [Pure]
        public static string ToCamelCase(string text)
        {
            var result = new List<char>();
            // Признак, что следующая буква на верхнем регистре
            var upper = true;
            foreach (var ch in text)
            {
                // Если символ - подчеркивание или число,
                // то следующая буква должна быть на верхнем регистре
                if (ch == '_' || char.IsDigit(ch))
                {
                    result.Add(ch);
                    upper = true;
                }
                // Символы +,-,. заменяем на _
                else if (ch == '+' || ch == '.' || ch == '-')
                {
                    result.Add('_');
                }
                // Переверсти букву на верхний регистр
                else if (upper)
                {
                    result.Add(char.ToUpper(ch));
                    upper = false;
                }
                // Просто добавить символ
                else
                {
                    result.Add(char.ToLower(ch));
                }
            }
            return new string(result.ToArray());
        }

        /// <summary>
        /// Заменить в строке все вхождения подстроки на другую строку
        /// </summary>
        /// <param name="str">Исходная строка</param>
        /// <param name="oldValue">Заменяемая строка</param>
        /// <param name="newValue">Строка для подстановки</param>
        /// <param name="caseSensitive">С учетом регистра</param>
        /// <returns></returns>
        public static string Replace(string str, string oldValue, string newValue, bool caseSensitive = true)
        {
            // Защита от null
            if (string.IsNullOrWhiteSpace(str))
            {
                return string.Empty;
            }

            // Ничего не меняем
            if (string.IsNullOrEmpty(oldValue) || newValue == null)
            {
                return str;
            }

            // Если необходить анализировать регистр - вызывать стандартный метод
            if (caseSensitive)
            {
                return str.Replace(oldValue, newValue);
            }

            int position0, position1;
            var count = position0 = 0;

            var upperString = str.ToUpperInvariant();
            var upperPattern = oldValue.ToUpperInvariant();

            // Определить максимальную длину строки
            var inc = (str.Length / oldValue.Length) * (newValue.Length - oldValue.Length);
            var chars = new char[str.Length + Math.Max(0, inc)];

            // Пока есть вхождения заменяемой строки в исходной (замена не выполняется, изменяется индекс начала поиска)
            while ((position1 = upperString.IndexOf(upperPattern, position0, StringComparison.InvariantCulture)) != -1)
            {
                for (var i = position0; i < position1; ++i)
                {
                    chars[count++] = str[i];
                }
                foreach (var c in newValue)
                {
                    chars[count++] = c;
                }
                position0 = position1 + oldValue.Length;
            }
            // Если не выполнено ни одной замены - вернуть исходную строку
            if (position0 == 0)
            {
                return str;
            }
            for (var i = position0; i < str.Length; ++i)
            {
                chars[count++] = str[i];
            }
            return new string(chars, 0, count);
        }

        /// <summary>
        /// Заменить все вхождения слова.
        /// Выполняется замена только целого слова. Если вхождение является частью другого слова, то замена не выполняется.
        /// </summary>
        /// <param name="str">Строка, в которой ищутся вхождения</param>
        /// <param name="word">Слово для замены</param>
        /// <param name="replacement">Слово, которым заменять</param>
        /// <returns>Результат замены</returns>
        public static string ReplaceWord(string str, string word, string replacement)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }
            var occurs = new List<int>();
            int index = 0;
            var wordLen = word.Length;
            // Найти все вхождения слова
            do
            {
                index = str.IndexOf(word, index, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }
                // Проверяем допустимость символа слева и справа
                var endIndex = index + wordLen;
                if ((index == 0 || !IsPartOfWord(str[index - 1])) &&
                    (endIndex == str.Length || !IsPartOfWord(str[endIndex])))
                {
                    // Нашли полное слово - выполняем замену
                    occurs.Add(index);
                    index = endIndex;
                }
                else
                {
                    index++;
                }

            } while (true);
            // Выполнить замены
            if (occurs.Count > 0)
            {
                var sb = new StringBuilder(str);
                for (int i = occurs.Count - 1; i >= 0; i--)
                {
                    var occurence = occurs[i];
                    sb.Remove(occurence, wordLen);
                    sb.Insert(occurence, replacement);
                }
                str = sb.ToString();
            }
            return str;
        }

        /// <summary>
        /// Падеж
        /// </summary>
        public enum Case
        {
            /// <summary>
            /// Именительный падеж
            /// </summary>
            Nominative = 1,

            /// <summary>
            /// Родительный падеж
            /// </summary>
            Genitive = 2,

            /// <summary>
            /// Дательный падеж
            /// </summary>
            Dative = 3,

            /// <summary>
            /// Винительный падеж
            /// </summary>
            Accusative = 4,

            /// <summary>
            /// Творительный падеж
            /// </summary>
            Instrumental = 5
        }

        ///<summary>
        /// Формат представления даты
        ///</summary>
        [Flags]
        public enum DateTimeView
        {
            /// <summary>
            /// Дата и время. Пример: 31.12.2007 14:37
            /// </summary>
            DateTime = 0,
            /// <summary>
            /// Дата. Пример: 31.12.2007
            /// </summary>
            Date = 1,
            /// <summary>
            /// Время. Пример: 14:37
            /// </summary>
            Time = 2,
            /// <summary>
            /// Timestamp (yyyyMMddHHmmss). Пример: 20071231143721
            /// </summary>
            TimeStamp = 4,
            /// <summary>
            /// Отображать 2 знака в году. 
            /// Пример: 31.12.07. Или для даты с временем: 31.12.07 14:37
            /// </summary>
            Year2 = 8,
            /// <summary>
            /// Отображать время с секундами. 
            /// Пример: 14:37:21. Для даты с временем: 31.12.2007 14:37:21
            /// </summary>
            Seconds = 16,
            /// <summary>
            /// YyyyMmDd. Пример: 20071231
            /// </summary>
            YyyyMmDd = TimeStamp | Date
        }
    }

    /// <summary>
    /// Региональные форматы, принятые в системе
    /// </summary>
    public class ITFormatProvider : IFormatProvider
    {
        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(NumberFormatInfo))
            {
                return Text.PointDecimalSeparator;
            }
            if (formatType == typeof(DateTimeFormatInfo))
            {
                return Text.DateTimeFormat;
            }
            return null;
        }

       

    }


}
