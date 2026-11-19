# Steeltoe InitializrConfiguration

[![Build Status](https://github.com/SteeltoeOSS/InitializrConfig/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/SteeltoeOSS/InitializrConfig/actions/workflows/build.yml?query=branch%3Amain)

Configuration for [Steeltoe InitializrWeb](https://github.com/SteeltoeOSS/InitializrWeb)

## Configuration files

[Steeltoe InitializrService](https://github.com/SteeltoeOSS/InitializrService) loads its UI configuration from
Spring Cloud Config Server, which serves `SteeltoeInitializr.yaml` overlaid with the environment-specific
`SteeltoeInitializr-{environment}.yaml` file (for example, `SteeltoeInitializr-Staging.yaml`).

The overlay is applied per setting, with list entries addressed by index. When an environment file overrides a list
from `SteeltoeInitializr.yaml`, it must override every entry and property of that list; otherwise the leftover base
settings are merged into the environment's list.

## Validating configuration changes

The tests in `test/Initializr.ConfigTests` validate the effective configuration of each environment without requiring
a running server. They check for unknown settings, missing or invalid defaults, duplicate dependency IDs and base list
entries leaking into environment lists. They run as part of the CI build, and can be run locally using:

```shell
dotnet test test/Initializr.ConfigTests
```

The tests in `test/Initializr.ApiTests` download and build projects for the options in `SteeltoeInitializr.yaml` from a
running InitializrService instance. The API URL is read from `test-settings.yaml` and can be overridden using the
`INITIALIZR_API_URL` environment variable.
