
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;


namespace Service
{
    /// <summary>
    /// Параметры полей перечня (enum-a)
    /// </summary>
    [Serializable]
    public class EnumAttributes
    {
        /// <summary>
        /// Получить элемент перечня (enum-a) по его значению, которое записано в MapValue
        /// </summary>
        /// <typeparam name="T">Enum</typeparam>
        /// <param name="value">Значение, которое соответствует полю Enum-a</param>
        /// <returns>Элемент перечня</returns>
        /// <exception cref="System.InvalidCastException">В перечне не найден элемент, который соответствует переданному значению</exception>
        /// <exception cref="System.ArgumentException">Переданный тип T не является типом System.Enum</exception>
        public static T ValueToEnum<T>(object value)
        {
            return (T)ValueToEnum(value, typeof(T));
        }

        /// <summary>
        /// Получить элемент перечня (enum-a) по его значению, которое записано в MapValue
        /// </summary>
        ///  <param name="value">Значение, которое соответствует полю Enum-a</param>
        /// <param name="type">Тип перечисления</param>
        /// <returns>Элемент перечня</returns>
        /// <exception cref="System.InvalidCastException">В перечне не найден элемент, который соответствует переданному значению</exception>
        /// <exception cref="System.ArgumentException">Переданный тип T не является типом System.Enum</exception>
        internal static object ValueToEnum(object value, Type type)
        {
            return getInstance(type).GetEnumField(value);
        }

        /// <summary>
        /// Попробовать получить элемент перечня (enum-а) по его значению, которое записано в MapValue
        /// </summary>
        /// <typeparam name="T">Enum</typeparam>
        /// <param name="value">Значение, которое соответствует полю Enum-a</param>
        /// <param name="enumField">Элемент перечня</param>
        /// <exception cref="System.ArgumentException">Переданный тип T не является типом System.Enum</exception>
        /// <returns>Признак что в перечне найден элемент, который соответствует переданному значению</returns>
        public static bool TryGetEnumFromValue<T>(object value, out T enumField)
        {
            object enumFieldObject;
            var result = getInstance(typeof(T)).TryGetEnumField(value, out enumFieldObject);
            enumField = result ? (T)enumFieldObject : default(T);
            return result;
        }

        /// <summary>
        /// Получить значение заданное в MapValue по элементу перечня (enum-a)
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения</typeparam>
        /// <param name="enumField">Элемент перечня (enum-a), для которого нужно получить значение в MapValue</param>
        /// <returns>Значение, заданное в MapValue для элемента перечня</returns>
        /// <exception cref="System.InvalidCastException">Тип возвращаемого значения не соответствует типу, заданному в MapValue</exception>
        public static T EnumToValue<T>(Enum enumField)
        {
            return (T)EnumToValue(enumField);
        }

        /// <summary>
        /// Получить значение заданное в MapValue по элементу перечня (enum-a)
        /// </summary>
        /// <param name="enumField">Элемент перечня (enum-a), для которого нужно получить значение в MapValue</param>
        /// <returns>Значение, заданное в MapValue для элемента перечня</returns>

        internal static object EnumToValue(Enum enumField)
        {
            return getInstance(enumField.GetType()).GetValueByEnumField(enumField);
        }

        private static readonly Dictionary<Type, EnumAttributes> _cache = new Dictionary<Type, EnumAttributes>();
        /// <summary>
        /// Получить объект для статического приведения значения
        /// </summary>
        private static EnumAttributes getInstance(Type type)
        {
            lock (_cache)
            {
                EnumAttributes ea;
                if (_cache.TryGetValue(type, out ea))
                {
                    return ea;
                }
                ea = new EnumAttributes(type, false, true, false);
                _cache.Add(type, ea);
                return ea;
            }
        }

        /// <summary>
        /// Получить атрибут MapValue для заданного элемента enum-a
        /// </summary>
        /// <param name="enumField">Элемент перечня (enum-a), для которого нужно получить атрибут</param>
        /// <returns>Атрибут заданного элемента перечня</returns>
        public static IMapValue GetEnumAttribute(Enum enumField)
        {
            var attributes = new EnumAttributes(enumField.GetType()).GetEnumFieldAttributes(enumField);
            return attributes[0];
        }

        /// <summary>
        /// Получить все атрибуты перечисления.
        /// </summary>
        /// <exclude />
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public static ReadOnlyCollection<KeyValuePair<string, List<IMapValue>>> GetAllAttributes(Type enumType)
        {
            var enumParser = new EnumAttributes(enumType);
            return enumParser.GetAllAttributes();
        }

