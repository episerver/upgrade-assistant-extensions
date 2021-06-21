// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.DotNet.UpgradeAssistant;
using Microsoft.DotNet.UpgradeAssistant.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Epi.Source.Updater
{
    /// <summary>
    /// Extension authors can implement the IExtensionServiceProvider interface to
    /// register services with Upgrade Assistant's dependency injection container.
    /// This could include registering additional upgrade steps. It might also include
    /// registering services needed by the steps registered or other migrations steps.
    /// For example, registering Roslyn analyzer/code fix providers, IConfigUpdaters,
    /// or IPackageReferenceAnalyzers will cause upgrade steps that use those types to
    /// pick the newly registered services up automatically and use them.
    /// </summary>
    public class EpiSourceUpdaterServiceProvider : IExtensionServiceProvider
    {

        private const string FindReplaceOptionsSectionName = "FindReplaceOptions";

        /// <summary>
        /// Registers services (the analyzer and code fix provider) comprising the
        /// SourceUpdaterSample extension into Upgrade Assistant's dependency injection container.
        /// </summary>
        /// <param name="services">A configuration object containing the service collection
        /// to register services in, the extension's configuration file, and a file provider
        /// for retrieving extension files.</param>
        public void AddServices(IExtensionServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            // Register the analyzer and code fix provider for this extension.
            // Even though this extension doesn't register any new upgrade steps,
            // these services will be picked up by existing upgrade steps that use
            // analzyers and code fix providers (like the SourceUpdaterStep and
            // RazorUpdaterStep).

            // Analyzers.
            services.Services.AddTransient<DiagnosticAnalyzer, EpiClassReplacementsAnalyzer>();
            services.Services.AddTransient<DiagnosticAnalyzer, TypeUpgradeAnalyzer>();
            services.Services.AddTransient<DiagnosticAnalyzer, EpiObsoleteTypesAnalyzer>();
            services.Services.AddTransient<DiagnosticAnalyzer, EpiAttributeRemoverAnalyzer>();

            // Upgrade Step.
            services.Services.AddUpgradeStep<FindReplaceUpgradeStep>();
            services.AddExtensionOption<FindReplaceOptions>(FindReplaceOptionsSectionName);

            // Code Fixers.
            services.Services.AddTransient<CodeFixProvider, EpiClassReplacementsCodeFixProvider>();
            services.Services.AddTransient<CodeFixProvider, TypeUpgradeCodeFixProvider>();
            services.Services.AddTransient<CodeFixProvider, EpiObsoleteTypesCodeFixProvider>();
            services.Services.AddTransient<CodeFixProvider, EpiAttributeRemoverCodeFixProvider>();
        }
    }
}
