﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Epi.Source.Updater
{
    public static class EpiClassMapLoader
    {
        /// <summary>
        /// The suffix that type map files are expected to use.
        /// </summary>
        private const string ClassMapFileSuffix = ".classmap";

        /// <summary>
        /// Load type mappings from additional files.
        /// </summary>
        /// <param name="additionalTexts">The additional texts to parse for type mappings.</param>
        /// <returns>Type mappings as defined in *.typemap files in the project's additional files.</returns>
        public static IEnumerable<TypeMapping> LoadMappings(ImmutableArray<AdditionalText> additionalTexts)
        {
            foreach (var file in additionalTexts)
            {
                if (file.Path.EndsWith(ClassMapFileSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var line in file.GetText()?.Lines ?? Enumerable.Empty<TextLine>())
                    {
                        var trimmedLine = line.ToString().Trim();
                        if (trimmedLine.StartsWith("#", StringComparison.Ordinal))
                        {
                            // Allow comments denoted by #
                            continue;
                        }

                        var components = trimmedLine.Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (components.Length == 2)
                        {
                            yield return new TypeMapping(components[0], components[1]);
                        }
                    }
                }
            }
        }
    }
}
