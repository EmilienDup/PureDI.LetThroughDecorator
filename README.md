# Pure.DI: `ctx.Let` does not reach the instance behind a decorator

Minimal repro for Pure.DI master (b6feb8b) and 2.5.7. A `Func<IModel, IViewer>` bound with `ctx.Let` (so the argument reaches only the object the delegate creates) fails with DIE000 "Unable to resolve IModel in Viewer" as soon as `IViewer` is decorated. The `Let` value reaches the decorator but not the decorated `Viewer`, which is the object that needs it.

[`ISSUE.md`](ISSUE.md) is the text to paste into a Pure.DI GitHub issue.

```
dotnet build                                                                                   # 2.5.7 from nuget.org
dotnet build -p:PureDIVersion=<master build version> -p:RestoreAdditionalProjectSources=<folder with the package>
dotnet build -p:Variant=DECORATOR_CLASS                                                        # decorator class with a [Tag("base")] parameter
dotnet build -p:Variant=NO_DECORATOR                                                           # no decorator
dotnet build -p:Variant=BUILT_IN_FUNC                                                          # the built-in Func<T1, T> (ctx.Override)
```

| variant | 2.5.7 | master b6feb8b |
|---|---|---|
| default: decorator factory that injects the `"base"` instance | DIE000 | DIE000 |
| `DECORATOR_CLASS` | DIE000 | DIE000 |
| `NO_DECORATOR` | prints `viewer of a` | prints `viewer of a` |
| `BUILT_IN_FUNC` | prints `viewer of a` | prints `viewer of a` |
