﻿// <copyright file="CommandLineConfigurationSource.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Extensions.Configuration;
    using Utility.CommandLine;

    /// <summary>
    ///     Extension methods for adding <see cref="CommandLineConfigurationProvider"/>.
    /// </summary>
    public static class CommandLineConfigurationExtensions
    {
        /// <summary>
        ///     Adds a command line argument configuration soruce to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to which to add.</param>
        /// <param name="targetType">The type from which to map properties.</param>
        /// <param name="multiValuedArguments">An array of argument names which can be specified with multiple values.</param>
        /// <param name="commandLine">The command line string from which to parse arguments.</param>
        /// <returns>The updated <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddCommandLine(this IConfigurationBuilder builder, Type targetType, string[] multiValuedArguments = null, string commandLine = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            return builder.AddCommandLine(s =>
            {
                s.TargetType = targetType;
                s.CommandLine = commandLine ?? Environment.CommandLine;
                s.MultiValuedArguments = multiValuedArguments;
            });
        }

        /// <summary>
        ///     Adds a command line argument configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to which to add.</param>
        /// <param name="configureSource">Configures the source.</param>
        /// <returns>The updated <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddCommandLine(this IConfigurationBuilder builder, Action<CommandLineConfigurationSource> configureSource)
            => builder.Add(configureSource);
    }

    /// <summary>
    ///     A command line argument <see cref="ConfigurationProvider"/>.
    /// </summary>
    public class CommandLineConfigurationProvider : ConfigurationProvider
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CommandLineConfigurationProvider"/> class.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public CommandLineConfigurationProvider(CommandLineConfigurationSource source)
        {
            TargetType = source.TargetType;
            Namespace = TargetType.Namespace.Split('.').First();
            CommandLine = source.CommandLine;
            MultiValuedArguments = source.MultiValuedArguments;
        }

        private string CommandLine { get; set; }
        private string Namespace { get; set; }
        private Type TargetType { get; set; }
        private string[] MultiValuedArguments { get; set; }

        /// <summary>
        ///     Parses command line arguments from the specified string and maps them to the corresponding keys.
        /// </summary>
        public override void Load()
        {
            var dictionary = Arguments.Parse(CommandLine, options => options.CombinableArguments = MultiValuedArguments).ArgumentDictionary;

            void Map(Type type, string path)
            {
                var props = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                foreach (PropertyInfo property in props)
                {
                    var attribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(ArgumentAttribute));
                    var key = ConfigurationPath.Combine(path, property.Name.ToLowerInvariant());

                    if (attribute != default)
                    {
                        var shortName = ((char)attribute.ConstructorArguments[0].Value).ToString();
                        var longName = (string)attribute.ConstructorArguments[1].Value;
                        var arguments = new[] { shortName, longName }.Where(i => !string.IsNullOrEmpty(i));

                        foreach (var argument in arguments)
                        {
                            if (dictionary.ContainsKey(argument))
                            {
                                // if the backing type is an array, it supports multiple values.
                                if (property.PropertyType.IsArray)
                                {
                                    var value = dictionary[argument];

                                    // Parse() will stuff multiple values into a List<T> if the argument name is
                                    // in the list of those supporting multiple values, and more than one value was supplied.
                                    // detect this, and add the values to the target.
                                    if (value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(List<>))
                                    {
                                        var elements = (List<object>)dictionary[argument];

                                        for (int i = 0; i < elements.Count; i++)
                                        {
                                            Data[ConfigurationPath.Combine(key, i.ToString())] = elements[i].ToString();
                                        }
                                    }
                                    else
                                    {
                                        // there may have only been one value supplied, in which case the value is just a
                                        // string.  stick it in index 0 of the target.
                                        Data[ConfigurationPath.Combine(key, "0")] = value.ToString();
                                    }
                                }
                                else
                                {
                                    var value = dictionary[argument].ToString();

                                    if (property.PropertyType == typeof(bool) && string.IsNullOrEmpty(value))
                                    {
                                        value = "true";
                                    }

                                    Data[key] = value;
                                }
                            }
                        }
                    }
                    else
                    {
                        Map(property.PropertyType, key);
                    }
                }
            }

            Map(TargetType, Namespace);
        }
    }

    /// <summary>
    ///     Represents command line arguments as an <see cref="IConfigurationSource"/>.
    /// </summary>
    public class CommandLineConfigurationSource : IConfigurationSource
    {
        /// <summary>
        ///     Gets or sets the command line string from which to parse arguments.
        /// </summary>
        public string CommandLine { get; set; }

        /// <summary>
        ///     Gets or sets the type from which to map properties.
        /// </summary>
        public Type TargetType { get; set; }

        /// <summary>
        ///     Gets or sets an array of argument names which can be specified with multiple values.
        /// </summary>
        public string[] MultiValuedArguments { get; set; } = Array.Empty<string>();

        /// <summary>
        ///     Builds the <see cref="CommandLineConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="CommandLineConfigurationProvider"/>.</returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder) => new CommandLineConfigurationProvider(this);
    }
}