        /// <summary>
        /// Получить все атрибуты перечисления.
        /// </summary>
        /// <exclude />
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public ReadOnlyCollection<KeyValuePair<string, List<IMapValue>>> GetAllAttributes()
        {
            //вызов ToDictionary нужен чтобы отдать _копию_, чтобы на вызывающей стороне не испортили _fieldsIMapValue
            return _fieldsIMapValue.AsReadOnly();
        }

#if NETCOREAPP
		// Type нельзя сеаиализовать в .net core
		[NonSerialized]
#endif
        private Type _enumType;

#if NETCOREAPP
		[OnSerializing]
		private void onSerializing(StreamingContext context)
		{
			_enumTypeAsString = _enumType?.AssemblyQualifiedName;
		}
		
		private string _enumTypeAsString;
		[OnDeserialized]
		private void onDeserialized(StreamingContext context)
		{
			if (!string.IsNullOrWhiteSpace(_enumTypeAsString))
			{
				_enumType = Type.GetType(_enumTypeAsString);
			}
		}
#endif

        /// <summary>
        /// Создать экземпляр класса параметров полей Enum-a
        /// </summary>
        /// <param name="enumType">Тип enum-a</param>
        /// <exception cref="System.ArgumentException">Переданный тип <see cref="enumType"/> не является типом System.Enum</exception>
        public EnumAttributes(Type enumType)
            : this(enumType, true)
        {

        }

        /// <summary>
        /// Создать экземпляр класса параметров полей Enum-a
        /// </summary>
        /// <param name="enumType">Тип enum-a</param>
        /// <param name="createAttributes">Создавать атрибут <see cref="MapValueAttribute"/>, если элемент перечисления не имеет его</param>
        /// <exception cref="System.ArgumentException">Переданный тип <see cref="enumType"/> не является типом System.Enum</exception>
        public EnumAttributes(Type enumType, bool createAttributes) : this(enumType, false, createAttributes)
        {

        }

        /// <summary>
        /// Создать экземпляр класса параметров полей Enum-a
        /// </summary>
        /// <param name="enumType">Тип enum-a</param>
        /// <param name="readResource">Выполнять считку наименований из ресурсов. 
        /// В случае, если наименования для дальнейшей работы ПО не нужны считку выполнять не нужно в целях оптимизации</param>
        /// <param name="createAttributes">Создавать атрибут <see cref="MapValueAttribute"/>, если элемент перечисления не имеет его</param>
        /// <exception cref="System.ArgumentException">Переданный тип <see cref="enumType"/> не является типом System.Enum</exception>
        public EnumAttributes(Type enumType, bool readResource, bool createAttributes) : this(enumType, readResource, createAttributes, true)
        {
        }

        /// <summary>
        /// Создать экземпляр класса параметров полей Enum-a
        /// </summary>
        /// <param name="enumType">Тип enum-a</param>
        /// <param name="readResource">Выполнять считку наименований из ресурсов. 
        /// В случае, если наименования для дальнейшей работы ПО не нужны считку выполнять не нужно в целях оптимизации</param>
        /// <param name="createAttributes">Создавать атрибут <see cref="MapValueAttribute"/>, если элемент перечисления не имеет его</param>
        /// <param name="needOrder">Упорядочивать элементы</param>
        /// <exception cref="System.ArgumentException">Переданный тип <see cref="enumType"/> не является типом System.Enum</exception>
        internal EnumAttributes(Type enumType, bool readResource, bool createAttributes, bool needOrder)
        {
            _enumType = enumType;
            if (enumType.IsEnum)
            {
                // Получаем элементы перечня полей. Public+Static нужен чтобы не считывать служебные элементы enum'а
                FieldInfo[] enumFields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);

                // Выполняем сортировку
                IEnumerable<FieldInfo> orderedFields = needOrder ? getEnumFieldsOrdered(enumFields, enumType) : enumFields;

                // Считываем атрибуты
                foreach (var enumField in orderedFields)
                {
                    var enumFieldName = enumField.Name;

                    // Получаем заданный для enum-a атрибут
                    var enumFieldAttributes = getMapValueAttributes(enumField);
                    List<IMapValue> values = null;
                    if (enumFieldAttributes.Length > 0)
                    {
                        foreach (var enumFieldAttribute in enumFieldAttributes)
                        {
                            addField(enumFieldName, enumFieldAttribute, enumFieldAttribute.IsDefaultValue, ref values);
                        }
                    }
                    else if (createAttributes)
                    {
                        var attribute = new MapValueAttribute(enumFieldName);
                        addField(enumFieldName, attribute, false, ref values);
                    }
                }
            }
            else
            {
                throw new ArgumentException(
                    string.Format("Заданий тип '{0}' не є типом System.Enum", _enumType.Name));
            }
        }

