# Optimizely Upgrade Assistant Extensions

This repository contains extensions to the Upgrade Assistant tool specific to Optimizely scenarios.

These extensions expand the upgrade-assistant tools's functionality to make Optimizely-specific changes during upgrade.

## Installation

Install the latest vesion of the upgrade-assistant `dotnet tool install -g upgrade-assistant` or upgrade `dotnet tool update -g upgrade-assistant`


Grab the latest release from [here](https://github.com/episerver/upgrade-assistant-extensions/releases) and unzip the file to a location of your computer (ex C:\temp\epi.source.updater).  Technically you should be able to point the zip file instead of extracting but there seems to be a bug in upgrade-assisant at the moment for that.

## Using the extension

```
upgrade-assistant upgrade {projectName}.csproj --extension "{extensionPath}" --ignore-unsupported-features
```

