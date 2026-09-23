class Dtor
{
    ~Dtor() => System.Console.WriteLine("arrow");
}
class DtorBlock
{
    ~DtorBlock() { System.Console.WriteLine("block"); }
}
