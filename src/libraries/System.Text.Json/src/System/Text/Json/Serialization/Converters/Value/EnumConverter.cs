﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace System.Text.Json.Serialization.Converters
{
    internal class EnumConverter<T> : JsonConverter<T>
        where T : struct, Enum
    {
        private class EnumInformation
        {
            public EnumInformation(ulong[] values, string[] names)
            {
                Values = values;
                Names = names;
            }

            public ulong[] Values;
            public string[] Names;
        }

        private static readonly TypeCode s_enumTypeCode = Type.GetTypeCode(typeof(T));
        private static readonly bool s_isFlag = typeof(T).IsDefined(typeof(FlagsAttribute), false);
        // Odd type codes are conveniently signed types (for enum backing types).
        private static readonly string? s_negativeSign = ((int)s_enumTypeCode % 2) == 0 ? null : NumberFormatInfo.CurrentInfo.NegativeSign;

        private readonly EnumConverterOptions _converterOptions;
        private readonly JsonNamingPolicy _namingPolicy;
        private readonly ConcurrentDictionary<string, string>? _nameCache;
        private readonly ConcurrentDictionary<string, EnumInformation>? _enumInformationCache;
        private const string _commaSeparator = ", ";

        public override bool CanConvert(Type type)
        {
            return type.IsEnum;
        }

        public EnumConverter(EnumConverterOptions options)
            : this(options, namingPolicy: null)
        {
        }

        public EnumConverter(EnumConverterOptions options, JsonNamingPolicy? namingPolicy)
        {
            _converterOptions = options;
            if (namingPolicy != null)
            {
                _nameCache = new ConcurrentDictionary<string, string>();
                if (s_isFlag)
                {
                    _enumInformationCache = new ConcurrentDictionary<string, EnumInformation>();
                }
            }
            else
            {
                namingPolicy = JsonNamingPolicy.Default;
            }
            _namingPolicy = namingPolicy;
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            JsonTokenType token = reader.TokenType;

            if (token == JsonTokenType.String)
            {
                if (!_converterOptions.HasFlag(EnumConverterOptions.AllowStrings))
                {
                    ThrowHelper.ThrowJsonException();
                    return default;
                }

                // Try parsing case sensitive first
                string? enumString = reader.GetString();
                if (!Enum.TryParse(enumString, out T value)
                    && !Enum.TryParse(enumString, ignoreCase: true, out value))
                {
                    ThrowHelper.ThrowJsonException();
                    return default;
                }
                return value;
            }

            if (token != JsonTokenType.Number || !_converterOptions.HasFlag(EnumConverterOptions.AllowNumbers))
            {
                ThrowHelper.ThrowJsonException();
                return default;
            }

            switch (s_enumTypeCode)
            {
                // Switch cases ordered by expected frequency

                case TypeCode.Int32:
                    if (reader.TryGetInt32(out int int32))
                    {
                        return Unsafe.As<int, T>(ref int32);
                    }
                    break;
                case TypeCode.UInt32:
                    if (reader.TryGetUInt32(out uint uint32))
                    {
                        return Unsafe.As<uint, T>(ref uint32);
                    }
                    break;
                case TypeCode.UInt64:
                    if (reader.TryGetUInt64(out ulong uint64))
                    {
                        return Unsafe.As<ulong, T>(ref uint64);
                    }
                    break;
                case TypeCode.Int64:
                    if (reader.TryGetInt64(out long int64))
                    {
                        return Unsafe.As<long, T>(ref int64);
                    }
                    break;

                // When utf8reader/writer will support all primitive types we should remove custom bound checks
                // https://github.com/dotnet/runtime/issues/29000
                case TypeCode.SByte:
                    if (reader.TryGetInt32(out int byte8) && JsonHelpers.IsInRangeInclusive(byte8, sbyte.MinValue, sbyte.MaxValue))
                    {
                        sbyte byte8Value = (sbyte)byte8;
                        return Unsafe.As<sbyte, T>(ref byte8Value);
                    }
                    break;
                case TypeCode.Byte:
                    if (reader.TryGetUInt32(out uint ubyte8) && JsonHelpers.IsInRangeInclusive(ubyte8, byte.MinValue, byte.MaxValue))
                    {
                        byte ubyte8Value = (byte)ubyte8;
                        return Unsafe.As<byte, T>(ref ubyte8Value);
                    }
                    break;
                case TypeCode.Int16:
                    if (reader.TryGetInt32(out int int16) && JsonHelpers.IsInRangeInclusive(int16, short.MinValue, short.MaxValue))
                    {
                        short shortValue = (short)int16;
                        return Unsafe.As<short, T>(ref shortValue);
                    }
                    break;
                case TypeCode.UInt16:
                    if (reader.TryGetUInt32(out uint uint16) && JsonHelpers.IsInRangeInclusive(uint16, ushort.MinValue, ushort.MaxValue))
                    {
                        ushort ushortValue = (ushort)uint16;
                        return Unsafe.As<ushort, T>(ref ushortValue);
                    }
                    break;
            }

            ThrowHelper.ThrowJsonException();
            return default;
        }

        private static bool IsValidIdentifier(string value)
        {
            // Trying to do this check efficiently. When an enum is converted to
            // string the underlying value is given if it can't find a matching
            // identifier (or identifiers in the case of flags).
            //
            // The underlying value will be given back with a digit (e.g. 0-9) possibly
            // preceded by a negative sign. Identifiers have to start with a letter
            // so we'll just pick the first valid one and check for a negative sign
            // if needed.
            return (value[0] >= 'A' &&
                (s_negativeSign == null || !value.StartsWith(s_negativeSign)));
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            // If strings are allowed, attempt to write it out as a string value
            if (_converterOptions.HasFlag(EnumConverterOptions.AllowStrings))
            {
                string original = value.ToString();
                if (_nameCache != null && _nameCache.TryGetValue(original, out string? transformed))
                {
                    writer.WriteStringValue(transformed);
                    return;
                }

                if (IsValidIdentifier(original))
                {
                    // If this is an flag enum then it can be a combined value
                    // else convert as regular enum
                    if (s_isFlag)
                    {
                        transformed = ConvertFlagEnumToString(original, value);
                    }
                    else
                    {
                        transformed = _namingPolicy.ConvertName(original);
                    }

                    writer.WriteStringValue(transformed);
                    if (_nameCache != null)
                    {
                        _nameCache.TryAdd(original, transformed);
                    }
                    return;
                }
            }

            if (!_converterOptions.HasFlag(EnumConverterOptions.AllowNumbers))
            {
                ThrowHelper.ThrowJsonException();
            }

            switch (s_enumTypeCode)
            {
                case TypeCode.Int32:
                    writer.WriteNumberValue(Unsafe.As<T, int>(ref value));
                    break;
                case TypeCode.UInt32:
                    writer.WriteNumberValue(Unsafe.As<T, uint>(ref value));
                    break;
                case TypeCode.UInt64:
                    writer.WriteNumberValue(Unsafe.As<T, ulong>(ref value));
                    break;
                case TypeCode.Int64:
                    writer.WriteNumberValue(Unsafe.As<T, long>(ref value));
                    break;
                case TypeCode.Int16:
                    writer.WriteNumberValue(Unsafe.As<T, short>(ref value));
                    break;
                case TypeCode.UInt16:
                    writer.WriteNumberValue(Unsafe.As<T, ushort>(ref value));
                    break;
                case TypeCode.Byte:
                    writer.WriteNumberValue(Unsafe.As<T, byte>(ref value));
                    break;
                case TypeCode.SByte:
                    writer.WriteNumberValue(Unsafe.As<T, sbyte>(ref value));
                    break;
                default:
                    ThrowHelper.ThrowJsonException();
                    break;
            }
       }

        private string ConvertFlagEnumToString(string original, T value)
        {
            string ConvertEnum(EnumInformation enumInformation, T value)
            {
                int index = enumInformation.Names.Length - 1;
                ulong copyEnumValue = Unsafe.As<T, ulong>(ref value);
                StringBuilder valueBuilder = new StringBuilder();
                bool firstOccurrence = true;

                while (index >= 0)
                {
                    if (index == 0 && enumInformation.Values[index] == 0)
                    {
                        break;
                    }

                    if ((copyEnumValue & enumInformation.Values[index]) == enumInformation.Values[index])
                    {
                        copyEnumValue -= enumInformation.Values[index];

                        if (!firstOccurrence)
                        {
                            valueBuilder.Insert(0, _commaSeparator);
                        }

                        string transformed = _namingPolicy.ConvertName(enumInformation.Names[index]);
                        valueBuilder.Insert(0, transformed);
                        firstOccurrence = false;
                    }

                    index--;
                }
                return valueBuilder.ToString();
            }

            if (_enumInformationCache != null
                && _enumInformationCache.TryGetValue(original, out EnumInformation? enumInformation))
            {
                return ConvertEnum(enumInformation, value);
            }

            // If not in the cache then collect information about the enum
            // and convert to string value
            Type enumType = typeof(T);
            string[] enumAllNames = Enum.GetNames(enumType);
            ulong[] enumAllValues = new ulong[enumAllNames.Length];

            for (int i = 0; i < enumAllNames.Length; i++)
            {
                FieldInfo fieldInfo = enumType.GetField(
                    enumAllNames[i],
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!;
                enumAllValues[i] = Convert.ToUInt64(fieldInfo.GetValue(null));
            }

            enumInformation = new EnumInformation(enumAllValues, enumAllNames);
            if (_enumInformationCache != null)
            {
                _enumInformationCache.TryAdd(original, enumInformation);
            }

            return ConvertEnum(enumInformation, value);
        }
    }
}
