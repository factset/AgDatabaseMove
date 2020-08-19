# AgDatabaseMove
AgDatabaseMove is a library that we use at FactSet to support moving a database from one availability group to another.
We hope that the native handling of availability groups will be helpful to other developers or the CLI built on top of it will be useful to DBA.

## Getting started

### Prerequisites
* SQL Server Availability Group
* Availability database
* A backup location accessible by each SQL Server instance
* dotnet core 3.0

### Installing
The intention is to make this available via NuGet so it can be installed to your solution as a library or PowerShell as the CLI.
For now, you'll need to download and build the project yourself. See [contributing.md](contributing.md) details on compiling.

### Library
We intend to use this as a library to support a move with limited client access downtime. An example of how this
will be accomplished is in the integration test method [ProgressiveRestore](tests/AgDatabaseMove.Integration/TestRestore.cs)

### CLI
You can currently use the CLI to create a copy of a database from one AG to another. It will take a log backup,
then restore from the existing backup chain to the new database. If a database exists in the destination AG
it will delete it and proceed with the copy.
```
AgDatabaseMove.Cli --From:ConnectionString="Server=SourceDatabaseListener.domain.com; Integrated Security=true; MultiSubnetFailover=true;"
    --From:DatabaseName=sourceDbName
    --From:BackupPathTemplate="\\NetworkShare\{0}_backup_{1}.trn"
    --To:ConnectionString="Server=DestinationDatabaseListener.domain.com; Integrated Security=true; MultiSubnetFailover=true;"
    --To:DatabaseName=DestinationDbName
    --Overwrite=true
```

## Contributing
If you would like to contribute see [contributing.md](contributing.md) for details.

## Authors
* Ryan Clare - [FactSet](http://www.github.com/FactSet)

## License
This project is licensed under the Apache 2.0 License - see [license.md](license.md) for details.
