# Rules and rulesets

Pass rulesets by name (comma-separated), or pass a path to your own
phpmd-format ruleset XML file.

| Ruleset | What it checks |
| :--- | :--- |
| **`csharp`** | **Recommended default.** Pulls in the component rulesets below, but tunes rules whose PHP defaults misfire on idiomatic C#. |
| `codesize` | CyclomaticComplexity, NPathComplexity, ExcessiveMethodLength, ExcessiveClassLength, ExcessiveParameterList, ExcessivePublicCount, TooManyFields, TooManyMethods, TooManyPublicMethods, ExcessiveClassComplexity |
| `naming` | ShortClassName, LongClassName, ShortVariable, LongVariable, ShortMethodName, ConstantNamingConventions, BooleanGetMethodName |
| `unusedcode` | UnusedPrivateField, UnusedLocalVariable, UnusedPrivateMethod, UnusedFormalParameter |
| `cleancode` | BooleanArgumentFlag, ElseExpression, StaticAccess, IfStatementAssignment, DuplicatedArrayKey |
| `design` | ExitExpression, GotoStatement, CountInLoopExpression, DevelopmentCodeFragment, EmptyCatchBlock, CouplingBetweenObjects, GlobalVariable, LackOfCohesionOfMethods |
| `controversial` | CamelCaseClassName, CamelCaseMethodName, CamelCasePropertyName, CamelCaseParameterName, CamelCaseVariableName — adapted to C# conventions (PascalCase types/members, camelCase locals/params) |
| `opinionated` | **Opt-in.** Rules the `csharp` ruleset deliberately drops because they fight common C# practice. |
| `explicitness` | **Opt-in.** ImplicitInput, ImplicitOutput, UnknownEffect. Tracks recognized implicit inputs, implicit outputs, and unresolved calls. See [Explicitness](#explicitness). |
| `explicitness-strict` | **Opt-in.** `explicitness` plus ImplicitInstanceInput, ImplicitInstanceOutput, which also treat an instance method's own fields and properties as implicit. |

Rules with a direct C# analog reproduce phpmd's behavior and message templates;
rules that are intrinsically PHP-specific are adapted or omitted (the C#
compiler already enforces several — e.g. `ConstructorWithNameAsEnclosingClass`
is a compile error).

## Notable adaptations

* `ConstantNamingConventions` checks **PascalCase** (the C# convention) rather
  than UPPERCASE; set its `convention` property to `upper` for phpmd behavior.
* `GlobalVariable` is **mutation-aware**: it reports only mutable static fields
  that are actually mutated. `static readonly` and `const` members stay silent.
  Set `report-immutable=true` to also surface un-mutated mutable statics.
* `LackOfCohesionOfMethods` computes the **LCOM4** cohesion metric per class.
  Stateless helpers and trivial getters/setters are ignored so plain data
  carriers stay quiet.

## Explicitness

The `explicitness` rulesets apply the idea of implicit inputs and outputs from
Eric Normand's *Grokking Simplicity*. A method's explicit inputs are its
arguments, including the receiver's fields; its explicit output is its return
value. Mutations visible to callers and reads of other changing state are
implicit effects. These syntax rules classify the supported syntax subset. A clean
report is a calculation verdict only inside that subset, not a proof about
arbitrary runtime behavior.

| Rule | Reports |
| :--- | :--- |
| `ImplicitInput` | Reads of the class's own mutable static fields and static auto-properties, including readonly collections with observed writes or exported aliases; reads of mutable captured state; the clock (`DateTime.Now`, `Stopwatch`), environment, console input, file and directory reads, `Guid.NewGuid()`, and `Random.Shared` or unseeded `new Random()`. |
| `ImplicitOutput` | Writes to the class's own static or instance state, and changes to the objects it holds (including `static readonly` collections); changes to an argument's object or captured storage; writes to `ref` and `out` parameters; console, `Debug` and `Trace` output; file and directory writes; environment changes, `Environment.Exit` and `Process.Start`. |
| `UnknownEffect` | Effects whose dispatch or storage binding is not established, including injected or rebound callbacks, opaque external calls, custom properties, indexers, operators, and events. |
| `ImplicitInstanceInput` | (strict) Reads of the instance's own fields, properties and primary constructor parameters, bare or through `this`/`base`. |
| `ImplicitInstanceOutput` | (strict) Writes to the instance's own fields and properties, and changes to the objects they hold. |

Each distinct input or output is reported once per method, at its first use.
Static constructors are exempt from the static-state checks; constructors and
static methods are exempt from the strict rules. Assigning a new value to a
by-value parameter is not an output.

The analysis is syntax-only, so the supported calculation subset is explicit:

* Plain built-in numeric, boolean, string, array, and collection operations are
  trusted only when their standard binding is not shadowed by a local symbol
  or user-defined type in the analyzed file. A method name alone does not make
  a custom method pure.
* Effects propagate through resolved same-file, non-virtual, non-abstract,
  non-override and non-extern methods, local functions, and lambdas until a
  fixed point. Recursive cycles terminate because summaries are finite sets of
  stable effect facts.
* Statement and conditional-expression arms guarded by literal `true` or
  `false` conditions are analyzed according to the arm that can execute.
  Literal-false `while` and `for` bodies do not contribute effects or storage
  writes. General constant folding is not part of the supported subset.
* Deferred callback creation does not execute the callback. Invoking a
  resolved callback carries its effects to the caller, with callback arguments
  and lexical captures bound separately. Returned closures retain their
  factory arguments and captured storage. Injected or rebound callbacks stay
  unknown.
* Recognized iterator methods, LINQ `Select` and `Where`, and `Lazy<T>` creation
  defer work. Enumeration, terminal sequence operations, or `Lazy<T>.Value`
  transfer effects to the execution site and apply storage writes before later
  reads. Recognized eager collection callbacks execute at their call site.
  Known delegate signatures supply callback parameter and return types.
  Explicit and target-typed lazy factories in declarations, assignments,
  returns, switch-expression arms, and static fields have a separate `<lazy>`
  owner. Named constructor arguments bind `valueFactory` by name, including
  reordered thread-safety options. Rebinding a lazy variable preserves the
  factory retained by an earlier alias. Resolved helpers, including non-virtual
  instance and extension methods, transfer known factory effects at the call site.
  Direct parameter replacement forces the replacement rather than the original
  argument. Unbound or partly known factory values retain `UnknownEffect`.
  Lazy values retain provenance through identity returns, static constructor
  assignments, and known `out` or `ref` writes. Writing an `out` parameter is an
  output even when the assigned Lazy has a pure factory.
  A Lazy without a factory constructs its value when forced. Known same-file
  constructor effects transfer at `.Value`; unresolved value construction stays
  unknown. Ordinary scalar defaults are quiet. Inherited Lazy properties use
  the base factory, but a subclass hiding `Value` keeps property-dispatch
  uncertainty. Conditional `.Value` access transfers the possible factory effects.
  Each factory body has its own findings; creation does not transfer those
  effects to the creator. Static initializer execution belongs to `<module>`,
  while forcing its lazy value transfers factory effects to the forcing method.
  Expression-bodied properties, indexers, and event accessors own their bodies.
  Anonymous bodies nested inside a factory keep their `<lambda>` owner rather
  than inheriting the factory's `<lazy>` name.
* Argument ownership is tracked at each operation site. Direct aliases retain
  caller ownership. Rebinding to a fresh object detaches the outer storage. A
  fresh shallow copy keeps caller-owned reachable elements, so mutating an
  element can still be an output.
* Fresh list, array, and proven field holders retain stored caller aliases,
  including initializer values and eager helper writes or returns. Exact
  unconditional replacement of one known slot detaches its old value;
  conditional writes and uncertain indices join possible origins. Allocation
  sites and acyclic call contexts bound heap identity, and loops join storage.
* A `ref` local to a proven field or standard array or collection element
  retains that storage location, so writes through either alias are visible
  through the other. Standard collection removal and reversal move known
  element slots; uncertain removal or reordering joins possible elements.
* Explicit and target-typed construction execute a resolved same-file
  constructor. A compiler-generated empty constructor is quiet only for a
  unique non-partial class without primary parameters, inheritance, a declared
  constructor, or field, event, or property initializers. Unmodeled default
  initialization stays unknown.
* Mutating a fresh private argument through a helper is a calculation when the
  helper has no ambient or unknown effects. Ambient and unknown effects are
  still propagated.
* Readonly collection reads are considered stable only within the analyzed
  closed-world subset. External aliases, reflection, dynamic dispatch, partial
  files outside the analyzed inputs, and unmodeled writes retain uncertainty
  rather than proving immutability.
* Async scheduling, unsupported deferred APIs, dynamic dispatch, event raising,
  thrown exceptions, expression trees, reflection, and external dependencies
  are not proved pure. Unsupported dispatch produces `UnknownEffect`.

## Custom rulesets

Ruleset XML supports phpmd's `<rule ref="...">` form, `<exclude name="..."/>`
children, and single-rule property/priority overrides.

```xml
<ruleset name="team policy">
  <rule ref="csharp">
    <exclude name="DevelopmentCodeFragment" />
  </rule>
  <rule ref="LongVariable">
    <priority>2</priority>
    <properties>
      <property name="maximum" value="50" />
    </properties>
  </rule>
</ruleset>
```

```console
messharp ./src text path/to/team-policy.xml --ignore-tests
```
