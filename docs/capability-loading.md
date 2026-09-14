# Remote capability loading

A widget's Blazor component sometimes needs data that only makes sense next to the real product,
for example a log viewer, or a connector health check. An operations aggregator that renders that
same widget against a remote host can't run the product's real implementation locally (it doesn't
have the product's database, credentials, or any of the state that implementation touches), but it
does get the product's plugin assembly deployed to it as a normal, versioned file. `CapabilityLoader`
turns that file into an HTTP-calling proxy for the widget's dependency, without ever running the
real implementation.

## Capability contracts

A capability contract is an interface a widget depends on instead of a concrete class, marked
`[CapabilityContract]`:

```csharp
[CapabilityContract]
public interface ILogStore
{
    Task<IReadOnlyList<LogEntry>> SearchAsync(LogFilter filter, CancellationToken ct = default);
}

public sealed class LogStore : ILogStore { /* touches the real log database */ }
```

The product registers `LogStore` for its own local use, same as any other service. The one-time
refactor a widget needs is exactly this: depend on `ILogStore`, never on `LogStore`, so nothing
about the widget's own code changes when it is later rendered by an aggregator instead of the
product itself.

Only mark an interface this way if it is meant to be proxied remotely. An ordinary framework or
hosting dependency (`NavigationManager`, `IJSRuntime`, anything DI hands you locally) must never
carry `[CapabilityContract]`, since those are supplied by whichever process is actually rendering,
not proxied, and a `CapabilityContractSafetyAnalyzer` (see below) does not catch this class of
mistake; it is on you.

## `ICapabilityLoader`

```csharp
var loader = new CapabilityLoader();
CapabilityLoadResult result = loader.Load("path/to/product.dll");
// result.Status: Loaded | AssemblyNotFound | LoadFailed
// result.Capabilities: the [CapabilityContract] interfaces now available

Type logStore = loader.AvailableCapabilities.Single(t => t.Name == nameof(ILogStore));
object? proxy = loader.CreateProxy(logStore, httpClient); // or loader.CreateProxy<ILogStore>(httpClient)
```

`Load` uses the same collectible-`AssemblyLoadContext` isolation `PluginLoader` uses (private
dependencies resolved from the plugin's own folder; `ConfigForge.*` types left to the host's
default context) so a capability assembly and a plugin assembly can be the same file. Unlike
`PluginLoader`, it **never calls `Activator.CreateInstance` on a concrete type from the assembly
and never invokes any of its methods**. It only reflects over `assembly.GetTypes()` looking for
interfaces carrying `[CapabilityContract]`.

`CreateProxy` builds a `System.Reflection.DispatchProxy` for one of `AvailableCapabilities` and
returns `null` for anything else, such as an interface without `[CapabilityContract]`, or one from
a different assembly entirely, instead of throwing. A caller checks for `null` the same way it
would check any other lookup miss.

## The wire contract

A proxy call is a `POST {prefix}/action/{interface}.{method}`, the same endpoint
[dashboard-protocol.md](dashboard-protocol.md) documents, with the interface and method name
supplying the `actionId` in place of a hand-written one. The request body extends that endpoint's
existing shape with the call's arguments (any trailing `CancellationToken` parameter omitted); the
response extends it with the method's return value:

```json
// request
{ "args": [{ "instance": "prod-1", "pageSize": 50 }] }

// response
{ "success": true, "message": null, "result": [{ "id": 1, "message": "..." }] }
```

`success: false` (or a non-2xx response, or a missing body) surfaces to the caller as a
`CapabilityRemoteCallException`, never an unhandled exception. A capability method must return
`Task` or `Task<T>`; anything else throws `NotSupportedException` when called, since a
`DispatchProxy` has no synchronous escape hatch that does not block a thread, and this codebase
never blocks on async work.

Serving these calls (reading `args`, calling the real local implementation, writing `result`) is
the product's own composition-root responsibility, the same way it already wires up
`RegisterAction` handlers for the existing action endpoint. `CapabilityLoader` is the client side.

## What this guarantees, and what it does not

`CapabilityLoader` guarantees it never executes code from the capability assembly to build or use
a proxy. It does **not** turn the assembly into a sandboxed artifact. This is discipline, not a
CLR sandbox:

- Loading the assembly still runs its module initializers, static field initializers, and any
  default interface method body, none of which require an explicit `new` or method call.
  `CapabilityContractSafetyAnalyzer` (`CFCAP001`/`CFCAP002`) catches the two concrete,
  statically-checkable instances of this: a default body directly on a marked interface, and a
  `[ModuleInitializer]` method anywhere in the same project. It does not attempt to catch every
  theoretical variant (a static initializer on some unrelated type the interface happens to
  reference, for instance).
- Safety depends entirely on the assembly reaching the loader through a controlled deployment
  step, the same versioned file the product itself built and shipped, **never** fetched live
  over the network from an untrusted or remote instance. `CapabilityLoader` has no opinion on
  where the file came from; that trust boundary is the caller's to hold.

## Error handling

**Unknown or unmarked capability.** `CreateProxy` returns `null`; `Load` never throws for a
well-formed-but-irrelevant assembly, it simply reports which `[CapabilityContract]` interfaces (if
any) it found.

**The assembly file disappears, then reappears.** `Load` reads the file into memory
(`AssemblyLoadContext.LoadFromStream`, not `LoadFromAssemblyPath`) specifically so the file is
never held open, so a delete-then-recreate deploy step works even while the previous load is still
in memory. A `Load` call against a path that no longer exists returns
`CapabilityLoadStatus.AssemblyNotFound` and **leaves `AvailableCapabilities` exactly as it was**,
nothing is reset. `CapabilityLoader` itself never persists or mutates any notion of "known
capabilities" beyond its own in-memory current set; a component that tracks capability
availability across restarts (e.g. an aggregator's own instance list) is responsible for treating
a transient `AssemblyNotFound` as soft, recoverable state rather than permanent removal. Mirror
`ConfigForgeDirectoryWatcher`'s pattern of reacting to file system events, but add a grace period
before treating "missing" as "gone", since delete-then-recreate is an expected, not exceptional,
deploy sequence.

**A call is in flight through a proxy while the assembly reloads.** A capability interface's
`Type` always lives in a collectible `AssemblyLoadContext`; a `DispatchProxy`'s generated type
necessarily references that `Type` and therefore keeps its context alive for as long as the proxy
itself is reachable. `AssemblyLoadContext.Unload()` only *requests* collection, and the runtime
defers it until every reference into the context (a live proxy, an in-flight call's stack frame)
is gone. `CapabilityLoader.Load` requests unload of the *previous* context on every successful
reload; it never forces it. A proxy built before a reload keeps working, calling out over HTTP
exactly as before, until nothing references it any more, at which point its context becomes
eligible for collection on its own. There is no scenario where an in-flight call crashes because
of a concurrent reload.

## Why the proxy is its own tiny assembly

`DispatchProxy.Create` generates a proxy type whose *collectibility* is decided by its **base**
type's assembly, never the interface's, and a non-collectible assembly cannot reference a
collectible one. A capability interface always lives in a collectible context, so
`CapabilityLoader` cannot hand `DispatchProxy.Create` a base type from its own, ordinarily-loaded
`ConfigForge.Core.dll`; it instead loads a private copy of the tiny `ConfigForge.Core.CapabilityProxy.dll`
(embedded as a resource in `ConfigForge.Core.dll`) into the very same collectible context as the
interface, and reaches it by reflection. That assembly declares nothing but the proxy's
`DispatchProxy` subclass. `CapabilityCallRequest`/`CapabilityCallResponse<T>`/
`CapabilityRemoteCallException` stay in `ConfigForge.Abstractions` so the private copy still
throws and serializes the exact same types the caller's own reference to `ConfigForge.Abstractions`
declares, instead of a second, uncatchable set bundled into the collectible copy.

See [dashboard-protocol.md](dashboard-protocol.md) for the endpoints this proxy calls, and
[plugins.md](plugins.md) for the plugin assembly format both loaders share.
