using System.Data;

namespace PrIMA.Api.Common;

public interface ISqlConnectionFactory
{
    IDbConnection CreateConnection();
}
