# toofz Leaderboards Service

[![Build status](https://ci.appveyor.com/api/projects/status/77fd6okl8bc2ulkb/branch/master?svg=true)](https://ci.appveyor.com/project/leonard-thieu/leaderboards-service/branch/master)
[![codecov](https://codecov.io/gh/leonard-thieu/leaderboards-service/branch/master/graph/badge.svg)](https://codecov.io/gh/leonard-thieu/leaderboards-service)

## Overview

**toofz Leaderboards Service** is a backend service that handles updating [Crypt of the NecroDancer](http://necrodancer.com/) leaderboards for [toofz API](https://api.toofz.com/). 
It polls [Steam Community Data](https://partner.steamgames.com/documentation/community_data) at regular intervals to provide up-to-date data.

---

**toofz Leaderboards Service** is a component of **toofz**. 
Information about other projects that support **toofz** can be found in the [meta-repository](https://github.com/leonard-thieu/toofz-necrodancer).

### Dependents

* [toofz API](https://github.com/leonard-thieu/api.toofz.com)

### Dependencies

* [toofz Steam](https://github.com/leonard-thieu/toofz-steam)
* [toofz Data](https://github.com/leonard-thieu/toofz-data)
* [toofz Services Core](https://github.com/leonard-thieu/toofz-services-core)
* [toofz Build](https://github.com/leonard-thieu/toofz-build)

## Requirements

* .NET Framework 4.6.1
* SQL Server

## Contributing

Contributions are welcome for toofz Leaderboards Service.

* Want to report a bug or request a feature? [File a new issue](https://github.com/leonard-thieu/leaderboards-service/issues).
* Join in design conversations.
* Fix an issue or add a new feature.
  * Aside from trivial issues, please raise a discussion before submitting a pull request.

### Development

#### Requirements

* Visual Studio 2017

#### Getting started

Open the solution file and build. Use Test Explorer to run tests.

## License

**toofz Leaderboards Service** is released under the [MIT License](LICENSE).
