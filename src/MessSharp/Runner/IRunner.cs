using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.Runner;

public interface IRunner
{
    MessSharp.Report.Report Run(RunOptions opts);
}
