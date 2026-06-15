# Chickensoft Architecture Conventions

## Layers

- Domain code owns rules, immutable data, repositories, and state transitions.
- LogicBlocks coordinate domain operations and emit presentation-facing outputs.
- Godot nodes own scene-tree access, resources, input, engine settings, animation, and formatting.
- Domain code must not reference `Node`, `Resource`, `Input`, `Engine`, scene loading, or visual formatting.
- Godot math structs are allowed in domain code.

## Nodes And Injection

- Apply `[Meta(typeof(IAutoNode))]` to injectable Godot nodes.
- Call `this.Notify(what)` from `_Notification`.
- Use `[Node]` for authored scene children and unique node names.
- Use `[Node("relative/path")]` only when repeated component children cannot be uniquely named, such as submenu back buttons.
- Prefer Godot node interfaces and public injectable setters where practical.
- Resolve dependencies in `Setup`, wire signals in `OnResolved`, and release them in `OnExitTree`.
- Dispose LogicBlock bindings and unsubscribe every subscribed signal in `OnExitTree`.

## Providers

- Scene roots provide long-lived application services.
- Consumers use `[Dependency]` rather than walking ancestors or locating globals.
- Runtime scene creation uses `IInstantiator`; do not call `GD.Load<PackedScene>` from feature nodes.

## LogicBlocks

- Inputs describe intent.
- States call repositories and domain services.
- Outputs contain only data needed by the receiving node.
- Engine and scene-tree side effects are handled by nodes receiving outputs.
- Presentation acknowledgement may be sent back as a LogicBlock input.

## Repositories

- Repositories store and mutate domain state only.
- Repositories do not parse nodes or resources.
- Visual/application compilers convert authored Godot data into domain values before repository calls.

## Scene Instantiation

- Load and instantiate runtime scenes through `IInstantiator`.
- Authored static children remain in `.tscn` files and are injected with `[Node]`.

## Testing

- Mirror `src` paths under `test/src`.
- Prefer focused domain and state tests.
- Use LightMoq, fake bindings, `FakeDependency`, `FakeNodeTree`, and `StateTester`.
- Use `Fixture` only when real scene-tree behavior is required.
- Test authored resource wiring, dependency wiring, cleanup, exact lookup behavior, and acknowledgement protocols.
