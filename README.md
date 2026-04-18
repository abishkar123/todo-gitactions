# TodoList (ASP.NET Core MVC + MongoDB)

## Getting started

1. Ensure a MongoDB Atlas cluster is reachable.
2. Keep source-controlled config free of credentials. Use one of these options for the real connection string:
   - `dotnet user-secrets set "MongoDb:ConnectionString" "<your-connection-string>"`
   - `$env:MongoDb__ConnectionString="<your-connection-string>"`
3. If you want a local file, copy `appsettings.Development.example.json` to `appsettings.Development.json` and replace `<username>` and `<password>`. That file is gitignored.
4. Optionally override `MongoDb:DatabaseName` and `MongoDb:TodoCollectionName` the same way.
5. From the repo root run `dotnet run --project TodoList.csproj`.

## Git hook secret scan

1. Enable the tracked hook path once for this clone:
   - `git config core.hooksPath .githooks`
2. The hook runs before every commit and scans staged changes.
3. If `gitleaks` is available on `PATH`, the hook uses it for the scan.
4. If `gitleaks` is not installed, the hook falls back to a built-in staged diff scan for common secrets such as MongoDB URIs with credentials, private keys, GitHub tokens, AWS keys, and password assignments.
5. The hook fails the commit if neither `pwsh` nor `powershell.exe` is available, so secret scanning cannot be bypassed by a missing runtime.
6. If you prefer the `pre-commit` framework, install it separately and run `pre-commit install`. The repo includes a compatible `.pre-commit-config.yaml`.

## Notes

- `appsettings.json` contains placeholders only and is safe to commit.
- `appsettings.Development.json` is local-only and ignored by git.
- If a real secret has ever been committed, rotate it even after removing it from the working tree.
