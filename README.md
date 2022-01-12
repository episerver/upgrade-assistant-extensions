# Optimizely Upgrade Assistant Extensions

This repository contains extensions to the Upgrade Assistant tool specific to Optimizely scenarios.

These extensions expand the upgrade-assistant tools's functionality to make Optimizely-specific changes during upgrade.

## Installation

Install the latest version of the upgrade-assistant `dotnet tool install -g upgrade-assistant` or upgrade `dotnet tool update -g upgrade-assistant`

Grab the latest release from [here](https://github.com/episerver/upgrade-assistant-extensions/releases) and unzip the file to a location of your computer (ex C:\temp\epi.source.updater).  Technically you should be able to point the zip file instead of extracting but there seems to be a bug in upgrade-assistant at the moment for that.

## Using the extension

Some of the source code upgrade code analysers requires that packages referenced are available in selected target framework. So for now when CMS targets net5.0 the recommendation is to target net5.0 when converting as well (target framework can be set to net6.0 after conversion is completed). To achieve this first set environment variable like:

```bash
set DefaultTargetFrameworks__LTS=net5.0
```

then run upgrade-assistant with flag *--target-tfm-support LTS*, like:

```bash
upgrade-assistant upgrade {projectName}.csproj --extension "{extensionPath}" --ignore-unsupported-features --target-tfm-support LTS
```

### Known Issues

If packages.config file exists under projectpath\\module\\_protected\\ then you need to remove it before start upgrading.
