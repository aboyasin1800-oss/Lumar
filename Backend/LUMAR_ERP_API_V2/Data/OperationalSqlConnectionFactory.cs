using LUMAR_ERP_API_V2.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Data;

public sealed class OperationalSqlConnectionFactory(IOptions<DatabaseOptions> options)
{
    public SqlConnection Create()
    {
        if (string.IsNullOrWhiteSpace(options.Value.ConnectionString)) throw new InvalidOperationException("Set Lumar__ConnectionString outside the repository before calling database endpoints.");

        var builder = new SqlConnectionStringBuilder(options.Value.ConnectionString)
        {
            MultipleActiveResultSets = true
        };

        return new SqlConnection(builder.ConnectionString);
    }
}