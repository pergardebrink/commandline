﻿// Copyright 2005-2015 Giacomo Stelluti Scala & Contributors. All rights reserved. See doc/License.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommandLine.Infrastructure;
using CSharpx;

namespace CommandLine.Core
{
    internal static class TypeConverter
    {
        public static Maybe<object> ChangeType(IEnumerable<string> values, Type conversionType, bool scalar, CultureInfo conversionCulture)
        {
            return scalar
                ? ChangeTypeScalar(values.Single(), conversionType, conversionCulture)
                : ChangeTypeSequence(values, conversionType, conversionCulture);
        }

        private static Maybe<object> ChangeTypeSequence(IEnumerable<string> values, Type conversionType, CultureInfo conversionCulture)
        {
            var type =
                conversionType.GetGenericArguments()
                              .SingleOrDefault()
                              .ToMaybe()
                              .FromJust(
                                  new ApplicationException("Non scalar properties should be sequence of type IEnumerable<T>."));

            var converted = values.Select(value => ChangeTypeScalar(value, type, conversionCulture));

            return converted.Any(a => a.MatchNothing())
                ? Maybe.Nothing<object>()
                : Maybe.Just(converted.Select(c => ((Just<object>)c).Value).ToArray(type));
        }

        private static Maybe<object> ChangeTypeScalar(string value, Type conversionType, CultureInfo conversionCulture)
        {
            try
            {
                Func<object> safeChangeType = () =>
                    {
                        var isFsOption = ReflectionHelper.IsFSharpOptionType(conversionType);

                        Func<Type> getUnderlyingType = () =>
                            isFsOption
                                ? FSharpOptionHelper.GetUnderlyingType(conversionType)
                                : Nullable.GetUnderlyingType(conversionType);

                        var type = getUnderlyingType() ?? conversionType;

                        Func<object> withValue = () =>
                            isFsOption
                                ? FSharpOptionHelper.Some(type, Convert.ChangeType(value, type, conversionCulture))
                                : Convert.ChangeType(value, type, conversionCulture);

                        Func<object> empty = () =>
                            isFsOption
                                ? FSharpOptionHelper.None(type)
                                : null;

                        return (value == null) ? empty() : withValue();
                    };

                return Maybe.Just(
                    MatchBoolString(value)
                        ? ConvertBoolString(value)
                        : conversionType.IsEnum
                            ? ConvertEnumString(value, conversionType)
                            : safeChangeType());
            }
            catch (InvalidCastException)
            {
                return Maybe.Nothing<object>();
            }
            catch (FormatException)
            {
                return Maybe.Nothing<object>();
            }
            catch (OverflowException)
            {
                return Maybe.Nothing<object>();
            }
        }

        private static Either<object, Exception> ChangeTypeScalarImpl(string value, Type conversionType, CultureInfo conversionCulture)
        {
            Func<string, object> changeType = input =>
            {
                Func<object> safeChangeType = () =>
                {
                    var isFsOption = ReflectionHelper.IsFSharpOptionType(conversionType);

                    Func<Type> getUnderlyingType =
                        () =>
                            isFsOption
                                ? FSharpOptionHelper.GetUnderlyingType(conversionType)
                                : Nullable.GetUnderlyingType(conversionType);

                    var type = getUnderlyingType() ?? conversionType;

                    Func<object> withValue =
                        () =>
                            isFsOption
                                ? FSharpOptionHelper.Some(type, Convert.ChangeType(input, type, conversionCulture))
                                : Convert.ChangeType(input, type, conversionCulture);

                    Func<object> empty = () => isFsOption ? FSharpOptionHelper.None(type) : null;

                    return (input == null) ? empty() : withValue();
                };

                return MatchBoolString(input)
                    ? ConvertBoolString(input)
                    : conversionType.IsEnum ? ConvertEnumString(input, conversionType) : safeChangeType();
            };
            return Either.Protect(changeType, value);
        }

        private static bool MatchBoolString(string value)
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ConvertBoolString(string value)
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static object ConvertEnumString(string value, Type conversionType)
        {
            object parsedValue;
            try
            {
                parsedValue = Enum.Parse(conversionType, value);
            }
            catch (ArgumentException)
            {
                throw new FormatException();
            }
            if (Enum.IsDefined(conversionType, parsedValue))
            {
                return parsedValue;
            }
            throw new FormatException();
        }
    }
}