# Contributing

This covers building dmd-extensions, testing a change, and getting it merged.

## Building

The solution is `DmdExtensions.sln`, targeting .NET Framework 4.7.2. You need Visual Studio with:

- the .NET Framework 4.7.2 targeting pack
- C++/CLI support for the v143 build tools, since `LibDmd` references the C++/CLI `ProPinballBridge`
  project
- the WiX Toolset 3.11, only for the `Installer` project

CI restores and builds with:

```
nuget restore DmdExtensions.sln
.\DllExport -action Restore -sln-file DmdExtensions.sln
msbuild -t:rebuild /p:Platform=x64 /p:Configuration=Release DmdExtensions.sln
```

When building a single project from the command line, also pass `/p:SolutionDir=<repo path>\`. Without
it DllExport fails with `DllExport.bat is not found. Path: '*Undefined*'`.

## Testing

`LibDmd.Test` holds NUnit unit tests for the render graph, frame conversion and scaling. Much of
dmd-extensions can't be covered by unit tests, so [LibDmd.Test/README.md](LibDmd.Test/README.md) lists
the games and commands to check by hand for each source. Check the sources your change touches.

## Continuous integration

GitHub Actions builds x86 and x64 Release, the installer and the portable bundle on every push. It does
not run on pull requests, so run it on your fork:

1. Enable workflows in your fork's Actions tab. GitHub starts forks with them off.
2. Push your branch.
3. Link the passing run in your pull request.

The build puts the branch name into the MSI and artifact file names, so a branch name containing `/`
fails at the installer step. Use `-` instead.

CI does not run the unit tests.

## Commits and pull requests

- Open pull requests against `master`.
- Pull requests are merged by rebasing, so each commit lands on `master` as written, message included.
- Commit subjects name the area touched, then say what changed, with the issue when there is one:
  `dmddevice: Ignore invalid host color and palette input. Fixes #556.`
- There is no changelog file. Release notes are written on GitHub Releases, so put one in the pull
  request description and call out anything existing setups will notice.
- Leave `VersionAssemblyInfo.cs` alone. Versions are bumped by release commits.