        /// <summary>
        /// Возвращает элементы enum в порядке их следования
        /// </summary>
        /// <returns></returns>
        private IEnumerable<FieldInfo> getEnumFieldsOrdered(IEnumerable<FieldInfo> enumFields, Type enumType)
        {
            if (Enum.GetUnderlyingType(enumType) == typeof(ulong))
            {
                // Для ulong делаем особую обработку, так как не всегда можем конвертировать ulong -> long (например, ulong.MaxValue не можем)
                var orderedFields = new List<KeyValuePair<ulong, FieldInfo>>();
                foreach (var enumField in enumFields)
                {
                    var order = (ulong)Convert.ChangeType(enumField.GetValue(enumType), typeof(ulong));
                    orderedFields.Add(new KeyValuePair<ulong, FieldInfo>(order, enumField));
                }

                orderedFields.Sort((x, y) => x.Key.CompareTo(y.Key));
                return orderedFields.Select(orderedField => orderedField.Value);
            }
            else
            {
                var orderedFields = new List<KeyValuePair<long, FieldInfo>>();
                foreach (var enumField in enumFields)
                {
                    var order = (long)Convert.ChangeType(enumField.GetValue(enumType), typeof(long));
                    orderedFields.Add(new KeyValuePair<long, FieldInfo>(order, enumField));
                }

                orderedFields.Sort((x, y) => x.Key.CompareTo(y.Key));
                return orderedFields.Select(orderedField => orderedField.Value);
            }
        }

        private readonly List<KeyValuePair<string, List<IMapValue>>> _fieldsIMapValue = new List<KeyValuePair<string, List<IMapValue>>>();


#if NETCOREAPP
		// Type нельзя сеаиализовать в .net core
		[NonSerialized]
#endif
        private System.Resources.ResourceManager _resourceManager;
#if NETCOREAPP
		// Type нельзя сеаиализовать в .net core
		[NonSerialized]
#endif
        private bool _resourceInited;

        /// <summary>
        /// Получить значение атрибута <see cref="MapValueAttribute"/>
        /// </summary>
        private MapValueAttribute[] getMapValueAttributes(FieldInfo member)
        {
            var mapValues = (MapValueAttribute[])member.GetCustomAttributes(typeof(MapValueAttribute), true);
           
            return mapValues;
        }

        /// <summary>
        /// Признак что значение по умолчанию для enum-а установлено
        /// </summary>
        private bool _defaultValueIsSet;

        private string _defaultValue;

        /// <summary>
        /// Добавить элемент перечня
        /// </summary>
        /// <param name="fieldName">Имя элемента</param>
        /// <param name="attribute">Атрибут, которым помечен элемент</param>
        /// <param name="isDefault">Признак что данное значение будет задано по умолчанию</param>
        /// <param name="mapValues"> </param>
        private void addField(string fieldName, MapValueAttribute attribute, bool isDefault, ref List<IMapValue> mapValues)
        {
            if (mapValues != null)
            {
                mapValues.Add(attribute);
            }
            else
            {
                mapValues = new List<IMapValue> { attribute };
                _fieldsIMapValue.Add(new KeyValuePair<string, List<IMapValue>>(fieldName, mapValues));
            }
            if (isDefault && !_defaultValueIsSet)
            {
                _defaultValue = fieldName;
                _defaultValueIsSet = true;
            }
        }

        /// <summary>
        /// Получить наименование элемента перечня по заданному значению в MapValue
        /// </summary>
        /// <param name="enumValue">Значение элемента</param>
        /// <returns>Наименование элемента enum-a</returns>
        /// <exception cref="System.InvalidCastException">В перечне не найден элемент, который соответствует переданному значению</exception>
        public string GetEnumFieldName(object enumValue)
        {
            string fieldName;
            if (!TryGetEnumFieldName(enumValue, out fieldName))
            {
                throw new InvalidCastException(
                    string.Format("У переліку '{0}' не знайдено елемент, який відповідає значенню '{1} '", _enumType.Name, enumValue));
            }
            return fieldName;
        }

