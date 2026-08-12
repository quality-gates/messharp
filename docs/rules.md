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
