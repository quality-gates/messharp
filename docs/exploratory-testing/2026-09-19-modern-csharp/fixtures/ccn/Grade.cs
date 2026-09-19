namespace Shop.Ccn;

public class Grader
{
    public string Grade(int score) => score switch
    {
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 60 => "D",
        >= 50 => "E",
        >= 40 => "F",
        >= 30 => "G",
        >= 20 => "H",
        >= 10 => "I",
        _ => "J",
    };

    public string GradeIf(int score)
    {
        if (score >= 90) return "A";
        if (score >= 80) return "B";
        if (score >= 70) return "C";
        if (score >= 60) return "D";
        if (score >= 50) return "E";
        if (score >= 40) return "F";
        if (score >= 30) return "G";
        if (score >= 20) return "H";
        if (score >= 10) return "I";
        return "J";
    }
}
