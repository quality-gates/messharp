using MessCS.Model;

namespace MessCS.Rule;

/// <summary>Base rule interface: every rule has metadata.</summary>
public interface IRule
{
    string Name { get; }
    string Message { get; }
    int Priority { get; }
    string SetName { get; }
    string ExternalUrl { get; }
    string Description { get; }
    string Since { get; }
}

public interface IClassRule : IRule
{
    void Apply(RuleContext ctx, ClassModel cls);
}

public interface IInterfaceRule : IRule
{
    void Apply(RuleContext ctx, InterfaceModel iface);
}

public interface IMethodRule : IRule
{
    void Apply(RuleContext ctx, MethodModel method);
}

public interface IFunctionRule : IRule
{
    void Apply(RuleContext ctx, MethodModel function);
}

public interface IFileRule : IRule
{
    void Apply(RuleContext ctx);
}
