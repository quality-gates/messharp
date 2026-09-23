using System.Collections.Generic;
using System.Threading.Tasks;
class Db { public Task SaveAsync() => Task.CompletedTask; }
class Scope
{
    static List<int> _log = new();
    List<int> _items = new();
    static async Task AsyncTask(Db db) => await db.SaveAsync();        // discarded (async Task)
    static Task PassThrough(Db db) => db.SaveAsync();                 // returned: clean
    static async Task<int> AsyncValue(Db db) { await db.SaveAsync(); return 1; } // block control: reported
    int Prop { get => 0; set => _log.Add(value); }                    // set accessor arrow: discarded
    Scope() => _log.Add(1);                                           // constructor arrow: discarded
    ~Scope() => _log.Add(2);                                          // destructor arrow: discarded
    static void Outer(List<int> sink) { void L() => sink.Add(1); L(); } // local function arrow
}
