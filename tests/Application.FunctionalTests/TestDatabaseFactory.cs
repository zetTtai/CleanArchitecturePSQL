namespace CleanArchitecturePSQL.Application.FunctionalTests;

public static class TestDatabaseFactory
{
    public static async Task<ITestDatabase> CreateAsync()
    {
        var database = new TestcontainersTestDatabase();

        try
        {
            await database.InitialiseAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }

        return database;
    }
}
