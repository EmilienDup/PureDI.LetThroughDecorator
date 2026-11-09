# `ctx.Let` does not reach the instance behind a decorator

**Pure.DI master (b6feb8b)** and **2.5.7**, .NET SDK 10.0.403, macOS. Repro project attached (`dotnet build`, `-p:Variant=DECORATOR_CLASS`, `-p:Variant=NO_DECORATOR`, `-p:Variant=BUILT_IN_FUNC`).

Follow-up to #184, where DIW016 recommends binding `Func<T1, T>` with `ctx.Let` so that a delegate argument reaches only the object the delegate creates. That works until the created object is decorated: the `Let` value reaches the decorator, but not the decorated instance, which is the one that consumes it.

## Minimal code

```csharp
using Pure.DI;
using static Pure.DI.Lifetime;

var composition = new Composition();
Console.WriteLine(composition.Root);

public interface IModel;

public sealed class Model(string name) : IModel
{
    public override string ToString() => name;
}

public interface IViewer;

public sealed class Viewer(IModel model) : IViewer
{
    public override string ToString() => $"viewer of {model}";
}

// Keeps track of the created viewers
public sealed class Tracker
{
    public int Count { get; private set; }

    public void Add(IViewer viewer) => Count++;
}

public sealed class App(Func<IModel, IViewer> viewerFactory)
{
    public override string ToString() => viewerFactory(new Model("a")).ToString();
}

partial class Composition
{
    private static void Setup() => DI.Setup(nameof(Composition))
        // The argument reaches only the object the delegate creates
        .Bind<Func<TT1, TT>>()
            .As(PerBlock)
            .To(ctx => new Func<TT1, TT>((TT1 arg1) =>
            {
                ctx.Let<TT1>(arg1);
                ctx.Inject<TT>(ctx.Tag, out var value);
                return value;
            }))
        .Bind().As(Singleton).To<Tracker>()
        .Bind<IViewer>("base").To<Viewer>()
        .Bind<IViewer>().To(ctx =>
        {
            ctx.Inject(out Tracker tracker);
            ctx.Inject<IViewer>("base", out var viewer);
            tracker.Add(viewer);
            return viewer;
        })
        .Root<App>("Root");
}
```

## Expected

The code compiles and prints `viewer of a`: the delegate argument reaches the `Viewer` that the decorated `IViewer` is made of.

## Actual

```
Program.cs(16,35): error DIE000: Unable to resolve "IModel" in Viewer(IModel model<--IModel).
```

## Variants

| variant | 2.5.7 | master b6feb8b |
|---|---|---|
| decorator factory that injects the `"base"` instance (above) | DIE000 | DIE000 |
| decorator class `TrackedViewer([Tag("base")] IViewer viewer, Tracker tracker) : IViewer` | DIE000 | DIE000 |
| no decorator: `.Bind<IViewer>().To<Viewer>()` | prints `viewer of a` | prints `viewer of a` |
| built-in `Func<T1, T>` (`ctx.Override`) | prints `viewer of a` | prints `viewer of a` |

The same happens with a plain `ctx.Let<IModel>(…)` followed by `ctx.Inject(out IViewer viewer)` in a factory.

## Why this matters

The README says that "`Let` applies only to the current injection level", and the decorator is that level. But a decorator of the same contract is part of the object the caller asked for, so a caller that passes an argument for an `IViewer` cannot tell, and should not need to know, whether that `IViewer` is decorated.

In a real application with several hundred delegates, decorators like this are common: tracking, logging, caching and normalizing wrappers around services and view models. There are 28 in ours. With the built-in `Func<T1, T>`, the arguments reach everything the delegate creates, and the override rewrite does not finish on that graph. With the `ctx.Let` binding, the rewrite takes seconds, but every decorated object made by a delegate fails like this one. Neither option works for such an application today.

## Suggestion

Let a `Let` value pass through a decorator of the same contract: when the factory or constructor at the current level injects another binding of the contract it is building (here `IViewer` tagged `"base"`), apply the current level's `Let` values to that injection too. Repeat for chains of decorators. Other dependencies of the decorator, such as `Tracker`, stay out of reach as today.
