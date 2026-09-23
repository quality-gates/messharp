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
| `explicitness` | **Opt-in.** ImplicitInput, ImplicitOutput — data that enters a method other than through its arguments, or leaves it other than through its return value. See [Explicitness](#explicitness). |
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
arguments; its explicit output is its return value. Everything else is
implicit, and makes the method an action rather than a calculation.

| Rule | Reports |
| :--- | :--- |
| `ImplicitInput` | Reads of the class's own mutable static fields and static auto-properties; the clock (`DateTime.Now`, `Stopwatch`), environment, console input, file and directory reads, `Guid.NewGuid()`, and `Random.Shared` or unseeded `new Random()`. |
| `ImplicitOutput` | Writes to the class's own static state, and changes to the objects it holds (including `static readonly` collections); changes to an argument's object; writes to `ref` and `out` parameters; console, `Debug` and `Trace` output; file and directory writes; environment changes, `Environment.Exit` and `Process.Start`. |
| `ImplicitInstanceInput` | (strict) Reads of the instance's own fields, properties and primary constructor parameters, bare or through `this`/`base`. |
| `ImplicitInstanceOutput` | (strict) Writes to the instance's own fields and properties, and changes to the objects they hold. |

Each distinct input or output is reported once per method, at its first use.
Static constructors are exempt from the static-state checks; constructors and
static methods are exempt from the strict rules. Assigning a new value to a
by-value parameter is not an output.

The analysis is syntax-only, so it has limits:

* Only the class's own static state is tracked. Static state of other classes,
  and members called through `using static`, are not.
* Effects are not followed through method calls: calling a method that writes
  to the console does not make the caller an action.
* A call whose result is discarded counts as a change to its receiver. That
  is a call statement such as `items.Add(x);`, or the expression body of a
  member that returns nothing, such as `void Add(int x) => items.Add(x);`.
  Calls made only for an `out` value, such as
  `map.TryGetValue(k, out var v);`, and calls on struct arguments can
  therefore be reported.
* Lambdas and local functions count toward their enclosing method. A lambda's
  expression body, such as `x => sink.Add(x)`, is not taken as discarded,
  because its delegate type is not known.
* Raised events and thrown exceptions are not reported.
* Like every method rule, these rules do not see expression-bodied properties
  (`int X => _x;`), indexers or top-level statements, which the method model
  does not include.

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
