namespace Shop.Ci;

public class Box<T> where T : class
{
    public bool GetFlag() => true;

    public bool GetOther() => false;
}

public class box_of_things
{
    public bool GetFlag() => true;
}
