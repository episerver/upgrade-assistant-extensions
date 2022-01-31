// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Epi.Source.Updater.Internal;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.DotNet.UpgradeAssistant;
using Microsoft.DotNet.UpgradeAssistant.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System;

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
        private const string ObsoleteOptionsSectionName = "ObsoleteOptions";

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

            // Options.
            services.AddExtensionOption<FindReplaceOptions>(FindReplaceOptionsSectionName);
            services.AddExtensionOption<ObsoletePropertyOptions>(ObsoleteOptionsSectionName);

            // Analyzers.
            services.Services.AddTransient<DiagnosticAnalyzer, EpiAttributeRemoverAnalyzer>();      // EP0001
            services.Services.AddTransient<DiagnosticAnalyzer, EpiClassReplacementsAnalyzer>();     // EP0002
            services.Services.AddTransient<DiagnosticAnalyzer, FindUIConfigurationReplacementAnalyzer>();     // EP0003
            services.Services.AddTransient<DiagnosticAnalyzer, EpiObsoleteTypesAnalyzer>();         // EP0004
            services.Services.AddTransient<DiagnosticAnalyzer, EpiObsoleteUsingAnalyzer>();         // EP0005
            services.Services.AddTransient<DiagnosticAnalyzer, EpiPartialControllerAnalyzer>();         // EP0006
            services.Services.AddTransient<DiagnosticAnalyzer, EpiDisplayChannelAnalyzer>();         // EP0007
            services.Services.AddTransient<DiagnosticAnalyzer, EpiMetadataAwareAnalyzer>();         // EP0008
            services.Services.AddTransient<DiagnosticAnalyzer, EpiPartialRouterAnalyzer>();         // EP0009
            services.Services.AddTransient<DiagnosticAnalyzer, EpiHttpContextBaseAccessorAnalyzer>();         // EP0010

            // Upgrade Step.
            services.Services.AddUpgradeStep<FindReplaceUpgradeStep>();
            services.Services.AddUpgradeStep<EpiTemplateInserterStep>();

            // Code Fixers.
            services.Services.AddTransient<CodeFixProvider, EpiAttributeRemoverCodeFixProvider>();  // EP0001
            services.Services.AddTransient<CodeFixProvider, EpiClassReplacementsCodeFixProvider>(); // EP0002
            services.Services.AddTransient<CodeFixProvider, FindUIConfigurationReplacementCodeFixProvider>(); // EP0003
            services.Services.AddTransient<CodeFixProvider, EpiObsoleteTypesCodeFixProvider>();     // EP0004
            services.Services.AddTransient<CodeFixProvider, EpiObsoleteUsingCodeFixProvider>();     // EP0005
            services.Services.AddTransient<CodeFixProvider, EpiPartialControllerCodeFixProvider>();     // EP0006
            services.Services.AddTransient<CodeFixProvider, EpiDisplayChannelCodeFixProvider>();     // EP0007
            services.Services.AddTransient<CodeFixProvider, EpiMetadataAwareCodeFixProvider>();     // EP0008
            services.Services.AddTransient<CodeFixProvider, EpiPartialRouterCodeFixProvider>();     // EP0009
            services.Services.AddTransient<CodeFixProvider, EpiHttpContextBaseAccessorCodeFixProvider>();     // EP0010
        }
    }
}
