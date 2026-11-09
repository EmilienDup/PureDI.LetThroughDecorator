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

#if DECORATOR_CLASS
public sealed class TrackedViewer([Tag("base")] IViewer viewer, Tracker tracker) : IViewer
{
    public override string ToString()
    {
        tracker.Add(viewer);
        return $"tracked {viewer}";
    }
}
#endif

public sealed class App(Func<IModel, IViewer> viewerFactory)
{
    public override string ToString() => viewerFactory(new Model("a")).ToString();
}

partial class Composition
{
    private static void Setup() => DI.Setup(nameof(Composition))
#if !BUILT_IN_FUNC
        // The argument reaches only the object the delegate creates
        .Bind<Func<TT1, TT>>()
            .As(PerBlock)
            .To(ctx => new Func<TT1, TT>((TT1 arg1) =>
            {
                ctx.Let<TT1>(arg1);
                ctx.Inject<TT>(ctx.Tag, out var value);
                return value;
            }))
#endif
        .Bind().As(Singleton).To<Tracker>()
#if NO_DECORATOR
        .Bind<IViewer>().To<Viewer>()
#elif DECORATOR_CLASS
        .Bind<IViewer>("base").To<Viewer>()
        .Bind<IViewer>().To<TrackedViewer>()
#else
        .Bind<IViewer>("base").To<Viewer>()
        .Bind<IViewer>().To(ctx =>
        {
            ctx.Inject(out Tracker tracker);
            ctx.Inject<IViewer>("base", out var viewer);
            tracker.Add(viewer);
            return viewer;
        })
#endif
        .Root<App>("Root");
}
