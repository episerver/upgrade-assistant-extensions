// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.UpgradeAssistant;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Epi.Source.Updater.Internal
{
    /// <summary>
    /// This class is temporary, ms upgrade-assistant always override the default (ms web extenstions startup and program)
    /// It is a workaround and should be removed when ms fixed the issue https://github.com/dotnet/upgrade-assistant/issues/989
    /// </summary>
    public class EpiTemplateInserterStep : UpgradeStep
    {

        private const int BufferSize = 65536;
        private static readonly Regex PropertyRegex = new(@"^\$\((.*)\)$", RegexOptions.Compiled);
        private readonly TemplateConfig _templates;

        public EpiTemplateInserterStep(ILogger<EpiTemplateInserterStep> logger) : base(logger)
        {
            _templates = new TemplateConfig()
            {
                Templates = new List<TemplateItems>()
                {
                    new TemplateItems() { Name = "Program.cs", Path="Epi.Source.Updater.Templates.EPiServerTemplates.Program.cs", Type = new ProjectItemType("Compile")},
                    new TemplateItems() { Name = "Startup.cs", Path="Epi.Source.Updater.Templates.EPiServerTemplates.Startup.cs", Type = new ProjectItemType("Compile")},
                 }
            };
        }

        public override string Description => $"Add Epi template files (Program and Startup)";

        public override string Title => $"Add Epi template files (Program and Startup)";

        public override string Id => WellKnownStepIds.ConfigUpdaterStepId;

        public override IEnumerable<string> DependsOn { get; } = new[]
        {
            // Project should be backed up before adding template files
            WellKnownStepIds.BackupStepId,

            // Project should be SDK-style before adding template files
            WellKnownStepIds.TryConvertProjectConverterStepId,

            // Project should have correct TFM
            WellKnownStepIds.SetTFMStepId,
            WellKnownStepIds.TemplateInserterStepId
        };

        protected override Task<bool> IsApplicableImplAsync(IUpgradeContext context, CancellationToken token) => Task.FromResult(context?.CurrentProject is not null);

        protected override Task<UpgradeStepInitializeResult> InitializeImplAsync(IUpgradeContext context, CancellationToken token)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            return Task.Run(() => new UpgradeStepInitializeResult(UpgradeStepStatus.Incomplete, "All expected template items found", BuildBreakRisk.None));
        }

        protected override async Task<UpgradeStepApplyResult> ApplyImplAsync(IUpgradeContext context, CancellationToken token)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var project = context.CurrentProject.Required();
            var projectFile = project.GetFile();

            // For each item to be added, make necessary replacements and then add the item to the project
            foreach (var item in _templates.Templates)
            {
                var filePath = Path.Combine(project.FileInfo.DirectoryName, item.Name);

                // If the file already exists, move it
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    // There is a bug in upgrade-assistant that should be fixed before we use rename. 
                    // projectFile.RenameFile(filePath);
                }

                // Get the contents of the template file
                try
                {
                    var tokenReplacements = ResolveTokenReplacements(new Dictionary<string, string>() { { "WebApplication1", "$(RootNamespace)" } }, projectFile);
                    var assembly = Assembly.GetExecutingAssembly();
                    using Stream templateStream = assembly.GetManifestResourceStream(item.Path);
                    if (templateStream is null)
                    {
                        Logger.LogCritical("Expected template {TemplatePath} not found", item.Path);
                        return new UpgradeStepApplyResult(UpgradeStepStatus.Failed, $"Expected template {item.Path} not found");
                    }

                    using var outputStream = File.Create(filePath, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                    await StreamHelpers.CopyStreamWithTokenReplacementAsync(templateStream, outputStream, tokenReplacements).ConfigureAwait(false);
                }
                catch (IOException exc)
                {
                    Logger.LogCritical(exc, "Expected template {TemplatePath}", item.Path);
                    return new UpgradeStepApplyResult(UpgradeStepStatus.Failed, $"Expected template {item.Path} not found");
                }
                await projectFile.SaveAsync(token).ConfigureAwait(false);

                Logger.LogInformation("Added template file {ItemName}", item.Name);
            }

            // After adding the items on disk, reload the workspace and check whether they were picked up automatically or not
            await context.ReloadWorkspaceAsync(token).ConfigureAwait(false);
            foreach (var item in _templates.Templates)
            {
                if (!projectFile.ContainsItem(item.Name, item.Type, token))
                {
                    // Add the new item to the project if it wasn't auto-included
                    projectFile.AddItem(item.Type.Name, item.Name);
                    Logger.LogDebug("Added {ItemName} to project file", item.Name);
                }
            }

            await projectFile.SaveAsync(token).ConfigureAwait(false);

            Logger.LogInformation("{ItemCount} template items added", _templates.Templates.Count());
            return new UpgradeStepApplyResult(UpgradeStepStatus.Complete, $"{_templates.Templates.Count()} template items added");
        }


        /// <summary>
        /// By default, upgrade steps' status resets when a new project is selected (so the same
        /// step can be applied to multiple projects in a single Upgrade Assistant session). If
        /// that heuristic is not correct for determining when to reset a given step's status,
        /// it can be changed by overriding ShouldReset.
        /// </summary>
        /// <param name="context">The upgrade context to make a decision about resetting the upgrade step for.</param>
        /// <returns>True if the upgrade step status should reset; false otherwise.</returns>
        protected override bool ShouldReset(IUpgradeContext context) => base.ShouldReset(context);

        /// <summary>
        /// Gets or sets the sub-steps for upgrade steps with sub-steps (like the sourcde updater step). If an upgrade
        /// step has children steps, the step should create them explicitly when it is created instead of depending
        /// on the dependency injection system to instantiate them.
        /// </summary>
        public override IEnumerable<UpgradeStep> SubSteps { get => base.SubSteps; protected set => base.SubSteps = value; }

        /// <summary>
        /// Gets or sets the parent step for a child step in a sub-step scenario (for example, the code fixer steps in
        /// source update scenarios have the source updater step as their parent).
        /// </summary>
        public override UpgradeStep? ParentStep { get => base.ParentStep; protected set => base.ParentStep = value; }

        private Dictionary<string, string> ResolveTokenReplacements(IEnumerable<KeyValuePair<string, string>>? replacements, IProjectFile project)
        {
            var propertyCache = new Dictionary<string, string?>();
            var ret = new Dictionary<string, string>();

            if (replacements is not null)
            {
                foreach (var replacement in replacements)
                {
                    var regexMatch = PropertyRegex.Match(replacement.Value);
                    if (regexMatch.Success)
                    {
                        // If the user specified an MSBuild property as a replacement value ($(...))
                        // then lookup the property value
                        var propertyName = regexMatch.Groups[1].Captures[0].Value;
                        string? propertyValue;

                        if (propertyCache.ContainsKey(propertyName))
                        {
                            propertyValue = propertyCache[propertyName];
                        }
                        else
                        {
                            propertyValue = project.GetPropertyValue(propertyName);
                            propertyCache[propertyName] = propertyValue;
                        }

                        if (!string.IsNullOrWhiteSpace(propertyValue))
                        {
                            Logger.LogDebug("Resolved project property {PropertyKey} to {PropertyValue}", propertyName, propertyValue);
                            ret.Add(replacement.Key, propertyValue!);
                        }
                        else
                        {
                            Logger.LogWarning("Could not resove project property {PropertyName}; not replacing token {Token}", propertyName, replacement.Key);
                        }
                    }
                    else
                    {
                        // If the replacement value is a string, then just add it directly to the return dictionary
                        ret.Add(replacement.Key, replacement.Value);
                    }
                }
            }

            return ret;
        }
    }
}
