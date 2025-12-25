using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Service
{
    /// <summary>
    /// Инструменты для работы с типами данных
    /// </summary>
    public static class TypeTools
    {
        /// <summary>
		/// Проверить пустое значение объекта или нет
		/// </summary>
		/// <param name="value">Объект</param>
		/// <returns>Признак что значение пустое</returns>
		[Pure]
        public static bool Empty(object value)
        {
            if (value == null || value is DBNull)
            {
                return true;
            }
            if (value is string)
            {
                return string.IsNullOrWhiteSpace((string)value);
            }
            var valueType = value.GetType();
            // Если это класс и он не равен null - возвращаем false
            if (!valueType.IsValueType)
            {
                return false;
            }
            // Если это структура - создаем значение по умолчанию и сравниваем с ним
            var emptyValue = Activator.CreateInstance(valueType);
            return Equals(emptyValue, value);
        }

        /// <summary>
        /// Преобразовывает значение в значение нужного типа
        /// </summary>
        /// <param name="value">Значение для преобразования</param>
        /// <param name="type">Тип, в который преобразовать</param>
        /// <returns>Результат в указанном типе</returns>
        [Pure]
        public static object Convert(object value, Type type)
        {
            // Обработать результат
            if (value == null || value is DBNull)
            {
                // Получили null
                if (type == typeof(string))
                {
                    return string.Empty;
                }
                if (type.IsValueType)
                {
                    return Activator.CreateInstance(type);
                }
                return null;
            }

            if (type.IsInstanceOfType(value))
            {
                // Если указали правильный тип результата - просто привести тип
                return value;
            }

            // Если мы выполняем конвертацию между типами int => decimal?, byte => int? и т.д.
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var nullableType = Nullable.GetUnderlyingType(type);
                // Поскольку типы разные - то выполняем сначала конвертацию
                var nullableConvertedValue = System.Convert.ChangeType(value, nullableType);

                // Создаем экземпляр класса
                return Activator.CreateInstance(type, new[] { nullableConvertedValue });
            }

            if (type.IsEnum)
            {
                // object -> перечисление по MapValue
                return EnumAttributes.ValueToEnum(value, type);
            }
            if (value.GetType().IsEnum)
            {
                // перечисление -> object по MapValue
                return EnumAttributes.EnumToValue((Enum)value);
            }

            if (value is string && type == typeof(byte[]))
            {
                return Text.NonUnicodeEncoding.GetBytes((string)value);
            }

            if (value is byte[] && type == typeof(string))
            {
                return Text.NonUnicodeEncoding.GetString((byte[])value);
            }
            if (value is Guid && type == typeof(byte[]))
            {
                return ((Guid)value).ToByteArray();
            }
            if (type == typeof(Guid) && value is byte[] && ((byte[])value).Length == 16)
            {
                return new Guid((byte[])value);
            }
            if (type == typeof(Guid) && value is string valStr && Text.CompareEx(valStr, string.Empty))
            {
                return Guid.Empty;
            }
            if (value is Guid)
            {
                return value.ToString();
            }
            if (value is string && type == typeof(decimal))
            {
                value = Text.Replace(value.ToString(), ",", ".");
            }
            if (value is string && string.IsNullOrEmpty(value.ToString()))
            {
                return TypeTools.GetDefaultValue(type);
            }

            return convert(value, type);
        }

        /// <summary>
        /// Преобразовывает значение в значение нужного типа
        /// </summary>
        /// <typeparam name="T">Тип, в который преобразовать</typeparam>
        /// <param name="value">Значение для преобразования</param>
        /// <returns>Результат в указанном типе</returns>
        [Pure]
        public static T Convert<T>(object value)
        {
            return (T)Convert(value, typeof(T));
        }

        /// <summary>
        /// Выполнить конвертацию. Вынесено в отдельный метод для пометки атрибутом <see cref="DebuggerStepThroughAttribute"/>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [DebuggerStepThrough]
        private static object convert(object value, Type type)
        {
            object returnResult = null;
            try
            {
                returnResult = System.Convert.ChangeType(value, type, Text.FormatProvider);
            }
            catch (InvalidCastException)
            {
                // Если не удалось конвертировать с помощью ChangeType - пробуем сделать конвертацию с помощью implicit или explicit операторов
                // Используется, например при конвертации string -> SqlCmdText
                Type typeOfValue = value.GetType();
                foreach (var methodInfo in getImplicitAndExplicitOperators(type))
                {
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeOfValue)
                    {
                        // Нашли нужный оператор (с необходимым типом параметра)
                        return methodInfo.Invoke(null, new[] { value });
                    }
                }
            }
            return returnResult;
        }

        /// <summary>
        /// Кэш explicit и explicit операторов
        /// </summary>
        private static readonly Dictionary<Type, MethodInfo[]> _operatorsCache = new Dictionary<Type, MethodInfo[]>();

        /// <summary>
        /// Возвращает implicit и explicit операторы типа 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static IEnumerable<MethodInfo> getImplicitAndExplicitOperators(Type type)
        {
            MethodInfo[] operators;
            if (!_operatorsCache.TryGetValue(type, out operators))
            {
                // В кэше не удалось найти операторы типа
                var operatorsList = new List<MethodInfo>();
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
                {
                    if (method.Name == "op_Implicit" || method.Name == "op_Explicit")
                    {
                        operatorsList.Add(method);
                    }
                }
                operators = operatorsList.ToArray();
                // Кэшируем операторы, чтобы в следующий раз не разбирать тип
                _operatorsCache[type] = operators;
            }
            return operators;
        }

        /// <summary>
        /// Получить значение по умолчанию для типа
        /// </summary>
        /// <param name="type">Тип</param>
        /// <returns></returns>
        [Pure]
        public static object GetDefaultValue(Type type)
        {
            switch (type.Name)
            {
                case "String":
                    return String.Empty;
                case "Int16":
                    return (Int16)0;
                case "Int32":
                    return 0;
                case "Int64":
                    return 0L;
                case "Decimal":
                    return 0m;
                case "Double":
                    return 0d;
                case "DateTime":
                    return DateTime.MinValue;
                case "Byte[]":
                    return new byte[0];
                default:
                    //дополнительная обработка для bool и enum'ов
                    return type.IsValueType ?
                        Activator.CreateInstance(type) : null;
            }
        }

        /// <summary>
        /// Получить тип по его имени в виде "assembly!typeName"
        /// </summary>
        /// <param name="typeName">Имя типа</param>
        /// <returns></returns>
        /// <exclude />
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public static Type GetTypeByName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }
            var enumDescAr = typeName.Split('!');
            if (enumDescAr.Length != 2)
            {
                return null;
            }
            var dll = enumDescAr[0];
            var type = enumDescAr[1];
            var dllWithoutExtension = Path.GetFileNameWithoutExtension(dll);
            if (dllWithoutExtension == null)
            {
                return null;
            }
            try
            {
                var asm = Assembly.Load(dllWithoutExtension);
                return asm == null ? null : asm.GetType(type);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Возвращает список значений побитового перечисления, входящих в заданное значение перечисления, исключая составные значения
        /// </summary>
        /// <typeparam name="T">Перечисление</typeparam>
        /// <param name="value">Заданное значение перечисления</param>
        /// <returns>Значения, входящие в заданное значение перечисления</returns>
        public static T[] EnumFlagsToArray<T>(Enum value)
        {
            if (!(value is T))
            {
                return null;
            }
            var enumItems = new List<T>();
            foreach (Enum enumItem in Enum.GetValues(value.GetType()))
            {
                var intItem = System.Convert.ToInt64(enumItem);
                if (value.HasFlag(enumItem) && (intItem != 0)
                    // проверка того, что числовое значение перечисления является степенью двойки,
                    // то есть элемент перечисления не является составным
                    && (intItem & (intItem - 1)) == 0)
                {
                    enumItems.Add((T)(object)enumItem);
                }
            }
            return enumItems.ToArray();
        }

        #region IsNumeric

        /// <summary>
        /// Проверить имеет ли объект числовой тип данных.
        /// </summary>
        /// <param name="o">Объект для проверки.</param>
        /// <returns>Объект имеет числовой тип.</returns>
        public static bool IsNumeric(object o)
        {
            return IsNumeric(o.GetType());
        }

        /// <summary>
        /// Проверить является ли тип данных числовым
        /// </summary>
        public static bool IsNumeric(Type type)
        {
            return IsNumeric(Type.GetTypeCode(type)) && !type.IsEnum;
        }

        /// <summary>
        /// Проверить является ли тип данных числовым
        /// </summary>
        public static bool IsNumeric(TypeCode typeCode)
        {
            switch (typeCode)
            {
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Byte:
                case TypeCode.Char:
                case TypeCode.Decimal:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Double:
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        /// <summary>
        /// Конвертация TypeCode в тип
        /// </summary>
        /// <param name="typeCode"></param>
        /// <returns></returns>
        [Pure]
        public static Type TypeCodeToType(TypeCode typeCode)
        {
            switch (typeCode)
            {
                case TypeCode.DBNull:
                    return typeof(DBNull);
                case TypeCode.Boolean:
                    return typeof(bool);
                case TypeCode.Char:
                    return typeof(char);
                case TypeCode.SByte:
                    return typeof(sbyte);
                case TypeCode.Byte:
                    return typeof(byte);
                case TypeCode.Int16:
                    return typeof(Int16);
                case TypeCode.UInt16:
                    return typeof(UInt16);
                case TypeCode.Int32:
                    return typeof(Int32);
                case TypeCode.UInt32:
                    return typeof(UInt32);
                case TypeCode.Int64:
                    return typeof(Int64);
                case TypeCode.UInt64:
                    return typeof(UInt64);
                case TypeCode.Single:
                    return typeof(Single);
                case TypeCode.Double:
                    return typeof(Double);
                case TypeCode.Decimal:
                    return typeof(Decimal);
                case TypeCode.DateTime:
                    return typeof(DateTime);
                case TypeCode.String:
                    return typeof(string);
                //case TypeCode.Empty:
                //case TypeCode.Object:
                default:
                    return typeof(Object);
            }
        }
    }
}
