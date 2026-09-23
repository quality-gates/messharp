using System.Collections.Generic;
class Arrow
{
    static List<int> _log = new();
    static void Block(List<int> xs) { xs.Add(1); }
    static void Expr(List<int> xs) => xs.Add(1);
    static void StaticBlock() { _log.Add(1); }
    static void StaticExpr() => _log.Add(1);
    static bool Kept(List<int> xs) => xs.Remove(1);
}
