using MessSharp.Model;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class FieldInitializerOwners
{
    public static IEnumerable<CallableOwner> Gather(SourceFile file)
    {
        foreach (var cls in file.Classes)
            foreach (var field in cls.Fields.Where(f => f.IsStatic))
            {
                if (field.Node is not FieldDeclarationSyntax declaration) continue;
                var variable = declaration.Declaration.Variables.First(v => v.Identifier.Text == field.Name);
                if (variable.Initializer is not { } initializer) continue;
                yield return new CallableOwner(new MethodModel
                {
                    Name = "<module>",
                    Node = variable,
                    Class = cls,
                    File = file,
                }, initializer.Value);
            }
    }
}
