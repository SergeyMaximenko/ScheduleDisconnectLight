using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ScheduleDisconnectLight
{

        /// <summary>
        /// Service for working with JSON-formatted string
        /// </summary>
        public class Json
        {
            private readonly JToken _jToken;

            /// <summary>
            /// <para>Create object for working with JSON-string</para>
            /// <para>Example:</para>
            /// <para><code>new JsonSerializer.Json("{...}")["Prop1"]["Prop2"].Value</code></para>
            /// </summary>
            /// <param name="jsonString">JSON-formatted string</param>
            public Json(string jsonString)
            {
                if (string.IsNullOrEmpty(jsonString))
                {
                    return;
                }
                _jToken = JToken.Parse(jsonString);
            }

            private Json(JToken jToken)
            {
                _jToken = jToken;
            }

            /// <summary>
            /// <para>Retrieve a specified property from a JSON object</para>
            /// <para>If the value doesn't exist, no error will be thrown</para>
            /// </summary>
            /// <param name="propertyName">Property name</param>
            public Json this[string propertyName]
            {
                get
                {
                    if (_jToken is JObject jObject)
                    {
                        // Ignore case
                        var property = jObject.Properties()
                                                .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
                        if (property != null)
                        {
                            return new Json(property.Value);
                        }
                    }
                    // An incorrect property was specified
                    return new Json(null);

                }
            }

            /// <summary>
            /// <para>Retrieve an array element by the specified index</para>
            /// <para>If the index is out of range, no error will be thrown</para>
            /// </summary>
            /// <param name="index">Array index</param>
            public Json this[int index]
            {
                get
                {
                    if (_jToken is JArray jArray)
                    {
                        if (index + 1 <= jArray.Count())
                        {
                            return new Json(jArray[index]);
                        }

                    }
                    // An incorrect index was specified
                    return new Json(null);
                }
            }

            /// <summary>
            /// Retrieve the number of elements in a list
            /// </summary>
            public int Count
            {
                get
                {
                    return _jToken.Count();
                }
            }


            /// <summary>
            /// Check if a property exists in the object
            /// </summary>
            public bool ContainsProperty(string propertyName)
            {
                return (_jToken is JObject jObject) && jObject.ContainsKey(propertyName);
            }

            /// <summary>
            /// Type of the current value
            /// </summary>
            public JsonType JsonType
            {
                get
                {
                    if (_jToken is JArray)
                    {
                        return JsonType.JArray;
                    }
                    if (_jToken is JObject)
                    {
                        return JsonType.JObject;
                    }
                    return JsonType.Value;
                }
            }

            /// <summary>
            /// Retrieve the value as the specified type
            /// </summary>
            public T GetValue<T>()
            {
                var emptyValue = default(T);
                if (_jToken == null)
                {
                    return emptyValue;
                }

                try
                {
                    return _jToken.ToObject<T>();
                }
                catch (Exception ex)
                {
                    var value = _jToken.Value<object>();
                    // Log the error (if needed)
                    throw new InvalidOperationException(string.Format(
@"Ошибка при чтении значения из JSON
Не удалось привести тип '{0}' к '{1}'
Ошибка: {2}
Значение: {3}", value?.GetType().Name, typeof(T), ex.Message, value));

                }
            }


            /// <summary>
            /// Value as a string
            /// </summary>
            public string Value => GetValue<string>();

            /// <summary>
            /// Value as an integer
            /// </summary>
            public int ValueInt => GetValue<int>();

            /// <summary>
            /// <para>Value as a date</para> 
            /// <para>Supported formats:</para>
            /// <list type="bullet">
            ///   <item>yyyy-MM-dd           // 2024-12-31 (date only)</item>
            ///   <item>yyyy.MM.dd           // 2024.12.31 (date only)</item>
            ///   <item>dd.MM.yyyy           // 31.12.2024 (date only)</item>
            ///   <item>dd-MM-yyyy           // 31-12-2024 (date only)</item>
            ///   <item>yyyy-MM-dd HH:mm:ss  // 2024-12-31 14:30:00 (date and time)</item>
            ///   <item>yyyy.MM.dd HH:mm:ss  // 2024.12.31 14:30:00 (date and time)</item>
            ///   <item>dd.MM.yyyy HH:mm:ss  // 31.12.2024 14:30:00 (date and time)</item>
            ///   <item>dd-MM-yyyy HH:mm:ss  // 31-12-2024 14:30:00 (date and time)</item>
            ///   <item>yyyy-MM-dd HH:mm     // 2024-12-31 14:30 (date and time without seconds)</item>
            ///   <item>yyyy.MM.dd HH:mm     // 2024.12.31 14:30 (date and time without seconds)</item>
            ///   <item>dd.MM.yyyy HH:mm     // 31.12.2024 14:30 (date and time without seconds)</item>
            ///   <item>dd-MM-yyyy HH:mm     // 31-12-2024 14:30 (date and time without seconds)</item>
            /// </list>
            /// </summary>
            public DateTime ValueDate
            {
                get
                {
                    string[] dateTimeFormats =
                    {
                        "yyyy-MM-dd",          // 2024-12-31 (date only)
						"yyyy.MM.dd",          // 2024.12.31 (date only)
						"dd.MM.yyyy",          // 31.12.2024 (date only)
						"dd-MM-yyyy",          // 31-12-2024 (date only)
						"yyyy-MM-dd HH:mm:ss", // 2024-12-31 14:30:00 (date and time)
						"yyyy.MM.dd HH:mm:ss", // 2024.12.31 14:30:00 (date and time)
						"dd.MM.yyyy HH:mm:ss", // 31.12.2024 14:30:00 (date and time)
						"dd-MM-yyyy HH:mm:ss", // 31-12-2024 14:30:00 (date and time)
						"yyyy-MM-dd HH:mm",    // 2024-12-31 14:30 (date and time without seconds)
						"yyyy.MM.dd HH:mm",    // 2024.12.31 14:30 (date and time without seconds)
						"dd.MM.yyyy HH:mm",    // 31.12.2024 14:30 (date and time without seconds)
						"dd-MM-yyyy HH:mm"     // 31-12-2024 14:30 (date and time without seconds)
					};

                    DateTime.TryParseExact(Value, dateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateTime);
                    return parsedDateTime;
                }
            }

            /// <summary>
            /// Value as a decimal number
            /// </summary>
            public decimal ValueDecimal => GetValue<decimal>();

            /// <summary>
            /// Value as a boolean
            /// </summary>
            public bool ValueBool => GetValue<bool>();

            /// <summary>
            /// Retrieve the JToken
            /// </summary>
            public JToken JToken => _jToken;

            /// <summary>
            /// Retrieve the array
            /// </summary>
            public IEnumerable<Json> GetArray()
            {
                if (_jToken is JArray jArray)
                {
                    foreach (var item in jArray)
                    {
                        yield return new Json(item);
                    }
                }
                else
                {
                    // Return an empty list
                    // Terminate iteration by returning an empty list
                    yield break;
                }
            }

            /// <summary>
            /// Retrieve an array cast to the specified type
            /// </summary>
            public IEnumerable<T> GetArray<T>()
            {
                return GetArray().Select(t => t.GetValue<T>());
            }


            /// <summary>
            /// Retrieve a dictionary
            /// </summary>
            public Dictionary<string, Json> GetDictionary()
            {
                if (_jToken is JObject jObject)
                {
                    var dictionary = new Dictionary<string, Json>(StringComparer.OrdinalIgnoreCase);
                    foreach (var property in jObject.Properties())
                    {
                        dictionary[property.Name] = new Json(property.Value);
                    }
                    return dictionary;
                }
                return new Dictionary<string, Json>();
            }
        }

        /// <summary>
        /// Type of value in JSON
        /// </summary>
        public enum JsonType
        {
            /// <summary>
            /// Object
            /// </summary>
            JObject,
            /// <summary>
            /// Array
            /// </summary>
            JArray,
            /// <summary>
            /// Simple value
            /// </summary>
            Value,
        }

  

}
