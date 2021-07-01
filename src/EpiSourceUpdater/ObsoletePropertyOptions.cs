// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Epi.Source.Updater
{
    /// <summary>
    /// Configuration options for the EpiObsoleteUsingAnalyzer.
    /// Will be read from extension configuration by EpiObsoleteUsingAnalyzer's constructor.
    /// </summary>
    public class ObsoletePropertyOptions
    {
        /// <summary>
        /// Gets or sets the namespaces to include in the Usings property.
        /// </summary>
        public Collection<string>? Usings { get; set; }
    }
}