        /// <summary>
        /// Попробовать получить наименование элемента перечня по заданному значению в MapValue
        /// </summary>
        /// <param name="enumValue">Значение элемента</param>
        /// <param name="enumFieldName">Наименование элемента enum-а</param>
        /// <returns>Признак что в перечне найден элемент, который соответствует переданному значению</returns>
        public bool TryGetEnumFieldName(object enumValue, out string enumFieldName)
        {
            if (!tryGetValue(enumValue, out enumFieldName))
            {
                if (_defaultValue == null)
                {
                    return false;
                }
                enumFieldName = _defaultValue;
            }
            return true;
        }

        /// <summary>
        /// Получить элемент перечня по заданному значению в MapValue
        /// </summary>
        /// <param name="enumValue">Значение элемента</param>
        /// <returns>Элемента enum-a</returns>
        public object GetEnumField(object enumValue)
        {
            return Enum.Parse(_enumType, GetEnumFieldName(enumValue));
        }

        /// <summary>
        /// Получить элемент перечня по заданному значению в MapValue
        /// </summary>
        /// <param name="enumValue">Значение элемента</param>
        /// <returns>Элемента enum-a</returns>
        public T GetEnumField<T>(object enumValue)
        {
            return (T)GetEnumField(enumValue);
        }

        /// <summary>
        /// Получить элемент перечня, установленный как элемент по-умолчанию с помощью атрибута MapValue
        /// </summary>
        public object GetDefaultEnumField()
        {
            if (_defaultValue == null)
            {
                return null;
            }
            return Enum.Parse(_enumType, _defaultValue);
        }

        /// <summary>
        /// Попробовать получить элемент перечня по заданному значению в mapValue
        /// </summary>
        /// <param name="enumValue">Значение элемента</param>
        /// <param name="enumField">Элемент enum-a</param>
        /// <returns>Признак что в enum-e найден элемент, для которого установлено заданное значение</returns>
        public bool TryGetEnumField(object enumValue, out object enumField)
        {
            string enumFieldName;
            if (TryGetEnumFieldName(enumValue, out enumFieldName))
            {
                enumField = Enum.Parse(_enumType, enumFieldName);
                return true;
            }
            enumField = null;
            return false;
        }

        /// <summary>
        /// Получить значение заданное в MapValue по элементу перечня
        /// </summary>
        /// <param name="enumField">Имя элемента перечня</param>
        /// <returns>Значение, заданное в MapValue для элемента перечня</returns>
        /// <exception cref="System.InvalidCastException">В перечне не найден указанный элемент</exception>
        /// <exception cref="System.ArgumentException">Переданный элемент перечня не соответствует типу, заданному в конструкторе</exception>
        public object GetValueByEnumField(Enum enumField)
        {
            var firstAttribute = GetEnumFieldAttributes(enumField)[0];
            return firstAttribute.GetSqlValue<object>();
        }

        /// <summary>
        /// Получить атрибуты заданного элемента перечня
        /// </summary>
        /// <param name="enumField">Элемент перечня</param>
        /// <returns></returns>
        public List<IMapValue> GetEnumFieldAttributes(Enum enumField)
        {
            checkType(enumField);
            return GetEnumFieldAttributes(enumField.ToString());
        }

        /// <summary>
        /// Получить атрибуты заданного элемента перечня
        /// </summary>
        /// <param name="enumField">Элемент перечня</param>
        /// <returns></returns>
        public List<IMapValue> GetEnumFieldAttributes(string enumField)
        {
            foreach (var pair in _fieldsIMapValue)
            {
                if (pair.Key == enumField)
                {
                    return pair.Value;
                }
            }
            return null;
        }

        private void checkType(Enum enumField)
        {
            if (enumField.GetType() != _enumType)
            {
                throw new ArgumentException(
                    string.Format("Переданий елемент переліку '{0}' не є типом '{1}'", enumField,
                    _enumType == null ? string.Empty : _enumType.Name));
            }
        }

