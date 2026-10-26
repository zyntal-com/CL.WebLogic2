namespace CL.WebLogic.MySql.Tests;

/// <summary>A fact that runs only when a test database is configured (<c>WEBLOGIC_MYSQL_TEST_HOST</c>).</summary>
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (!MySqlFixture.Configured)
            Skip = "Set WEBLOGIC_MYSQL_TEST_HOST (and _PORT, _DB, _USER, _PASS) to run the MySQL store tests.";
    }
}
