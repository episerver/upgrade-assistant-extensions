# Optimizely Upgrade Assistant Extensions

[![Continuous integration](https://github.com/episerver/upgrade-assistant-extensions/actions/workflows/ci.yml/badge.svg)](https://github.com/episerver/upgrade-assistant-extensions/actions/workflows/ci.yml)

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

#### packages.config

If there is a packages.config file under projectpath\\module\\_protected\\ then you need to remove it before start upgrading.

#### Database

If you have used ASPNET Identity and after migration you are not able to login or get exception like "SqlException: Invalid column name 'NormalizedUserName'.", 'ConcurrencyStamp', 'LockoutEnd', 'NormalizedEmail' or missing 'AspNetRoleClaims' table, the reason is the schema between ASPNET Identity versions has been changed and the resource doesn't exist in the db. Please run the migrate MigrateAspnetIdentity.sql script file under database folder. (OBS: we recommend to take a backup of database before perform the script).
