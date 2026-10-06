namespace DevStation.Application.Shared.Security;

public static class Tiers
{
    public static int Rank(string t) => t switch
    {
        "read" => 1,
        "execute" => 2,
        "deploy" => 3,
        "admin" => 4,
        _ => 0,
    };
}
