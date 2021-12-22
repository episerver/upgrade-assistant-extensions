// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.UpgradeAssistant;
using System.Collections.Generic;

namespace Epi.Source.Updater.Internal
{
    internal class TemplateConfig
    {
        public IEnumerable<TemplateItems> Templates{ get; set; }

    }

    public class TemplateItems
    {
        public string Name { get; set; }
        public ProjectItemType Type { get; set; }
        public string Path { get; set; }
    }
}