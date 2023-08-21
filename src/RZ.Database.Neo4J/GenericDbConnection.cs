namespace RZ.Database;

public readonly record struct GenericDbConnection(string Host, Option<string> Database, Option<string> User, Option<string> Password)
{
    public static GenericDbConnection From(string s) {
        var parts = s.Split(';').Map(toKeyValue).ToMap();
        return new(parts["Server"], parts.Get("Database"), parts.Get("User"), parts.Get("Password"));

        (string Key, string Value) toKeyValue(string kv) {
            var p = kv.Split('=');
            return (p[0], p[1]);
        }
    }
}