        private bool tryGetValue(object value, out string enumField)
        {
            enumField = null;
            if (value == null)
            {
                return false;
            }
            foreach (var field in _fieldsIMapValue)
            {
                foreach (var iMapValue in field.Value)
                {
                    var fieldAttribute = (MapValueAttribute)iMapValue;
                    if (fieldAttribute.IsEqualValue(value))
                    {
                        enumField = field.Key;
                        return true;
                    }
                }
            }
            return false;
        }
    }

   
	/// <summary>
	/// Параметры элемента перечня (enum-a)
	/// </summary>
	[Serializable, AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class MapValueAttribute : Attribute, IMapValue
    {
        private readonly object _sqlValue;
        private object _interValue;
        private object[] _obsoleteValues;
        private string _caption;

        /// <summary>
        /// Наименование элемента перечня.
        /// </summary>
        public string Caption
        {
            get { return _caption ?? CaptionResourceName; }
            set { _caption = value; }
        }

        /// <summary>
        /// <para>Код строки в ресурсах, с многоязычным наименованием перечня. Если задано - заменяет свойство <see cref="Caption"/></para>
        /// <para></para>
        /// </summary>
        /// <exclude />
        [System.ComponentModel.Browsable(false), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public string CaptionResourceName { get; set; }

        /// <summary>
        /// Создать атрибут для элемента перечня (enum-a)
        /// </summary>
        /// <param name="sqlValue">Значение поля в SQL</param>
        public MapValueAttribute(object sqlValue)
        {
            _sqlValue = sqlValue;
        }

        string IMapValue.GetCaption()
        {
            return Caption;
        }

        T IMapValue.GetSqlValue<T>()
        {
            object value = _internationalMode && _interValue != null ? _interValue : _sqlValue;
            if (value is T)
            {
                return (T)value;
            }
            return (T)Convert.ChangeType(value, typeof(T));
        }

        /// <summary>
        /// <para>Признак что данное поле установлено по умолчанию</para>
        /// </summary>
        public bool IsDefaultValue { get; set; }

        /// <summary>
        /// Альтернативное значение поля в SQL для интернационального режима
        /// </summary>
        public object InterValue
        {
            get { return _interValue; }
            set { _interValue = value; }
        }

        /// <summary>
        /// Устаревшие значения поля
        /// </summary>
        public object[] ObsoleteValues
        {
            get { return _obsoleteValues; }
            set { _obsoleteValues = value; }
        }

        /// <summary>
        /// Интернациональный режим
        /// </summary>
        private static bool _internationalMode;

        /// <summary>
        /// Установить интернациональный режим
        /// </summary>
        private static void setInternationalMode(bool value)
        {
            _internationalMode = value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal bool IsEqualValue(object value)
        {
            if (compareValues(_sqlValue, value))
            {
                return true;
            }
            if (_interValue != null && compareValues(_interValue, value))
            {
                return true;
            }
            if (_obsoleteValues != null)
            {
                foreach (var obsoleteValue in _obsoleteValues)
                {
                    if (compareValues(obsoleteValue, value))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Сравнить значения
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        private static bool compareValues(object value1, object value2)
        {
            var fieldValueType = value1.GetType();
            // Если типы одинаковые
            if (value2.GetType() == fieldValueType)
            {
                return value1.Equals(value2);
            }
            // Преобразовываем тип
            object convertedValue;
            try
            {
                convertedValue = Convert.ChangeType(value2, fieldValueType);
            }
            catch (Exception exception)
            {
                if (exception is InvalidCastException || exception is FormatException)
                {
                    return false;
                }
                throw;
            }
            return value1.Equals(convertedValue);
        }

        /// <summary>
        /// Получить перечисление возможных значений атрибута
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<object> GetPossibleValues()
        {
            yield return _sqlValue;
            if (_interValue != null)
            {
                yield return _interValue;
            }
            if (_obsoleteValues != null)
            {
                foreach (var obsoleteValue in _obsoleteValues)
                {
                    if (obsoleteValue != null)
                    {
                        yield return obsoleteValue;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Информация об атрибуте <see cref="MapValueAttribute"/>
    /// </summary>
    public interface IMapValue
    {
        /// <summary>
        /// Признак того, что данное поле установлено по умолчанию
        /// </summary>
        /// <returns></returns>
        bool IsDefaultValue
        {
            get;
            set;
        }

        /// <summary>
        /// Получить название элемента с учетом локализации
        /// </summary>
        /// <returns>Название элемента</returns>
        string GetCaption();

        /// <summary>
        /// Получить значение элемента в SQL
        /// </summary>
        /// <typeparam name="T">Тип, к которому следует привести значение</typeparam>
        /// <returns>Значение элемента в SQL, приведенное к выбранному типу</returns>
        T GetSqlValue<T>();
    }
}
